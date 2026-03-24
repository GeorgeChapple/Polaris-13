using UnityEngine;

// Made By: Jason Lodge
// Summary: Script that goes on the equipped item instance, inherits the item instance data.

public class CC_INV_EquippedItem : MonoBehaviour
{
    [Header("Visual Root")]
    [Tooltip("Optional child root to hold the visual. If null we create one if item visual setup is enabled.")]
    [SerializeField] private Transform visualRoot;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Renderer[] cachedRenderers;

    private INV_Item item;
    private bool isOwnerVisual;

    public INV_Item Item => item;
    public bool IsOwnerVisual => isOwnerVisual;

    public void Init(INV_Item newItem, bool newIsOwnerVisual)
    {
        item = newItem;
        isOwnerVisual = newIsOwnerVisual;

        ApplyVisuals();
    }

    public void SetVisualVisible(bool isVisible)
    {
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

    private void ApplyVisuals()
    {
        if (item == null) { return; }

        if (item.ApplyEquippedPrefabVisuals)
        {
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
                visualRoot.localRotation = Quaternion.Euler(item.EquippedMeshRotation);
                visualRoot.localScale = Vector3.one * item.EquippedMeshScale;
            }
        }

        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        gameObject.name = $"Equipped_{item.Name}";
        SetVisualVisible(true);
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