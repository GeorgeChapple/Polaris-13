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
    private NetworkVariable<FixedString128Bytes> equippedItemId = new NetworkVariable<FixedString128Bytes>(
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
        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (replicatedEquippedItemRoot == null)
        {
            CC_CameraController cameraController = GetComponentInParent<CC_CameraController>();
            if (cameraController != null)
            {
                replicatedEquippedItemRoot = cameraController.ReplicatedCameraDirectionRoot;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        equippedItemId.OnValueChanged += OnEquippedItemIdChanged;

        // build current visual state when spawned
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
        // owner local first person visual follows local root
        if (localEquippedVisual != null && equippedItemRoot != null)
        {
            localEquippedVisual.transform.position = equippedItemRoot.position;
            localEquippedVisual.transform.rotation = equippedItemRoot.rotation;
            localEquippedVisual.transform.localScale = Vector3.one;
        }

        // replicated visual follows replicated root
        if (replicatedEquippedVisual != null)
        {
            Transform followRoot = GetReplicatedEquippedItemRoot();
            if (followRoot != null)
            {
                replicatedEquippedVisual.transform.position = followRoot.position;
                replicatedEquippedVisual.transform.rotation = followRoot.rotation;
                replicatedEquippedVisual.transform.localScale = Vector3.one;
            }
        }
    }

    private void OnEquippedItemIdChanged(FixedString128Bytes oldValue, FixedString128Bytes newValue)
    {
        RebuildEquippedVisuals(newValue.ToString());
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void AddItemLocalRpc(string itemId, RpcParams rpcParams = default)
    {
        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (inventory == null)
        {
            Debug.LogError("INV_PlayerInventoryNet could not find INV_Inventory.", this);
            return;
        }

        INV_Item item = INV_ItemDatabase.Instance != null
            ? INV_ItemDatabase.Instance.GetItemById(itemId)
            : null;

        if (item == null)
        {
            Debug.LogError($"Could not resolve item id '{itemId}' from INV_ItemDatabase.", this);
            return;
        }

        bool added = inventory.TryAddItem(item);
        if (!added)
        {
            Debug.LogWarning($"Client inventory could not add item '{itemId}'.", this);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void CraftResultLocalRpc(bool succeeded, string craftedItemId, RpcParams rpcParams = default)
    {
        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (inventory == null)
        {
            Debug.LogError("INV_PlayerInventoryNet could not find INV_Inventory.", this);
            OnCraftRequestFinished?.Invoke(false, craftedItemId);
            return;
        }

        if (succeeded)
        {
            INV_Item craftedItem = INV_ItemDatabase.Instance != null
                ? INV_ItemDatabase.Instance.GetItemById(craftedItemId)
                : null;

            if (craftedItem != null)
            {
                // remove crafting requirements locally
                for (int i = 0; i < craftedItem.CraftingRequirements.Count; i++)
                {
                    INV_Item.CraftingStack req = craftedItem.CraftingRequirements[i];
                    if (req == null || req.item == null || req.amount <= 0)
                    {
                        continue;
                    }

                    inventory.RemoveItemAmount(req.item.ItemID, req.amount);
                }

                // then add crafted result locally
                inventory.TryAddItem(craftedItem);
            }
        }

        OnCraftRequestFinished?.Invoke(succeeded, craftedItemId);
    }

    // Called by local inventory UI
    public void RequestDropItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogError("RequestDropItem called with empty itemId.", this);
            return;
        }

        // host can process immediately
        if (IsServer)
        {
            SpawnDroppedItem_Server(itemId);
            return;
        }

        // client asks server to spawn it
        RequestDropItemRpc(itemId);
    }

    public void RequestEquipItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            RequestClearEquippedItem();
            return;
        }

        // owner should always build their local only first person visual immediately
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
            Debug.LogWarning("Only the owner can request use of the equipped item.", this);
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
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogError("RequestUseItemInInventory called with empty itemId.", this);
            return;
        }

        if (!IsOwner)
        {
            Debug.LogWarning("Only the owner can request use of the equipped item.", this);
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
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        if (!IsOwner)
        {
            Debug.LogWarning("Only the owner can request crafting.", this);
            return;
        }

        if (IsServer)
        {
            CraftItem_Server(itemId);
            return;
        }

        RequestCraftItemRpc(itemId);
    }

    [Rpc(SendTo.Server)]
    private void RequestDropItemRpc(string itemId, RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogWarning("RequestDropItemRpc received empty itemId.", this);
            return;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (OwnerClientId != senderClientId)
        {
            Debug.LogWarning($"Client {senderClientId} tried to drop from player owned by {OwnerClientId}.", this);
            return;
        }

        SpawnDroppedItem_Server(itemId);
    }

    [Rpc(SendTo.Server)]
    private void RequestEquipItemRpc(string itemId, RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogWarning("RequestEquipItemRpc received empty itemId.", this);
            return;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (OwnerClientId != senderClientId)
        {
            Debug.LogWarning($"Client {senderClientId} tried to equip from player owned by {OwnerClientId}.", this);
            return;
        }

        EquipItem_Server(itemId);
    }

    [Rpc(SendTo.Server)]
    private void RequestClearEquippedItemRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (OwnerClientId != senderClientId)
        {
            Debug.LogWarning($"Client {senderClientId} tried to clear equip from player owned by {OwnerClientId}.", this);
            return;
        }

        ClearEquippedItem_Server();
    }

    [Rpc(SendTo.Server)]
    private void RequestUseEquippedItemRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (OwnerClientId != senderClientId)
        {
            Debug.LogWarning($"Client {senderClientId} tried to use equip from player owned by {OwnerClientId}.", this);
            return;
        }

        UseEquippedItem_Server();
    }

    [Rpc(SendTo.Server)]
    private void RequestUseItemInInventoryRpc(string itemId, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (OwnerClientId != senderClientId)
        {
            Debug.LogWarning($"Client {senderClientId} tried to use item in player inventory owned by {OwnerClientId}.", this);
            return;
        }
        UseItemInInventory_Server(itemId);
    }

    [Rpc(SendTo.Server)]
    private void RequestCraftItemRpc(string itemId, RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (OwnerClientId != senderClientId)
        {
            Debug.LogWarning($"Client {senderClientId} tried to craft on player owned by {OwnerClientId}.", this);
            return;
        }

        CraftItem_Server(itemId);
    }

    private void EquipItem_Server(string itemId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("EquipItem_Server called while not on server.", this);
            return;
        }

        INV_Item item = INV_ItemDatabase.Instance != null
            ? INV_ItemDatabase.Instance.GetItemById(itemId)
            : null;

        if (item == null)
        {
            Debug.LogError($"Could not resolve equipped item id '{itemId}' from INV_ItemDatabase.", this);
            return;
        }

        if (item.EquippedPrefab == null)
        {
            Debug.LogError($"Item '{item.Name}' is missing EquippedPrefab.", item);
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
            Debug.LogWarning("ClearEquippedItem_Server called while not on server.", this);
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
            Debug.LogWarning("UseEquippedItem_Server called while not on server.", this);
            return;
        }

        string itemId = equippedItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogWarning("Tried to use equipped item, but no item is equipped.", this);
            return;
        }

        if (replicatedEquippedVisual == null)
        {
            Debug.LogWarning("Server has no authoritative equipped visual to use.", this);
            return;
        }

        MonoBehaviour[] behaviours = replicatedEquippedVisual.GetComponentsInChildren<MonoBehaviour>(true);
        bool foundUsable = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IUsableItem usableItem)
            {
                usableItem.WireUp(gameObject, itemId);
                usableItem.OnUse();
                foundUsable = true;
            }
        }

        if (!foundUsable)
        {
            Debug.LogWarning($"Equipped item '{itemId}' has no IUsableItem components.", replicatedEquippedVisual);
        }
    }

    private void UseItemInInventory_Server(string itemId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("UseItemInInventory_Server called while not on server.", this);
            return;
        }

        INV_Item usedItem = INV_ItemDatabase.Instance != null
            ? INV_ItemDatabase.Instance.GetItemById(itemId)
            : null;

        if (usedItem.objectType != INV_Item.ObjectType.Consumable) { return; }

        bool removed = inventory.RemoveItemAmount(itemId, 1);

        if (removed)
        {
            //use here
            characterValues.AddHungerDelay(usedItem.HungerDrainDelay);
            characterValues.AddHunger(usedItem.HungerReplenish);

            characterValues.AddThirstDelay(usedItem.ThirstDrainDelay);
            characterValues.AddThirst(usedItem.ThirstReplenish);
            return;
        }
    }

    private void CraftItem_Server(string itemId)
    {
        if (!IsServer)
        {
            return;
        }

        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (inventory == null)
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        INV_Item craftedItem = INV_ItemDatabase.Instance != null
            ? INV_ItemDatabase.Instance.GetItemById(itemId)
            : null;

        if (craftedItem == null)
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        if (!craftedItem.Craftable)
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        // first validate all requirements exist
        for (int i = 0; i < craftedItem.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = craftedItem.CraftingRequirements[i];
            if (req == null || req.item == null || req.amount <= 0)
            {
                continue;
            }

            if (!inventory.HasItemAmount(req.item.ItemID, req.amount))
            {
                NotifyCraftResult(false, itemId);
                return;
            }
        }

        // check output can be added before we remove requirements
        if (!inventory.CanAddItem(craftedItem))
        {
            NotifyCraftResult(false, itemId);
            return;
        }

        // remove requirements now
        for (int i = 0; i < craftedItem.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = craftedItem.CraftingRequirements[i];
            if (req == null || req.item == null || req.amount <= 0)
            {
                continue;
            }

            if (!inventory.RemoveItemAmount(req.item.ItemID, req.amount))
            {
                // failed removing, restore anything already removed
                for (int r = 0; r < i; r++)
                {
                    INV_Item.CraftingStack restoreReq = craftedItem.CraftingRequirements[r];
                    if (restoreReq == null || restoreReq.item == null || restoreReq.amount <= 0)
                    {
                        continue;
                    }

                    for (int a = 0; a < restoreReq.amount; a++)
                    {
                        inventory.TryAddItem(restoreReq.item);
                    }
                }

                NotifyCraftResult(false, itemId);
                return;
            }
        }

        // then add crafted item
        bool added = inventory.TryAddItem(craftedItem);
        if (!added)
        {
            // unexpected fail after remove, restore requirements
            for (int i = 0; i < craftedItem.CraftingRequirements.Count; i++)
            {
                INV_Item.CraftingStack restoreReq = craftedItem.CraftingRequirements[i];
                if (restoreReq == null || restoreReq.item == null || restoreReq.amount <= 0)
                {
                    continue;
                }

                for (int a = 0; a < restoreReq.amount; a++)
                {
                    inventory.TryAddItem(restoreReq.item);
                }
            }

            NotifyCraftResult(false, itemId);
            return;
        }

        NotifyCraftResult(true, itemId);
    }

    private void NotifyCraftResult(bool succeeded, string craftedItemId)
    {
        if (IsOwner)
        {
            // host / local owner already has the server side inventory instance changed
            OnCraftRequestFinished?.Invoke(succeeded, craftedItemId);
            return;
        }

        CraftResultLocalRpc(
            succeeded,
            craftedItemId,
            RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp)
        );
    }

    private void RebuildEquippedVisuals(string itemId)
    {
        // owner local first person visual
        if (IsOwner)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                ClearLocalEquippedVisual();
            }
            else
            {
                ShowLocalEquippedVisual(itemId);
            }
        }
        else
        {
            ClearLocalEquippedVisual();
        }

        // replicated / authority visual
        // exists on the server for authority
        // exists on non owners for third person visuals
        bool shouldHaveReplicatedVisual = IsServer || !IsOwner;

        if (shouldHaveReplicatedVisual)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                ClearReplicatedEquippedVisual();
            }
            else
            {
                ShowReplicatedEquippedVisual(itemId);
            }
        }
        else
        {
            ClearReplicatedEquippedVisual();
        }
    }

    private void ShowLocalEquippedVisual(string itemId)
    {
        if (!IsOwner) { return; }
        if (equippedItemRoot == null)
        {
            Debug.LogError("Missing equippedItemRoot for local equipped visual.", this);
            return;
        }

        INV_Item item = INV_ItemDatabase.Instance != null
            ? INV_ItemDatabase.Instance.GetItemById(itemId)
            : null;

        if (item == null)
        {
            Debug.LogError($"Could not resolve equipped item id '{itemId}' from INV_ItemDatabase.", this);
            return;
        }

        if (item.EquippedPrefab == null)
        {
            Debug.LogError($"Item '{item.Name}' is missing EquippedPrefab.", item);
            return;
        }

        ClearLocalEquippedVisual();

        localEquippedVisual = Instantiate(item.EquippedPrefab, equippedItemRoot);
        localEquippedVisual.name = $"LocalEquipped_{item.Name}";
        localEquippedVisual.transform.localPosition = Vector3.zero;
        localEquippedVisual.transform.localRotation = Quaternion.identity;
        localEquippedVisual.transform.localScale = Vector3.one;

        NetworkObject localNetObj = localEquippedVisual.GetComponent<NetworkObject>();
        if (localNetObj != null)
        {
            Destroy(localNetObj);
        }

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
            Debug.LogError("Could not find a replicated equipped item root.", this);
            return;
        }

        INV_Item item = INV_ItemDatabase.Instance != null
            ? INV_ItemDatabase.Instance.GetItemById(itemId)
            : null;

        if (item == null)
        {
            Debug.LogError($"Could not resolve equipped item id '{itemId}' from INV_ItemDatabase.", this);
            return;
        }

        if (item.EquippedPrefab == null)
        {
            Debug.LogError($"Item '{item.Name}' is missing EquippedPrefab.", item);
            return;
        }

        ClearReplicatedEquippedVisual();

        replicatedEquippedVisual = Instantiate(item.EquippedPrefab, followRoot);
        replicatedEquippedVisual.name = $"ReplicatedEquipped_{item.Name}";
        replicatedEquippedVisual.transform.localPosition = Vector3.zero;
        replicatedEquippedVisual.transform.localRotation = Quaternion.identity;
        replicatedEquippedVisual.transform.localScale = Vector3.one;

        NetworkObject replicatedNetObj = replicatedEquippedVisual.GetComponent<NetworkObject>();
        if (replicatedNetObj != null)
        {
            Destroy(replicatedNetObj);
        }

        CC_INV_EquippedItem equippedItem = replicatedEquippedVisual.GetComponent<CC_INV_EquippedItem>();
        if (equippedItem != null)
        {
            equippedItem.Init(item, false);

            // host needs this object for server authority,
            // but should not see it as the first person local visual is used instead
            if (IsServer && IsOwner)
            {
                equippedItem.SetVisualVisible(false);
            }
            else
            {
                equippedItem.SetVisualVisible(true);
            }
        }

        replicatedEquippedVisual.SetActive(true);
    }

    private void ClearLocalEquippedVisual()
    {
        if (localEquippedVisual != null)
        {
            Destroy(localEquippedVisual);
            localEquippedVisual = null;
        }
    }

    private void ClearReplicatedEquippedVisual()
    {
        if (replicatedEquippedVisual != null)
        {
            Destroy(replicatedEquippedVisual);
            replicatedEquippedVisual = null;
        }
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

    private void SpawnDroppedItem_Server(string itemId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("SpawnDroppedItem_Server called while not on server.", this);
            return;
        }

        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (inventory == null)
        {
            Debug.LogError("INV_PlayerInventoryNet could not find INV_Inventory.", this);
            return;
        }

        INV_Item item = INV_ItemDatabase.Instance != null
            ? INV_ItemDatabase.Instance.GetItemById(itemId)
            : null;

        if (item == null)
        {
            Debug.LogError($"Could not resolve item id '{itemId}' from INV_ItemDatabase.", this);
            return;
        }

        if (inventory.ItemPrefab == null || inventory.dropItemTransform == null)
        {
            Debug.LogError("Inventory is missing itemPrefab or dropItemTransform.", this);
            return;
        }

        Vector3 dropPoint = inventory.dropItemTransform.position;
        Quaternion dropRotation = inventory.dropItemTransform.rotation;

        GameObject drop = Instantiate(inventory.ItemPrefab, dropPoint, dropRotation);
        if (drop == null)
        {
            Debug.LogError("Failed to instantiate dropped item prefab.", this);
            return;
        }

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
        else
        {
            Debug.LogError("Dropped item prefab is missing INV_ItemDrop!", drop);
        }

        NetworkObject netObj = drop.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("Dropped item prefab is missing NetworkObject!", drop);
            Destroy(drop);
            return;
        }

        Rigidbody rb = drop.GetComponent<Rigidbody>();

        netObj.Spawn();

        if (rb != null)
        {
            rb.WakeUp();

            Vector3 throwVelocity = inventory.dropItemTransform.forward * testThrowPower;
            Vector3 randomTorque = Vector3.one * UnityEngine.Random.Range(-testThrowTorque, testThrowTorque);

            rb.linearVelocity = throwVelocity;
            rb.angularVelocity = randomTorque;
        }
    }

    public string GetEquippedItemId()
    {
        return equippedItemId.Value.ToString();
    }
}