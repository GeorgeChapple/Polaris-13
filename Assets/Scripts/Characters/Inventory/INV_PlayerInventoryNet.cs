using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

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

    [Tooltip("Object root for the replicated equipped item for other players. If null, uses replicated camera direction root, then this objects transform.")]
    [SerializeField] private Transform replicatedEquippedItemRoot;

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

    // local owner only equipped visual
    private GameObject localEquippedVisual;

    // replicated visual built locally on non owners
    // also used as the server authority equipped object on the server
    private GameObject replicatedEquippedVisual;

    private void Awake()
    {
        CacheRefs();
        CacheReplicatedEquippedRoot();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        equippedItemId.OnValueChanged += OnEquippedItemIdChanged;
        RebuildEquippedVisuals(equippedItemId.Value.ToString());
    }

    public override void OnNetworkDespawn()
    {
        equippedItemId.OnValueChanged -= OnEquippedItemIdChanged;

        ClearLocalEquippedVisual();
        ClearReplicatedEquippedVisual();

        base.OnNetworkDespawn();
    }

    private void LateUpdate()
    {
        FollowLocalEquippedRoot();
        FollowReplicatedEquippedRoot();
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

    private void CacheReplicatedEquippedRoot()
    {
        if (replicatedEquippedItemRoot != null)
        {
            return;
        }

        CC_CameraController cameraController = GetComponentInParent<CC_CameraController>();
        if (cameraController != null)
        {
            replicatedEquippedItemRoot = cameraController.ReplicatedCameraDirectionRoot;
        }
    }

    private void FollowLocalEquippedRoot()
    {
        if (localEquippedVisual == null || equippedItemRoot == null)
        {
            return;
        }

        localEquippedVisual.transform.position = equippedItemRoot.position;
        localEquippedVisual.transform.rotation = equippedItemRoot.rotation;
        localEquippedVisual.transform.localScale = Vector3.one;
    }

    private void FollowReplicatedEquippedRoot()
    {
        if (replicatedEquippedVisual == null)
        {
            return;
        }

        Transform followRoot = GetReplicatedEquippedItemRoot();
        if (followRoot == null)
        {
            return;
        }

        replicatedEquippedVisual.transform.position = followRoot.position;
        replicatedEquippedVisual.transform.rotation = followRoot.rotation;
        replicatedEquippedVisual.transform.localScale = Vector3.one;
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

        inventory.TryAddItem(item);
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

        inventory.TryAddItemAtCell
        (
            item,
            new Vector2Int(cellX, cellY),
            (INV_Inventory.ItemInstance.Rotation)rotation,
            amount
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void CraftResultLocalRpc(bool succeeded, string craftedItemId, RpcParams rpcParams = default)
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
                RemoveCraftRequirementsLocally(craftedItem);
                inventory.TryAddItem(craftedItem);
            }
        }

        OnCraftRequestFinished?.Invoke(succeeded, craftedItemId);
    }

    private void RemoveCraftRequirementsLocally(INV_Item craftedItem)
    {
        for (int i = 0; i < craftedItem.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = craftedItem.CraftingRequirements[i];
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

    public void RequestStoreItemInOpenChest(string itemId, int quantity, Vector2Int chestCell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
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
            StoreItemInChest_Server(inventory.ActiveChest, itemId, quantity, chestCell, rotation);
            return;
        }

        RequestStoreItemInChestRpc
        (
            new NetworkObjectReference(chestNetObj),
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
    public void RequestEquipItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            RequestClearEquippedItem();
            return;
        }

        if (IsOwner)
        {
            ShowLocalEquippedVisual(itemId);
        }

        if (IsServer)
        {
            EquipItem_Server(itemId);
            return;
        }

        RequestEquipItemRpc(itemId);
    }

    public void RequestClearEquippedItem()
    {
        if (IsOwner)
        {
            ClearLocalEquippedVisual();
        }

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

    public void RequestUseItemInInventory(string itemId)
    {
        if (!IsOwner || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        if (IsServer)
        {
            UseItemInInventory_Server(itemId);
            return;
        }

        RequestUseItemInInventoryRpc(itemId);
    }

    public void RequestCraftItem(string itemId)
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
        if (crafting != null && !crafting.CanCraftItemRightNow(item))
        {
            return;
        }

        if (IsServer)
        {
            CraftItem_Server(itemId);
            return;
        }

        RequestCraftItemRpc(itemId);
    }

    // server rpc entry points
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
    private void RequestStoreItemInChestRpc(NetworkObjectReference chestRef, string itemId, int quantity, int cellX, int cellY, int rotation, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        if (!TryGetChestFromRef(chestRef, out INV_Chest chest))
        {
            return;
        }

        StoreItemInChest_Server(chest, itemId, quantity, new Vector2Int(cellX, cellY), (INV_Inventory.ItemInstance.Rotation)rotation);
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
    private void RequestUseItemInInventoryRpc(string itemId, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams))
        {
            return;
        }

        UseItemInInventory_Server(itemId);
    }

    [Rpc(SendTo.Server)]
    private void RequestCraftItemRpc(string itemId, RpcParams rpcParams = default)
    {
        if (!IsSenderOwner(rpcParams) || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        INV_Item craftedItem = GetItemById(itemId);
        if (craftedItem == null)
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        // if the server-side inventory copy is not in sync for this remote client yet,
        // trust the request path and let the owner apply the result locally through CraftResultLocalRpc.
        if (!IsOwner)
        {
            NotifyCraftResult(true, itemId);
            return;
        }

        CraftItem_Server(itemId);
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

    private INV_Item GetItemById(string itemId)
    {
        return INV_ItemDatabase.Instance != null ? INV_ItemDatabase.Instance.GetItemById(itemId) : null;
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

        equippedItemId.Value = default;

        if (logEquippedItem)
        {
            Debug.Log("Cleared equipped item on server.", this);
        }
    }

    private void UseEquippedItem_Server()
    {
        if (!IsServer)
        {
            return;
        }

        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId) || replicatedEquippedVisual == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = replicatedEquippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IUsableItem usableItem)
            {
                continue;
            }

            usableItem.WireUp(gameObject, itemId);
            usableItem.OnUse();
            foundUsable = true;
        }

        if (!foundUsable && logEquippedItem)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no IUsableItem components.", replicatedEquippedVisual);
        }
    }

    // inventory item use / crafting
    private void UseItemInInventory_Server(string itemId)
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

        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (inventory == null)
        {
            return;
        }

        bool removed = inventory.RemoveItemAmount(itemId, 1);
        if (!removed)
        {
            return;
        }

        if (characterValues != null)
        {
            characterValues.AddHungerDelay(usedItem.HungerDrainDelay);
            characterValues.AddHunger(usedItem.HungerReplenish);
            characterValues.AddThirstDelay(usedItem.ThirstDrainDelay);
            characterValues.AddThirst(usedItem.ThirstReplenish);
        }
    }

    private void CraftItem_Server(string itemId)
    {
        if (!IsServer)
        {
            return;
        }

        CacheRefs();

        if (inventory == null)
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        INV_Item craftedItem = GetItemById(itemId);
        if (craftedItem == null || !craftedItem.Craftable)
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        if (!HasAllCraftRequirements(craftedItem))
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        if (!inventory.CanAddItem(craftedItem))
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        if (!RemoveCraftRequirementsServer(craftedItem))
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        if (inventory.TryAddItem(craftedItem))
        {
            NotifyCraftResult(true, itemId);
            return;
        }

        RestoreCraftRequirements(craftedItem);
        NotifyCraftResult(false, itemId);
    }

    private bool HasAllCraftRequirements(INV_Item craftedItem)
    {
        for (int i = 0; i < craftedItem.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = craftedItem.CraftingRequirements[i];
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

    private bool RemoveCraftRequirementsServer(INV_Item craftedItem)
    {
        for (int i = 0; i < craftedItem.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = craftedItem.CraftingRequirements[i];
            if (req?.item == null || req.amount <= 0)
            {
                continue;
            }

            if (inventory.RemoveItemAmount(req.item.ItemID, req.amount))
            {
                continue;
            }

            RestoreCraftRequirementsUpToIndex(craftedItem, i);
            return false;
        }

        return true;
    }

    private void RestoreCraftRequirementsUpToIndex(INV_Item craftedItem, int endExclusive)
    {
        for (int r = 0; r < endExclusive; r++)
        {
            INV_Item.CraftingStack restoreReq = craftedItem.CraftingRequirements[r];
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

    private void RestoreCraftRequirements(INV_Item craftedItem)
    {
        for (int i = 0; i < craftedItem.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack restoreReq = craftedItem.CraftingRequirements[i];
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

    private void NotifyCraftResult(bool succeeded, string craftedItemId)
    {
        if (IsOwner)
        {
            OnCraftRequestFinished?.Invoke(succeeded, craftedItemId);
            return;
        }

        CraftResultLocalRpc(succeeded, craftedItemId, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
    }

    // chest
    private void StoreItemInChest_Server(INV_Chest chest, string itemId, int quantity, Vector2Int chestCell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (!IsServer || chest == null || string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
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
            if (!inventory.HasItemAmount(itemId, quantity))
            {
                chest.RefreshViewer();
                return;
            }

            if (!chest.TryStoreItemData(itemId, quantity, chestCell, rotation))
            {
                chest.RefreshViewer();
                return;
            }

            if (inventory.RemoveItemAmount(itemId, quantity))
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

        NetworkObject netObj = drop.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            return false;
        }

        netObj.Spawn();
        return true;
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
        RebuildOwnerEquippedVisual(itemId);
        RebuildReplicatedEquippedVisual(itemId);
    }

    private void RebuildOwnerEquippedVisual(string itemId)
    {
        if (!IsOwner)
        {
            ClearLocalEquippedVisual();
            return;
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            ClearLocalEquippedVisual();
            return;
        }

        ShowLocalEquippedVisual(itemId);
    }

    private void RebuildReplicatedEquippedVisual(string itemId)
    {
        bool shouldHaveReplicatedVisual = IsServer || !IsOwner;
        if (!shouldHaveReplicatedVisual)
        {
            ClearReplicatedEquippedVisual();
            return;
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            ClearReplicatedEquippedVisual();
            return;
        }

        ShowReplicatedEquippedVisual(itemId);
    }

    private void ShowLocalEquippedVisual(string itemId)
    {
        if (!IsOwner || equippedItemRoot == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null || item.EquippedPrefab == null)
        {
            return;
        }

        ClearLocalEquippedVisual();

        localEquippedVisual = InstantiateEquippedVisual(item, equippedItemRoot, $"LocalEquipped_{item.Name}");
        if (localEquippedVisual == null)
        {
            return;
        }

        RemoveNetworkObjectIfPresent(localEquippedVisual);

        CC_INV_EquippedItem equippedItem = localEquippedVisual.GetComponent<CC_INV_EquippedItem>();
        if (equippedItem != null)
        {
            equippedItem.Init(item, true);
        }

        localEquippedVisual.SetActive(true);
    }

    private void ShowReplicatedEquippedVisual(string itemId)
    {
        Transform followRoot = GetReplicatedEquippedItemRoot();
        if (followRoot == null)
        {
            return;
        }

        INV_Item item = GetItemById(itemId);
        if (item == null || item.EquippedPrefab == null)
        {
            return;
        }

        ClearReplicatedEquippedVisual();

        replicatedEquippedVisual = InstantiateEquippedVisual(item, followRoot, $"ReplicatedEquipped_{item.Name}");
        if (replicatedEquippedVisual == null)
        {
            return;
        }

        RemoveNetworkObjectIfPresent(replicatedEquippedVisual);

        CC_INV_EquippedItem equippedItem = replicatedEquippedVisual.GetComponent<CC_INV_EquippedItem>();
        if (equippedItem != null)
        {
            equippedItem.Init(item, false);
            equippedItem.SetVisualVisible(!(IsServer && IsOwner));
        }

        replicatedEquippedVisual.SetActive(true);
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

    private void ClearLocalEquippedVisual()
    {
        if (localEquippedVisual != null)
        {
            Destroy(localEquippedVisual);
        }

        localEquippedVisual = null;
    }

    private void ClearReplicatedEquippedVisual()
    {
        if (replicatedEquippedVisual != null)
        {
            Destroy(replicatedEquippedVisual);
        }

        replicatedEquippedVisual = null;
    }

    private Transform GetReplicatedEquippedItemRoot()
    {
        if (replicatedEquippedItemRoot != null)
        {
            return replicatedEquippedItemRoot;
        }

        CC_CameraController cameraController = GetComponentInParent<CC_CameraController>();
        if (cameraController != null && cameraController.ReplicatedCameraDirectionRoot != null)
        {
            replicatedEquippedItemRoot = cameraController.ReplicatedCameraDirectionRoot;
            return replicatedEquippedItemRoot;
        }

        return transform;
    }

    public string GetEquippedItemId()
    {
        return equippedItemId.Value.ToString();
    }
}