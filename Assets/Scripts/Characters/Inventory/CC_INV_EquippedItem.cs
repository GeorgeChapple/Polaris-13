using UnityEngine;

// Made By: Jason Lodge
// Summary: Script that goes on the equipped item instance, inherits the item instance data.

public class CC_INV_EquippedItem : MonoBehaviour
{
    [Header("Visual Root")]
    [Tooltip("Optional child root to hold the visual. If null we create one if item visual setup is enabled.")]
    [SerializeField] private Transform visualRoot;

    [SerializeField] private Transform rightHandSnapPoint;
    [SerializeField] private Transform leftHandSnapPoint;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Renderer[] cachedRenderers;

    private INV_Item item;
    private bool isOwnerVisual;

    public INV_Item Item => item;
    public bool IsOwnerVisual => isOwnerVisual;

    public Transform RightHandSnapPoint => rightHandSnapPoint;
    public Transform LeftHandSnapPoint => leftHandSnapPoint;

    // setup this UI element with its target data.
    public void Init(INV_Item newItem, bool newIsOwnerVisual)
    {
        item = newItem;
        isOwnerVisual = newIsOwnerVisual;

        ApplyVisuals();
    }

    public void SetVisualVisible(bool isVisible)
    {
        CacheRenderers();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null)
            {
                continue;
            }

            cachedRenderers[i].enabled = isVisible;
        }
    }

    private void ApplyVisuals()
    {
        if (item == null)
        {
            return;
        }

        gameObject.name = $"Equipped_{item.Name}";

        if (item.ApplyEquippedPrefabVisuals)
        {
            EnsureVisualSetup();
            ApplyItemMeshVisuals();
        }

        CacheRenderers();
        SetVisualVisible(true);
    }

    private void ApplyItemMeshVisuals()
    {
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = item.Mesh;
        }

        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = item.Material;
            meshRenderer.enabled = item.Mesh != null && item.Material != null;
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition = item.EquippedMeshOffset;
            visualRoot.localRotation = Quaternion.Euler(item.EquippedMeshRotation);
            visualRoot.localScale = Vector3.one * Mathf.Max(0f, item.EquippedMeshScale);
        }
    }

    private void EnsureVisualSetup()
    {
        if (visualRoot == null)
        {
            visualRoot = FindOrCreateVisualRoot();
        }

        if (meshFilter == null)
        {
            meshFilter = GetOrAddComponent<MeshFilter>(visualRoot.gameObject);
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetOrAddComponent<MeshRenderer>(visualRoot.gameObject);
        }
    }

    private Transform FindOrCreateVisualRoot()
    {
        Transform found = transform.Find("VisualRoot");
        if (found != null)
        {
            return found;
        }

        GameObject go = new GameObject("VisualRoot");
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void CacheRenderers()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T comp = target.GetComponent<T>();
        if (comp != null)
        {
            return comp;
        }

        return target.AddComponent<T>();
    }
}