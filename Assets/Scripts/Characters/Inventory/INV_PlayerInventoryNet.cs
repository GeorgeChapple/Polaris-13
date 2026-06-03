using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Made By: Jason Lodge.
// Summary: Inventory and crafting networking,
// handles all server side capabilities for inventory and crafting.

public class INV_PlayerInventoryNet : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private INV_Inventory inventory;
    [SerializeField] private CC_CharacterValues characterValues;
    [SerializeField] private INV_Crafting crafting;

    [Header("Equipped Item")]
    [SerializeField] private Transform equippedItemRoot;

    [Tooltip("Fallback root used for non owners / server view. If null, uses replicated camera direction root, then this objects transform.")]
    [SerializeField] private Transform observerEquippedItemRoot;

    [SerializeField] private TwoBoneIKConstraint leftArmIKConstraint;
    [SerializeField] private TwoBoneIKConstraint rightArmIKConstraint;
    [SerializeField] private Transform leftArmTarget;
    [SerializeField] private Transform rightArmTarget;

    [Header("Throw Power, Power is force, Torque is rotational vel added +/- what ever it is.")]
    [SerializeField] private float testThrowPower;
    [SerializeField] private float testThrowTorque;

    [Header("Debug")]
    [SerializeField] private bool logEquippedItem;

    public event Action<bool, string> OnCraftRequestFinished;

    // replicated equipped item id, the visual is built locally on each player from this
    private NetworkVariable<FixedString128Bytes> equippedItemId = new NetworkVariable<FixedString128Bytes>
    (
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // local only, used so equipped consumables can remove the correct inventory item on the owner
    private string localEquippedInventoryItemUniqueId;
    private string localVisualItemId;
    private bool localEquipCallbackAlreadyFired;

    // one local equipped visual per instance
    // owner uses equippedItemRoot, non owners / server use observer root
    [SerializeField] private GameObject equippedVisual;

    private void Awake()
    {
        CacheRefs();
        CacheObserverEquippedRoot();
    }

    // setup network state once the object has spawned.
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        equippedItemId.OnValueChanged += OnEquippedItemIdChanged;
        RebuildEquippedVisuals(equippedItemId.Value.ToString());
    }

    // clean up network subscriptions when despawned.
    public override void OnNetworkDespawn()
    {
        equippedItemId.OnValueChanged -= OnEquippedItemIdChanged;

        ClearEquippedVisual();

        base.OnNetworkDespawn();
    }

    private void LateUpdate()
    {
        // update visuals after movement and camera changes.
        FollowEquippedRoot();
        SnapHandTargetsToItem();
    }

    private void CacheRefs()
    {
        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (characterValues == null)
        {
            characterValues = GetComponent<CC_CharacterValues>();
        }

        if (crafting == null)
        {
            crafting = GetComponentInChildren<INV_Crafting>();
        }
    }

    private void CacheObserverEquippedRoot()
    {
        if (observerEquippedItemRoot != null)
        {
            return;
        }

        CC_CameraController cameraController = GetComponentInParent<CC_CameraController>();
        if (cameraController != null)
        {
            observerEquippedItemRoot = cameraController.ReplicatedCameraDirectionRoot;
        }
    }

    private void FollowEquippedRoot()
    {
        if (equippedVisual == null)
        {
            return;
        }

        Transform followRoot = GetEquippedItemRootForThisInstance();
        if (followRoot == null)
        {
            return;
        }

        equippedVisual.transform.position = followRoot.position;
        equippedVisual.transform.rotation = followRoot.rotation;
        equippedVisual.transform.localScale = Vector3.one;
    }

    private void SnapHandTargetsToItem()
    {
        if (equippedVisual == null)
        {
            leftArmIKConstraint.weight = 0;
            rightArmIKConstraint.weight = 0;
        }
        else
        {
            CC_INV_EquippedItem equippedItem = equippedVisual.GetComponentInChildren<CC_INV_EquippedItem>();

            if (equippedItem != null)
            {
                INV_Item item = equippedItem.Item;
                if (item.TwoHanded)
                {
                    leftArmIKConstraint.weight = 1;
                    rightArmIKConstraint.weight = 1;
                }
                else
                {
                    leftArmIKConstraint.weight = 0;
                    rightArmIKConstraint.weight = 1;
                }
                if (equippedItem.RightHandSnapPoint != null)
                {
                    rightArmTarget.position = equippedItem.RightHandSnapPoint.position;
                    rightArmTarget.rotation = equippedItem.RightHandSnapPoint.rotation;
                }
                if (equippedItem.LeftHandSnapPoint != null)
                {
                    leftArmTarget.position = equippedItem.LeftHandSnapPoint.position;
                    leftArmTarget.rotation = equippedItem.LeftHandSnapPoint.rotation;
                }
            }
        }
    }

    private void OnEquippedItemIdChanged(FixedString128Bytes oldValue, FixedString128Bytes newValue)
    {
        RebuildEquippedVisuals(newValue.ToString());
    }

    // local sync rpcs
    [Rpc(SendTo.SpecifiedInParams)]
    public void AddItemLocalRpc(string itemId, RpcParams rpcParams = default)
    {
        CacheRefs();

        if (inventory == null || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        if (inventory.TryAddItem(item))
        {
            UnlockItemLocally(itemId);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AddItemAmountLocalRpc(string itemId, int amount, RpcParams rpcParams = default)
    {
        CacheRefs();

        if (inventory == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        bool addedAny = false;

        for (int i = 0; i < amount; i++)
        {
            if (inventory.TryAddItem(item))
            {
                addedAny = true;
            }
        }

        if (addedAny)
        {
            UnlockItemLocally(itemId);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AddItemAtCellLocalRpc(string itemId, int amount, int cellX, int cellY, int rotation, RpcParams rpcParams = default)
    {
        CacheRefs();

        if (inventory == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        bool added = inventory.TryAddItemAtCell
        (item,
        new Vector2Int(cellX, cellY),
        (INV_Inventory.ItemInstance.Rotation)rotation,
        amount
        );

        if (added)
        {
            UnlockItemLocally(itemId);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void CraftResultLocalRpc(bool succeeded, string craftedItemId, int recipeIndex, RpcParams rpcParams = default)
    {
        CacheRefs();

        if (inventory == null)
        {
            OnCraftRequestFinished?.Invoke(false, craftedItemId);
            return;
        }

        if (succeeded)
        {
            INV_Item craftedItem = GetItemById(craftedItemId);

            if (craftedItem != null)
            {
                int returnAmount = craftedItem.GetRecipeReturnAmount(recipeIndex);

                if (!inventory.CanAddItem(craftedItem, returnAmount))
                {
                    OnCraftRequestFinished?.Invoke(false, craftedItemId);
                    return;
                }

                RemoveCraftRequirementsLocally(craftedItem, recipeIndex);
                inventory.TryAddItem(craftedItem, returnAmount);

                UnlockCraftedItem(craftedItemId);
            }
        }

        OnCraftRequestFinished?.Invoke(succeeded, craftedItemId);
    }

    private void RemoveCraftRequirementsLocally(INV_Item craftedItem, int recipeIndex)
    {
        List<INV_Item.CraftingStack> recipeRequirements = craftedItem.GetRecipeRequirements(recipeIndex);
        if (recipeRequirements == null)
        {
            return;
        }

        for (int i = 0; i < recipeRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = recipeRequirements[i];
            if (req?.item == null || req.amount <= 0)
            {
                continue;
            }

            inventory.RemoveItemAmount(req.item.ItemID, req.amount);
        }
    }

    // public requests
    public void RequestDropItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        if (IsServer)
        {
            SpawnDroppedItem_Server(itemId);
            return;
        }

        RequestDropItemRpc(itemId);
    }

    public void RequestStoreItemInOpenChest(string inventoryItemUniqueId, string itemId, int quantity, Vector2Int chestCell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (string.IsNullOrWhiteSpace(inventoryItemUniqueId) || string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return;
        }

        CacheRefs();

        if (inventory == null || inventory.ActiveChest == null)
        {
            return;
        }

        NetworkObject chestNetObj = inventory.ActiveChest.NetworkObject;
        if (chestNetObj == null)
        {
            return;
        }

        if (IsServer)
        {
            StoreItemInChest_Server(inventory.ActiveChest, inventoryItemUniqueId, itemId, quantity, chestCell, rotation);
            return;
        }

        RequestStoreItemInChestRpc
        (
            new NetworkObjectReference(chestNetObj),
            inventoryItemUniqueId,
            itemId,
            quantity,
            chestCell.x,
            chestCell.y,
            (int)rotation
        );
    }

    public void RequestMoveChestItemInOpenChest(string chestItemUniqueId, Vector2Int chestCell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (string.IsNullOrWhiteSpace(chestItemUniqueId))
        {
            return;
        }

        CacheRefs();

        if (inventory == null || inventory.ActiveChest == null)
        {
            return;
        }

        NetworkObject chestNetObj = inventory.ActiveChest.NetworkObject;
        if (chestNetObj == null)
        {
            return;
        }

        if (IsServer)
        {
            MoveChestItem_Server(inventory.ActiveChest, chestItemUniqueId, chestCell, rotation);
            return;
        }

        RequestMoveChestItemRpc
        (
            new NetworkObjectReference(chestNetObj),
            chestItemUniqueId,
            chestCell.x,
            chestCell.y,
            (int)rotation
        );
    }

    public void RequestTakeChestItemFromOpenChest(string chestItemUniqueId, Vector2Int inventoryCell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (string.IsNullOrWhiteSpace(chestItemUniqueId))
        {
            return;
        }

        CacheRefs();

        if (inventory == null || inventory.ActiveChest == null)
        {
            return;
        }

        NetworkObject chestNetObj = inventory.ActiveChest.NetworkObject;
        if (chestNetObj == null)
        {
            return;
        }

        if (IsServer)
        {
            TakeChestItem_Server(inventory.ActiveChest, chestItemUniqueId, inventoryCell, rotation);
            return;
        }

        RequestTakeChestItemRpc
        (
            new NetworkObjectReference(chestNetObj),
            chestItemUniqueId,
            inventoryCell.x,
            inventoryCell.y,
            (int)rotation
        );
    }

    public void RequestTakeChestItemQuick(string chestItemUniqueId, bool takeStack)
    {
        if (string.IsNullOrWhiteSpace(chestItemUniqueId))
        {
            return;
        }

        CacheRefs();

        if (inventory == null || inventory.ActiveChest == null)
        {
            return;
        }

        NetworkObject chestNetObj = inventory.ActiveChest.NetworkObject;
        if (chestNetObj == null)
        {
            return;
        }

        if (IsServer)
        {
            TakeChestItemQuick_Server(inventory.ActiveChest, chestItemUniqueId, takeStack);
            return;
        }

        RequestTakeChestItemQuickRpc(new NetworkObjectReference(chestNetObj), chestItemUniqueId, takeStack);
    }

    // ask the server to equip an item.
    public void RequestEquipItem(string itemId, string inventoryItemUniqueId = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            RequestClearEquippedItem();
            return;
        }

        localEquippedInventoryItemUniqueId = inventoryItemUniqueId;

        ShowEquippedVisual(itemId);

        // fire local equip immediately for owner visual
        localVisualItemId = itemId;
        localEquipCallbackAlreadyFired = true;
        NotifyEquipped_Locally(itemId);

        if (IsServer)
        {
            EquipItem_Server(itemId);
            return;
        }

        RequestEquipItemRpc(itemId);
    }

    public void RequestClearEquippedItem()
    {
        if (!string.IsNullOrWhiteSpace(localVisualItemId) && equippedVisual != null)
        {
            NotifyUnequipped_Locally(localVisualItemId);
        }

        localEquippedInventoryItemUniqueId = null;
        localVisualItemId = null;
        localEquipCallbackAlreadyFired = false;

        ClearEquippedVisual();

        if (IsServer)
        {
            ClearEquippedItem_Server();
            return;
        }

        RequestClearEquippedItemRpc();
    }

    public void RequestUseEquippedItem()
    {
        if (!IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            UseEquippedItem_Server();
            return;
        }

        RequestUseEquippedItemRpc();
    }

    public void RequestHoldUseEquippedItem()
    {
        if (!IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            HoldUseEquippedItem_Server();
            return;
        }

        RequestHoldUseEquippedItemRpc();
    }

    public void RequestReleaseEquippedItem()
    {
        if (!IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            ReleaseUseEquippedItem_Server();
            return;
        }

        RequestReleaseUseEquippedItemRpc();
    }

    public void RequestAltUseEquippedItem()
    {
        if (!IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            AltUseEquippedItem_Server();
            return;
        }

        RequestAltUseEquippedItemRpc();
    }

    public void RequestHoldAltUseEquippedItem()
    {
        if (!IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            HoldAltUseEquippedItem_Server();
            return;
        }

        RequestHoldAltUseEquippedItemRpc();
    }

    public void RequestReleaseAltUseEquippedItem()
    {
        if (!IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            ReleaseAltUseEquippedItem_Server();
            return;
        }

        RequestReleaseAltUseEquippedItemRpc();
    }
    public void RequestUseEquippedItemLocally()
    {
        if (!IsOwner)
        {
            return;
        }

        UseEquippedItem_Locally();
    }

    public void RequestHoldUseEquippedItemLocally()
    {
        if (!IsOwner)
        {
            return;
        }

        HoldUseEquippedItem_Locally();
    }

    public void RequestReleaseEquippedItemLocally()
    {
        if (!IsOwner)
        {
            return;
        }

        ReleaseUseEquippedItem_Locally();
    }

    public void RequestAltUseEquippedItemLocally()
    {
        if (!IsOwner)
        {
            return;
        }

        AltUseEquippedItem_Locally();
    }

    public void RequestHoldAltUseEquippedItemLocally()
    {
        if (!IsOwner)
        {
            return;
        }

        HoldAltUseEquippedItem_Locally();
    }

    public void RequestReleaseAltUseEquippedItemLocally()
    {
        if (!IsOwner)
        {
            return;
        }

        ReleaseAltUseEquippedItem_Locally();
    }

    public void RequestUseItemInInventory(string inventoryItemUniqueId, string itemId)
    {
        if (!IsOwner || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        if (IsServer)
        {
            UseItemInInventory_Server(inventoryItemUniqueId, itemId);
            return;
        }

        RequestUseItemInInventoryRpc(inventoryItemUniqueId, itemId);
    }

    // ask the server to craft an item.
    public void RequestCraftItem(string itemId, int recipeIndex, NetworkObjectReference craftingStationRef = default)
    {
        if (!IsOwner || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        CacheRefs();

        // local validation first so clients can craft even if server-side inventory copy is not fully mirrored yet
        if (crafting != null && !crafting.CanCraftRecipeRightNow(item, recipeIndex))
        {
            return;
        }

        if (IsServer)
        {
            CraftItem_Server(itemId, recipeIndex, craftingStationRef);
            return;
        }

        RequestCraftItemRpc(itemId, recipeIndex, craftingStationRef);
    }

    // server rpc entry points
    [Rpc(SendTo.SpecifiedInParams)]
    public void TryAddWorldPickupLocalRpc(string itemId, NetworkObjectReference pickupRef, RpcParams rpcParams = default)
    {
        CacheRefs();

        bool added = false;

        if (inventory != null && !string.IsNullOrWhiteSpace(itemId))
        {
            INV_Item item = GetItemById(itemId);
            if (item != null)
            {
                added = inventory.TryAddItem(item);

                if (added)
                {
                    UnlockItemLocally(itemId);
                }
            }
        }

        ConfirmWorldPickupAddResultRpc(pickupRef, added);
    }

    [Rpc(SendTo.Server)]
    private void ConfirmWorldPickupAddResultRpc(NetworkObjectReference pickupRef, bool added, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams)) { return; }
        if (!added) { return; }
        if (!pickupRef.TryGet(out NetworkObject pickupNetObj) || pickupNetObj == null) { return; }
        if (!pickupNetObj.IsSpawned) { return; }

        SP_SpaceJunk junk = pickupNetObj.GetComponent<SP_SpaceJunk>();
        if (junk != null) { junk.RemoveFromSpaceManager(); }

        pickupNetObj.Despawn(true);
    }

    [Rpc(SendTo.Server)]
    private void RequestDropItemRpc(string itemId, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams) || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SpawnDroppedItem_Server(itemId);
    }

    [Rpc(SendTo.Server)]
    private void RequestStoreItemInChestRpc(NetworkObjectReference chestRef, string inventoryItemUniqueId, string itemId, int quantity, int cellX, int cellY, int rotation, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        if (!TryGetChestFromRef(chestRef, out INV_Chest chest))
        {
            return;
        }

        StoreItemInChest_Server
        (
            chest,
            inventoryItemUniqueId,
            itemId,
            quantity,
            new Vector2Int(cellX, cellY),
            (INV_Inventory.ItemInstance.Rotation)rotation
        );
    }

    [Rpc(SendTo.Server)]
    private void RequestMoveChestItemRpc(NetworkObjectReference chestRef, string chestItemUniqueId, int cellX, int cellY, int rotation, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        if (!TryGetChestFromRef(chestRef, out INV_Chest chest))
        {
            return;
        }

        MoveChestItem_Server(chest, chestItemUniqueId, new Vector2Int(cellX, cellY), (INV_Inventory.ItemInstance.Rotation)rotation);
    }

    [Rpc(SendTo.Server)]
    private void RequestTakeChestItemRpc(NetworkObjectReference chestRef, string chestItemUniqueId, int cellX, int cellY, int rotation, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        if (!TryGetChestFromRef(chestRef, out INV_Chest chest))
        {
            return;
        }

        TakeChestItem_Server(chest, chestItemUniqueId, new Vector2Int(cellX, cellY), (INV_Inventory.ItemInstance.Rotation)rotation);
    }

    [Rpc(SendTo.Server)]
    private void RequestTakeChestItemQuickRpc(NetworkObjectReference chestRef, string chestItemUniqueId, bool takeStack, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        if (!TryGetChestFromRef(chestRef, out INV_Chest chest))
        {
            return;
        }

        TakeChestItemQuick_Server(chest, chestItemUniqueId, takeStack);
    }

    [Rpc(SendTo.Server)]
    private void RequestEquipItemRpc(string itemId, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams) || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        EquipItem_Server(itemId);
    }

    [Rpc(SendTo.Server)]
    private void RequestClearEquippedItemRpc(RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        ClearEquippedItem_Server();
    }

    [Rpc(SendTo.Server)]
    private void RequestUseEquippedItemRpc(RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        UseEquippedItem_Server();
    }

    [Rpc(SendTo.Server)]
    private void RequestHoldUseEquippedItemRpc(RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        HoldUseEquippedItem_Server();
    }

    [Rpc(SendTo.Server)]
    private void RequestReleaseUseEquippedItemRpc(RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        ReleaseUseEquippedItem_Server();
    }

    [Rpc(SendTo.Server)]
    private void RequestAltUseEquippedItemRpc(RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        AltUseEquippedItem_Server();
    }

    [Rpc(SendTo.Server)]
    private void RequestHoldAltUseEquippedItemRpc(RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        HoldAltUseEquippedItem_Server();
    }

    [Rpc(SendTo.Server)]
    private void RequestReleaseAltUseEquippedItemRpc(RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        ReleaseAltUseEquippedItem_Server();
    }

    [Rpc(SendTo.Server)]
    private void RequestUseItemInInventoryRpc(string inventoryItemUniqueId, string itemId, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        UseItemInInventory_Server(inventoryItemUniqueId, itemId);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void UseConsumableLocalRpc(string inventoryItemUniqueId, string itemId, RpcParams rpcParams = default)
    {
        CacheRefs();

        if (inventory == null)
        {
            return;
        }

        bool removed = false;

        if (!string.IsNullOrWhiteSpace(inventoryItemUniqueId))
        {
            removed = inventory.TryUseLocalConsumableByUniqueId(inventoryItemUniqueId);
        }

        if (!removed && !string.IsNullOrWhiteSpace(itemId))
        {
            inventory.TryUseLocalConsumableByItemId(itemId);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestCraftItemRpc(string itemId, int recipeIndex, NetworkObjectReference craftingStationRef, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams) || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        INV_Item craftedItem = GetItemById(itemId);
        if (craftedItem == null)
        {
            NotifyCraftResult(false, itemId, recipeIndex);
            return;
        }

        // if the server-side inventory copy is not in sync for this remote client yet,
        // trust the request path and let the owner apply the result locally through CraftResultLocalRpc.
        if (!IsOwner)
        {
            TryPlayCraftingStationEffects_Server(craftingStationRef);
            NotifyCraftResult(true, itemId, recipeIndex);
            return;
        }

        CraftItem_Server(itemId, recipeIndex, craftingStationRef);
    }

    private bool IsSenderOwner(RpcParams rpcParams)
    {
        return OwnerClientId == rpcParams.Receive.SenderClientId;
    }

    private bool TryGetChestFromRef(NetworkObjectReference chestRef, out INV_Chest chest)
    {
        chest = null;

        if (!chestRef.TryGet(out NetworkObject chestNetObj) || chestNetObj == null)
        {
            return false;
        }

        chest = chestNetObj.GetComponent<INV_Chest>();
        return chest != null;
    }

    // resolve an item id into the matching item asset.
    private INV_Item GetItemById(string itemId)
    {
        return INV_ItemDatabase.Instance != null ? INV_ItemDatabase.Instance.GetItemById(itemId) : null;
    }

    private void UnlockCraftedItem(string craftedItemId)
    {
        UnlockItemLocally(craftedItemId);
    }

    private void UnlockItemLocally(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || INV_ItemDatabase.Instance == null)
        {
            return;
        }

        INV_Item item = INV_ItemDatabase.Instance.GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        item.UnlockItem();

        if (crafting != null)
        {
            crafting.RebuildCraftingView();
        }
    }

    // equipped item
    private void EquipItem_Server(string itemId)
    {
        if (!IsServer)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null || item.EquippedPrefab == null)
        {
            return;
        }

        string oldItemId = equippedItemId.Value.ToString();

        // if something is already equipped, notify before swapping
        if (!string.IsNullOrWhiteSpace(oldItemId) && equippedVisual != null)
        {
            NotifyUnequipped_Server(oldItemId);

            if (IsOwner)
            {
                NotifyUnequipped_Locally(oldItemId);
            }
        }

        equippedItemId.Value = itemId;

        if (logEquippedItem)
        {
            Debug.Log($"Equipped item set on server: {item.Name}", this);
        }
    }

    private void ClearEquippedItem_Server()
    {
        if (!IsServer)
        {
            return;
        }

        string oldItemId = equippedItemId.Value.ToString();

        if (!string.IsNullOrWhiteSpace(oldItemId) && equippedVisual != null)
        {
            NotifyUnequipped_Server(oldItemId);

            if (IsOwner)
            {
                NotifyUnequipped_Locally(oldItemId);
            }
        }

        equippedItemId.Value = default;

        if (logEquippedItem)
        {
            Debug.Log("Cleared equipped item on server.", this);
        }
    }

    private void NotifyEquipped_Server(string itemId)
    {
        if (!IsServer) { return; }
        if (string.IsNullOrWhiteSpace(itemId)) { return; }
        if (equippedVisual == null) { return; }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnEquipped(gameObject.GetComponent<NetworkObject>());
        }
    }

    private void NotifyUnequipped_Server(string itemId)
    {
        if (!IsServer) { return; }
        if (string.IsNullOrWhiteSpace(itemId)) { return; }
        if (equippedVisual == null) { return; }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnUnequipped(gameObject.GetComponent<NetworkObject>());
        }
    }

    private void NotifyEquipped_Locally(string itemId)
    {
        if (!IsOwner) { return; }
        if (string.IsNullOrWhiteSpace(itemId)) { return; }
        if (equippedVisual == null) { return; }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnEquippedLocally(gameObject.GetComponent<NetworkObject>());
        }
    }

    private void NotifyUnequipped_Locally(string itemId)
    {
        if (!IsOwner) { return; }
        if (string.IsNullOrWhiteSpace(itemId)) { return; }
        if (equippedVisual == null) { return; }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnUnequippedLocally(gameObject.GetComponent<NetworkObject>());
        }
    }

    private void UseEquippedItem_Server()
    {
        if (!IsServer)
        {
            return;
        }

        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        // consumables should probably be done here so they use the same networked path as inventory
        if (item.ItemTypeVal == INV_Item.ItemType.Consumable)
        {
            UseItemInInventory_Server(localEquippedInventoryItemUniqueId, itemId);
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnUse(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components.", equippedVisual);
        }
    }

    private void HoldUseEquippedItem_Server()
    {
        if (!IsServer)
        {
            return;
        }

        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnUseHeld(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for hold use.", equippedVisual);
        }
    }

    private void ReleaseUseEquippedItem_Server()
    {
        if (!IsServer)
        {
            return;
        }

        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnUseReleased(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for release use.", equippedVisual);
        }
    }

    private void AltUseEquippedItem_Server()
    {
        if (!IsServer)
        {
            return;
        }

        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnAltUse(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for alt use.", equippedVisual);
        }
    }

    private void HoldAltUseEquippedItem_Server()
    {
        if (!IsServer)
        {
            return;
        }

        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnAltUseHeld(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for alt hold use.", equippedVisual);
        }
    }

    private void ReleaseAltUseEquippedItem_Server()
    {
        if (!IsServer)
        {
            return;
        }

        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnAltUseReleased(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for alt release use.", equippedVisual);
        }
    }

    private void UseEquippedItem_Locally()
    {
        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnUseLocally(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for local use.", equippedVisual);
        }
    }

    private void HoldUseEquippedItem_Locally()
    {
        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnUseHeldLocally(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for local hold use.", equippedVisual);
        }
    }

    private void ReleaseUseEquippedItem_Locally()
    {
        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnUseReleasedLocally(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for local release use.", equippedVisual);
        }
    }

    private void AltUseEquippedItem_Locally()
    {
        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnAltUseLocally(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for local alt use.", equippedVisual);
        }
    }

    private void HoldAltUseEquippedItem_Locally()
    {
        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnAltUseHeldLocally(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for local alt hold use.", equippedVisual);
        }
    }

    private void ReleaseAltUseEquippedItem_Locally()
    {
        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || equippedVisual == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = equippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not CC_INV_UsableItems usableItem)
            {
                continue;
            }

            usableItem.SendItemId(itemId);
            usableItem.SendSender(gameObject.GetComponent<NetworkObject>());
            usableItem.OnAltUseReleasedLocally(gameObject.GetComponent<NetworkObject>());
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no CC_INV_UsableItems components for local alt release use.", equippedVisual);
        }
    }

    // inventory item use / crafting
    private void UseItemInInventory_Server(string inventoryItemUniqueId, string itemId)
    {
        if (!IsServer)
        {
            return;
        }

        INV_Item usedItem = GetItemById(itemId);
        if (usedItem == null || usedItem.ItemTypeVal != INV_Item.ItemType.Consumable)
        {
            return;
        }

        CacheRefs();

        if (inventory == null)
        {
            return;
        }

        // host uses the real inventory instance on server
        if (IsOwner)
        {
            bool removedHost = false;

            if (!string.IsNullOrWhiteSpace(inventoryItemUniqueId))
            {
                removedHost = inventory.RemovePlayerItemByUniqueId(inventoryItemUniqueId, 1);
            }

            if (!removedHost)
            {
                removedHost = inventory.RemoveItemAmount(itemId, 1);
            }

            if (!removedHost)
            {
                return;
            }
        }
        else
        {
            // remote owner inventory is local only, tell the owner to remove 1 item
            UseConsumableLocalRpc
            (
                inventoryItemUniqueId,
                itemId,
                RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp)
            );
        }

        ApplyConsumableEffects_Server(usedItem);

        // if the equipped item no longer exists locally after use, clear equipped state too
        if (!string.IsNullOrWhiteSpace(localEquippedInventoryItemUniqueId) &&
            inventoryItemUniqueId == localEquippedInventoryItemUniqueId)
        {
            if (IsOwner && !inventory.HasPlayerItemWithUniqueId(localEquippedInventoryItemUniqueId))
            {
                RequestClearEquippedItem();
            }
        }
    }

    private void ApplyConsumableEffects_Server(INV_Item usedItem)
    {
        if (usedItem == null || characterValues == null)
        {
            return;
        }

        characterValues.AddHungerDelay(usedItem.HungerDrainDelay);
        characterValues.AddHunger(usedItem.HungerReplenish);
        characterValues.AddThirstDelay(usedItem.ThirstDrainDelay);
        characterValues.AddThirst(usedItem.ThirstReplenish);
    }

    private void CraftItem_Server(string itemId, int recipeIndex, NetworkObjectReference craftingStationRef = default)
    {
        if (!IsServer)
        {
            return;
        }

        CacheRefs();

        if (inventory == null)
        {
            NotifyCraftResult(false, itemId, recipeIndex);
            return;
        }

        INV_Item craftedItem = GetItemById(itemId);
        if (craftedItem == null || !craftedItem.Craftable)
        {
            NotifyCraftResult(false, itemId, recipeIndex);
            return;
        }

        List<INV_Item.CraftingStack> recipeRequirements = craftedItem.GetRecipeRequirements(recipeIndex);
        if (recipeRequirements == null || recipeRequirements.Count == 0)
        {
            NotifyCraftResult(false, itemId, recipeIndex);
            return;
        }

        if (!HasAllCraftRequirements(recipeRequirements))
        {
            NotifyCraftResult(false, itemId, recipeIndex);
            return;
        }

        int returnAmount = craftedItem.GetRecipeReturnAmount(recipeIndex);

        if (!inventory.CanAddItem(craftedItem, returnAmount))
        {
            NotifyCraftResult(false, itemId, recipeIndex);
            return;
        }

        if (!RemoveCraftRequirementsServer(recipeRequirements))
        {
            NotifyCraftResult(false, itemId, recipeIndex);
            return;
        }

        if (inventory.TryAddItem(craftedItem, returnAmount))
        {
            TryPlayCraftingStationEffects_Server(craftingStationRef);
            NotifyCraftResult(true, itemId, recipeIndex);
            return;
        }

        RestoreCraftRequirements(recipeRequirements);
        NotifyCraftResult(false, itemId, recipeIndex);
    }

    private bool HasAllCraftRequirements(List<INV_Item.CraftingStack> recipeRequirements)
    {
        if (recipeRequirements == null)
        {
            return false;
        }

        for (int i = 0; i < recipeRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = recipeRequirements[i];
            if (req?.item == null || req.amount <= 0)
            {
                continue;
            }

            if (!inventory.HasItemAmount(req.item.ItemID, req.amount))
            {
                return false;
            }
        }

        return true;
    }

    private bool RemoveCraftRequirementsServer(List<INV_Item.CraftingStack> recipeRequirements)
    {
        if (recipeRequirements == null)
        {
            return false;
        }

        for (int i = 0; i < recipeRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = recipeRequirements[i];
            if (req?.item == null || req.amount <= 0)
            {
                continue;
            }

            if (inventory.RemoveItemAmount(req.item.ItemID, req.amount))
            {
                continue;
            }

            RestoreCraftRequirementsUpToIndex(recipeRequirements, i);
            return false;
        }

        return true;
    }

    private void RestoreCraftRequirementsUpToIndex(List<INV_Item.CraftingStack> recipeRequirements, int endExclusive)
    {
        if (recipeRequirements == null)
        {
            return;
        }

        for (int r = 0; r < endExclusive; r++)
        {
            INV_Item.CraftingStack restoreReq = recipeRequirements[r];
            if (restoreReq?.item == null || restoreReq.amount <= 0)
            {
                continue;
            }

            for (int a = 0; a < restoreReq.amount; a++)
            {
                inventory.TryAddItem(restoreReq.item);
            }
        }
    }

    private void RestoreCraftRequirements(List<INV_Item.CraftingStack> recipeRequirements)
    {
        if (recipeRequirements == null)
        {
            return;
        }

        for (int i = 0; i < recipeRequirements.Count; i++)
        {
            INV_Item.CraftingStack restoreReq = recipeRequirements[i];
            if (restoreReq?.item == null || restoreReq.amount <= 0)
            {
                continue;
            }

            for (int a = 0; a < restoreReq.amount; a++)
            {
                inventory.TryAddItem(restoreReq.item);
            }
        }
    }

    private void NotifyCraftResult(bool succeeded, string craftedItemId, int recipeIndex)
    {
        if (IsOwner)
        {
            if (succeeded)
            {
                UnlockCraftedItem(craftedItemId);
            }

            OnCraftRequestFinished?.Invoke(succeeded, craftedItemId);
            return;
        }

        CraftResultLocalRpc(succeeded, craftedItemId, recipeIndex, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
    }

    private void TryPlayCraftingStationEffects_Server(NetworkObjectReference craftingStationRef)
    {
        if (!IsServer)
        {
            return;
        }

        if (!craftingStationRef.TryGet(out NetworkObject stationNetObj) || stationNetObj == null)
        {
            return;
        }

        INV_CraftingInteract craftingStation = stationNetObj.GetComponent<INV_CraftingInteract>();
        if (craftingStation == null)
        {
            return;
        }

        craftingStation.OnCraft();
    }

    // chest
    private void StoreItemInChest_Server(INV_Chest chest, string inventoryItemUniqueId, string itemId, int quantity, Vector2Int chestCell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (!IsServer || chest == null || string.IsNullOrWhiteSpace(inventoryItemUniqueId) || string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return;
        }

        CacheRefs();

        if (inventory == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null)
        {
            chest.RefreshViewer();
            return;
        }

        // host player uses the real shared inventory instance on server
        if (IsOwner)
        {
            if (!chest.TryStoreItemData(itemId, quantity, chestCell, rotation))
            {
                chest.RefreshViewer();
                return;
            }

            if (inventory.RemovePlayerItemByUniqueId(inventoryItemUniqueId, quantity))
            {
                chest.RefreshViewer();
                return;
            }

            chest.TryRemoveLastStoredItem();
            chest.RefreshViewer();
            return;
        }

        // remote client inventory is handled locally on the owner,
        // server only mutates chest data and refreshes the viewer snapshot
        if (!chest.TryStoreItemData(itemId, quantity, chestCell, rotation))
        {
            chest.RefreshViewer();
            return;
        }

        chest.RefreshViewer();
    }

    private void MoveChestItem_Server(INV_Chest chest, string chestItemUniqueId, Vector2Int chestCell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (!IsServer || chest == null || string.IsNullOrWhiteSpace(chestItemUniqueId))
        {
            return;
        }

        chest.TryMoveItemData(chestItemUniqueId, chestCell, rotation);
        chest.RefreshViewer();
    }

    private void TakeChestItem_Server(INV_Chest chest, string chestItemUniqueId, Vector2Int inventoryCell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (!IsServer || chest == null || string.IsNullOrWhiteSpace(chestItemUniqueId))
        {
            return;
        }

        CacheRefs();

        if (inventory == null)
        {
            return;
        }

        INV_Chest.ChestItemData data;
        if (!chest.TryGetItemData(chestItemUniqueId, out data))
        {
            chest.RefreshViewer();
            return;
        }

        INV_Item item = GetItemById(data.itemId);
        if (item == null)
        {
            chest.RefreshViewer();
            return;
        }

        int amount = Mathf.Max(1, data.quantity);

        // host player uses the real shared inventory instance on server
        if (IsOwner)
        {
            bool added = inventory.TryAddItemAtCell(item, inventoryCell, rotation, amount);
            if (!added)
            {
                chest.RefreshViewer();
                return;
            }

            if (!chest.TryRemoveItem(chestItemUniqueId))
            {
                chest.RefreshViewer();
                return;
            }

            chest.RefreshViewer();
            return;
        }

        // remote client inventory is handled locally on the owner,
        // server only removes from chest and tells the owner to add it locally at exact cell
        if (!chest.TryRemoveItem(chestItemUniqueId))
        {
            chest.RefreshViewer();
            return;
        }

        AddItemAtCellLocalRpc
        (
            item.ItemID,
            amount,
            inventoryCell.x,
            inventoryCell.y,
            (int)rotation,
            RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp)
        );

        chest.RefreshViewer();
    }

    private void TakeChestItemQuick_Server(INV_Chest chest, string chestItemUniqueId, bool takeStack)
    {
        if (!IsServer || chest == null || string.IsNullOrWhiteSpace(chestItemUniqueId))
        {
            return;
        }

        CacheRefs();

        if (inventory == null)
        {
            return;
        }

        INV_Chest.ChestItemData data;
        if (!chest.TryGetItemData(chestItemUniqueId, out data))
        {
            chest.RefreshViewer();
            return;
        }

        INV_Item item = GetItemById(data.itemId);
        if (item == null)
        {
            chest.RefreshViewer();
            return;
        }

        int chestAmount = Mathf.Max(1, data.quantity);
        int wantedAmount = takeStack ? chestAmount : 1;
        int addedAmount = 0;

        // host uses server
        if (IsOwner)
        {
            for (int i = 0; i < wantedAmount; i++) // try add on each item rather than new logic
            {
                if (!inventory.TryAddItem(item))
                {
                    break;
                }

                addedAmount++;
            }

            if (addedAmount <= 0)
            {
                chest.RefreshViewer();
                return;
            }

            int remaining = chestAmount - addedAmount;

            if (remaining <= 0)
            {
                chest.TryRemoveItem(chestItemUniqueId);
            }
            else
            {
                chest.TrySetItemQuantity(chestItemUniqueId, remaining);
            }

            chest.RefreshViewer();
            return;
        }

        // remote client path
        for (int i = 0; i < wantedAmount; i++)
        {
            addedAmount++;
        }

        if (addedAmount <= 0)
        {
            chest.RefreshViewer();
            return;
        }

        int remainingRemote = chestAmount - addedAmount;

        if (remainingRemote <= 0)
        {
            chest.TryRemoveItem(chestItemUniqueId);
        }
        else
        {
            chest.TrySetItemQuantity(chestItemUniqueId, remainingRemote);
        }

        AddItemAmountLocalRpc(item.ItemID, addedAmount, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        chest.RefreshViewer();
    }

    // world drop
    private void SpawnDroppedItem_Server(string itemId)
    {
        if (!IsServer)
        {
            return;
        }

        CacheRefs();

        if (inventory == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null || inventory.ItemPrefab == null || inventory.dropItemTransform == null)
        {
            return;
        }

        GameObject drop = CreateDropObject();
        if (drop == null)
        {
            return;
        }

        if (!SetupDropObject(drop, item))
        {
            Destroy(drop);
            return;
        }

        ApplyDropPhysics(drop);
    }

    private GameObject CreateDropObject()
    {
        Vector3 dropPoint = inventory.dropItemTransform.position;
        Quaternion dropRotation = inventory.dropItemTransform.rotation;
        return Instantiate(inventory.ItemPrefab, dropPoint, dropRotation);
    }

    private bool SetupDropObject(GameObject drop, INV_Item item)
    {
        INV_ItemDrop dropHandler = drop.GetComponent<INV_ItemDrop>();
        if (dropHandler != null)
        {
            dropHandler.Init(item);

            InteractableObject interactable = dropHandler.GetComponent<InteractableObject>();
            if (interactable != null)
            {
                interactable.RewireInteractListeners();
            }
        }

        ApplyDropScale(drop, item);

        NetworkObject netObj = drop.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            return false;
        }

        netObj.Spawn();
        return true;
    }

    private void ApplyDropScale(GameObject drop, INV_Item item)
    {
        if (drop == null || item == null)
        {
            return;
        }

        float scale = item.DropMeshScale;
        if (scale <= 0f)
        {
            scale = 0f;
        }

        drop.transform.localScale = Vector3.one * scale;
    }

    private void ApplyDropPhysics(GameObject drop)
    {
        Rigidbody rb = drop.GetComponent<Rigidbody>();
        if (rb == null)
        {
            return;
        }

        rb.WakeUp();
        rb.linearVelocity = inventory.dropItemTransform.forward * testThrowPower;
        rb.angularVelocity = Vector3.one * UnityEngine.Random.Range(-testThrowTorque, testThrowTorque);
    }

    // equipped visuals
    private void RebuildEquippedVisuals(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            localVisualItemId = null;
            localEquipCallbackAlreadyFired = false;
            ClearEquippedVisual();
            return;
        }

        ShowEquippedVisual(itemId);

        if (IsServer)
        {
            NotifyEquipped_Server(itemId);
        }

        if (IsOwner)
        {
            // if we already fired local equip when the owner preview visual was shown,
            // dont fire it again for the same item rebuild
            if (!localEquipCallbackAlreadyFired || localVisualItemId != itemId)
            {
                NotifyEquipped_Locally(itemId);
            }

            localVisualItemId = itemId;
            localEquipCallbackAlreadyFired = true;
        }
    }

    private void ShowEquippedVisual(string itemId)
    {
        Transform followRoot = GetEquippedItemRootForThisInstance();
        if (followRoot == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null || item.EquippedPrefab == null)
        {
            return;
        }

        ClearEquippedVisual();

        string objectName = IsOwner ? $"Equipped_{item.Name}_Owner" : $"Equipped_{item.Name}_Observer";
        equippedVisual = InstantiateEquippedVisual(item, followRoot, objectName);
        if (equippedVisual == null)
        {
            return;
        }

        RemoveNetworkObjectIfPresent(equippedVisual);

        CC_INV_EquippedItem equippedItem = equippedVisual.GetComponent<CC_INV_EquippedItem>();
        if (equippedItem != null)
        {
            equippedItem.Init(item, IsOwner);
            equippedItem.SetVisualVisible(true);
        }

        equippedVisual.SetActive(true);
    }

    private GameObject InstantiateEquippedVisual(INV_Item item, Transform parent, string objectName)
    {
        GameObject go = Instantiate(item.EquippedPrefab, parent);
        go.name = objectName;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    private void RemoveNetworkObjectIfPresent(GameObject target)
    {
        NetworkObject netObj = target.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Destroy(netObj);
        }
    }

    private void ClearEquippedVisual()
    {
        if (equippedVisual != null)
        {
            Destroy(equippedVisual);
        }

        equippedVisual = null;
    }

    private Transform GetEquippedItemRootForThisInstance()
    {
        if (IsOwner && equippedItemRoot != null)
        {
            return equippedItemRoot;
        }

        if (observerEquippedItemRoot != null)
        {
            return observerEquippedItemRoot;
        }

        CC_CameraController cameraController = GetComponentInParent<CC_CameraController>();
        if (cameraController != null && cameraController.ReplicatedCameraDirectionRoot != null)
        {
            observerEquippedItemRoot = cameraController.ReplicatedCameraDirectionRoot;
            return observerEquippedItemRoot;
        }

        return transform;
    }

    public string GetEquippedItemId()
    {
        return equippedItemId.Value.ToString();
    }

    public GameObject GetEquippedVisual()
    {
        return equippedVisual;
    }
}