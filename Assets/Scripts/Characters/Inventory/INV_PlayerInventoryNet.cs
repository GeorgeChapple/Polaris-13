using Unity.Netcode;
using UnityEngine;

public class INV_PlayerInventoryNet : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private INV_Inventory inventory;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }
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