using Unity.Netcode;
using UnityEngine;

public class INV_PlayerInventoryNet : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private INV_Inventory inventory;

    [Header("Equipped Item")]
    [SerializeField] private GameObject equippedItemPrefab;
    [SerializeField] private Transform equippedItemRoot;

    [Tooltip("Object root for the replicated equipped item for other players. If null, uses replicated camera direction root, then this objects transform.")]
    [SerializeField] private Transform replicatedEquippedItemRoot;

    private NetworkObject currentEquippedItem;

    // local owner only equipped visual
    private GameObject localEquippedVisual;
    private MeshFilter localEquippedMeshFilter;
    private MeshRenderer localEquippedMeshRenderer;

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

    private void LateUpdate()
    {
        // server drives the replicated equipped item transform
        if (!IsServer) { return; }
        if (currentEquippedItem == null) { return; }

        Transform followRoot = GetReplicatedEquippedItemRoot();
        if (followRoot == null) { return; }

        currentEquippedItem.transform.position = followRoot.position;
        currentEquippedItem.transform.rotation = followRoot.rotation;
        currentEquippedItem.transform.localScale = Vector3.one;
    }

    public override void OnNetworkDespawn()
    {
        ClearLocalEquippedVisual();
        base.OnNetworkDespawn();
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

    private void EquipItem_Server(string itemId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("EquipItem_Server called while not on server.", this);
            return;
        }

        if (equippedItemPrefab == null)
        {
            Debug.LogError("Missing equippedItemPrefab.", this);
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

        ClearEquippedItem_Server();

        Transform spawnRoot = GetReplicatedEquippedItemRoot();
        if (spawnRoot == null)
        {
            Debug.LogError("Could not find a replicated equipped item root.", this);
            return;
        }

        GameObject equipped = Instantiate(equippedItemPrefab, spawnRoot.position, spawnRoot.rotation);
        if (equipped == null)
        {
            Debug.LogError("Failed to instantiate equipped item prefab.", this);
            return;
        }

        CC_INV_EquippedItem equippedItem = equipped.GetComponent<CC_INV_EquippedItem>();
        NetworkObject netObj = equipped.GetComponent<NetworkObject>();
        NetworkObject playerNetObj = GetComponentInParent<NetworkObject>();

        if (equippedItem == null)
        {
            Debug.LogError("Equipped item prefab is missing CC_INV_EquippedItem!", equipped);
            Destroy(equipped);
            return;
        }

        if (netObj == null)
        {
            Debug.LogError("Equipped item prefab is missing NetworkObject!", equipped);
            Destroy(equipped);
            return;
        }

        if (playerNetObj == null)
        {
            Debug.LogError("Player is missing NetworkObject!", gameObject);
            Destroy(equipped);
            return;
        }

        // init before spawn so replicated vars are already set
        equippedItem.Init(itemId, OwnerClientId);

        // server owns the replicated equipped item because server is driving its transform
        netObj.Spawn();

        // valid network parenting, because parent is a spawned network object
        bool parented = netObj.TrySetParent(playerNetObj, false);
        if (!parented)
        {
            Debug.LogWarning("Failed to parent equipped item under player NetworkObject.", equipped);
        }

        // snap to replicated anchor
        equippedItem.SnapToAnchor(spawnRoot);

        currentEquippedItem = netObj;
    }

    private void ClearEquippedItem_Server()
    {
        if (!IsServer)
        {
            Debug.LogWarning("ClearEquippedItem_Server called while not on server.", this);
            return;
        }

        if (currentEquippedItem == null) { return; }

        if (currentEquippedItem.IsSpawned)
        {
            currentEquippedItem.Despawn(true);
        }
        else
        {
            Destroy(currentEquippedItem.gameObject);
        }

        currentEquippedItem = null;
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

        EnsureLocalEquippedVisual();

        if (localEquippedMeshFilter != null)
        {
            localEquippedMeshFilter.sharedMesh = item.Mesh;
        }

        if (localEquippedMeshRenderer != null)
        {
            localEquippedMeshRenderer.sharedMaterial = item.Material;
        }

        localEquippedVisual.name = $"LocalEquipped_{item.Name}";
        localEquippedVisual.transform.SetParent(equippedItemRoot, false);
        localEquippedVisual.transform.localPosition = item.EquippedMeshOffset;
        localEquippedVisual.transform.localRotation = Quaternion.identity;
        localEquippedVisual.transform.localScale = Vector3.one * item.EquippedMeshScale;
        localEquippedVisual.SetActive(true);
    }

    private void ClearLocalEquippedVisual()
    {
        if (localEquippedVisual != null)
        {
            localEquippedVisual.SetActive(false);
        }
    }

    private void EnsureLocalEquippedVisual()
    {
        if (localEquippedVisual == null)
        {
            localEquippedVisual = new GameObject("LocalEquippedVisual");
            localEquippedVisual.transform.SetParent(equippedItemRoot, false);

            localEquippedMeshFilter = localEquippedVisual.AddComponent<MeshFilter>();
            localEquippedMeshRenderer = localEquippedVisual.AddComponent<MeshRenderer>();
        }

        if (localEquippedMeshFilter == null)
        {
            localEquippedMeshFilter = localEquippedVisual.GetComponent<MeshFilter>();
            if (localEquippedMeshFilter == null)
            {
                localEquippedMeshFilter = localEquippedVisual.AddComponent<MeshFilter>();
            }
        }

        if (localEquippedMeshRenderer == null)
        {
            localEquippedMeshRenderer = localEquippedVisual.GetComponent<MeshRenderer>();
            if (localEquippedMeshRenderer == null)
            {
                localEquippedMeshRenderer = localEquippedVisual.AddComponent<MeshRenderer>();
            }
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
        GameObject drop = Instantiate(inventory.ItemPrefab, dropPoint, Quaternion.identity);
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

        Rigidbody rb = drop.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(inventory.dropItemTransform.forward, ForceMode.Impulse);
            rb.AddTorque(Vector3.one * Random.Range(-0.5f, 0.5f), ForceMode.Impulse);
        }

        NetworkObject netObj = drop.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("Dropped item prefab is missing NetworkObject!", drop);
            Destroy(drop);
            return;
        }

        netObj.Spawn();
    }
}