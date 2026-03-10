using Unity.Netcode;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Item Drop, used by item prefabs on floor.

[DisallowMultipleComponent]
public class INV_ItemDrop : NetworkBehaviour
{
    [Header("Item Data")]
    [SerializeField] private INV_Item item;

    [Header("Auto Setup")]
    [Tooltip("If true, uses MeshCollider. If false, uses BoxCollider.")]
    [SerializeField] private bool useMeshCollider = false;

    [Tooltip("If true, renames the drop object to include the item name.")]
    [SerializeField] private bool renameToItemName = true;

    private MeshFilter mf;
    private MeshRenderer mr;

    // called by inventory when spawning a drop
    public void Init(INV_Item newItem)
    {
        item = newItem;
        ApplyItemVisuals();
    }

    private string GetItemId()
    {
        return item != null ? item.ItemID : string.Empty;
    }

    private void ApplyItemVisuals()
    {
        if (item == null) { return; }

        if (renameToItemName)
        {
            gameObject.name = $"Drop_{item.Name}";
        }

        if (mf == null) { mf = GetComponent<MeshFilter>(); }
        if (mr == null) { mr = GetComponent<MeshRenderer>(); }

        if (mf == null) { mf = gameObject.AddComponent<MeshFilter>(); }
        if (mr == null) { mr = gameObject.AddComponent<MeshRenderer>(); }

        if (item.Mesh != null)
        {
            mf.sharedMesh = item.Mesh;
        }

        if (item.Material != null)
        {
            mr.sharedMaterial = item.Material;
        }

        SetupColliderFromMesh(mf.sharedMesh);
    }

    private void SetupColliderFromMesh(Mesh mesh)
    {
        if (mesh == null) { return; }

        MeshCollider mc = GetComponent<MeshCollider>();
        BoxCollider bc = GetComponent<BoxCollider>();

        if (useMeshCollider)
        {
            if (bc != null) { Destroy(bc); }

            if (mc == null) { mc = gameObject.AddComponent<MeshCollider>(); }
            mc.sharedMesh = null; // force refresh
            mc.sharedMesh = mesh;
            mc.convex = true;
        }
        else
        {
            if (mc != null) { Destroy(mc); }

            if (bc == null) { bc = gameObject.AddComponent<BoxCollider>(); }
            bc.center = mesh.bounds.center;
            bc.size = mesh.bounds.size;
        }
    }

    // called by player interact script
    public void TryAddToInventory(Object interactor)
    {
        if (interactor == null)
        {
            Debug.LogError("Interactor is Null!", this);
            return;
        }

        GameObject player = interactor as GameObject;
        if (player == null)
        {
            Debug.LogError("Interactor is not a GameObject!", this);
            return;
        }

        if (IsServer)
        {
            NetworkObject playerNetObj = player.GetComponent<NetworkObject>();
            if (playerNetObj == null)
            {
                Debug.LogError("Interactor player is missing NetworkObject!", player);
                return;
            }

            TryAddToInventory_Server(player, playerNetObj.OwnerClientId);
            return;
        }

        RequestPickupRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPickupRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (NetworkManager == null)
        {
            Debug.LogError("NetworkManager is null!", this);
            return;
        }

        if (!NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var clientData))
        {
            Debug.LogWarning($"No ConnectedClients entry for senderClientId {senderClientId}.", this);
            return;
        }

        NetworkObject playerNetObj = clientData.PlayerObject;
        if (playerNetObj == null)
        {
            Debug.LogWarning($"Client {senderClientId} has no PlayerObject.", this);
            return;
        }

        TryAddToInventory_Server(playerNetObj.gameObject, senderClientId);
    }

    private void TryAddToInventory_Server(GameObject player, ulong senderClientId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("TryAddToInventory_Server called while not running on server.", this);
            return;
        }

        if (player == null)
        {
            Debug.LogError("No Player ref in TryAddToInventory_Server!", this);
            return;
        }

        if (item == null)
        {
            Debug.LogError("INV_ItemDrop has no item assigned!", this);
            return;
        }

        string itemId = GetItemId();
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogError($"Item '{item.name}' has an empty ItemID.", this);
            return;
        }

        INV_PlayerInventoryNet playerInvNet = player.GetComponentInChildren<INV_PlayerInventoryNet>();
        if (playerInvNet == null)
        {
            Debug.LogError("Couldn't find INV_PlayerInventoryNet on Player!", this);
            return;
        }

        playerInvNet.AddItemLocalRpc(
            itemId,
            RpcTarget.Single(senderClientId, RpcTargetUse.Temp)
        );

        if (NetworkObject == null)
        {
            Debug.LogError("INV_ItemDrop is missing NetworkObject!", this);
            return;
        }

        if (!NetworkObject.IsSpawned)
        {
            Debug.LogWarning("Tried to despawn a pickup that is not spawned.", this);
            return;
        }

        NetworkObject.Despawn(true);
    }
}