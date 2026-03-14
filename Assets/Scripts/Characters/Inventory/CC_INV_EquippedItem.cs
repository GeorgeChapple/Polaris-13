using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Script that goes on the equipped item instance, inherits the item instance data.

public class CC_INV_EquippedItem : NetworkBehaviour
{
    [Header("Visual Root")]
    [Tooltip("Optional child root to hold the visual. If null we create one.")]
    [SerializeField] private Transform visualRoot;

    // internals
    private INV_Inventory.ItemInstance itemInstance;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Renderer[] cachedRenderers;

    private NetworkVariable<FixedString128Bytes> networkItemId = new NetworkVariable<FixedString128Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<ulong> owningPlayerClientId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public string ItemId => networkItemId.Value.ToString();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        networkItemId.OnValueChanged += OnItemIdChanged;
        owningPlayerClientId.OnValueChanged += OnOwningPlayerChanged;

        ResolveItemFromNetworkId();
        RefreshOwnerVisibility();
    }

    public override void OnNetworkDespawn()
    {
        networkItemId.OnValueChanged -= OnItemIdChanged;
        owningPlayerClientId.OnValueChanged -= OnOwningPlayerChanged;

        base.OnNetworkDespawn();
    }

    // called by server before spawn
    public void Init(string itemId, ulong ownerClientId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogError("CC_INV_EquippedItem Init called with empty item id.", this);
            return;
        }

        networkItemId.Value = itemId;
        owningPlayerClientId.Value = ownerClientId;
        ResolveItemFromNetworkId();
    }

    private void OnItemIdChanged(FixedString128Bytes oldValue, FixedString128Bytes newValue)
    {
        ResolveItemFromNetworkId();
    }

    private void OnOwningPlayerChanged(ulong oldValue, ulong newValue)
    {
        RefreshOwnerVisibility();
    }

    private void ResolveItemFromNetworkId()
    {
        string itemId = networkItemId.Value.ToString();
        if (string.IsNullOrWhiteSpace(itemId)) { return; }

        INV_Item item = INV_ItemDatabase.Instance != null
            ? INV_ItemDatabase.Instance.GetItemById(itemId)
            : null;

        if (item == null)
        {
            Debug.LogError($"Could not resolve equipped item id '{itemId}' from INV_ItemDatabase.", this);
            return;
        }

        ApplyVisuals(item);
    }

    public void SnapToAnchor(Transform anchor)
    {
        if (anchor == null) { return; }

        transform.position = anchor.position;
        transform.rotation = anchor.rotation;
        transform.localScale = Vector3.one;
    }

    public void SetVisualVisible(bool isVisible)
    {
        EnsureVisualSetup();

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null) { continue; }
            cachedRenderers[i].enabled = isVisible;
        }
    }

    private void RefreshOwnerVisibility()
    {
        if (NetworkManager == null)
        {
            SetVisualVisible(true);
            return;
        }

        // owner uses the local-only first person visual instead
        if (NetworkManager.LocalClientId == owningPlayerClientId.Value)
        {
            SetVisualVisible(false);
        }
        else
        {
            SetVisualVisible(true);
        }
    }

    private void ApplyVisuals(INV_Item item)
    {
        if (item == null) { return; }

        EnsureVisualSetup();

        if (meshFilter != null)
        {
            meshFilter.sharedMesh = item.Mesh;
        }

        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = item.Material;
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition = item.EquippedMeshOffset;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one * item.EquippedMeshScale;
        }

        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        gameObject.name = $"Equipped_{item.Name}";

        RefreshOwnerVisibility();
    }

    private void EnsureVisualSetup()
    {
        if (visualRoot == null)
        {
            Transform found = transform.Find("VisualRoot");
            if (found != null)
            {
                visualRoot = found;
            }
            else
            {
                GameObject go = new GameObject("VisualRoot");
                visualRoot = go.transform;
                visualRoot.SetParent(transform, false);
            }
        }

        if (meshFilter == null)
        {
            meshFilter = visualRoot.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = visualRoot.gameObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = visualRoot.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = visualRoot.gameObject.AddComponent<MeshRenderer>();
            }
        }
    }
}