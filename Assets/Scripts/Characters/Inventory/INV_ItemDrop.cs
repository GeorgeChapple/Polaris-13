using UnityEngine;

[DisallowMultipleComponent]
public class INV_ItemDrop : MonoBehaviour
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

    private void ApplyItemVisuals()
    {
        if (item == null) { return; }

        if (renameToItemName)
        {
            gameObject.name = $"Drop_{item.Name}";
        }

        // ensure mesh components exist
        if (mf == null) { mf = GetComponent<MeshFilter>(); }
        if (mr == null) { mr = GetComponent<MeshRenderer>(); }

        if (mf == null) { mf = gameObject.AddComponent<MeshFilter>(); }
        if (mr == null) { mr = gameObject.AddComponent<MeshRenderer>(); }

        // assign mesh/material
        if (item.Mesh != null)
        {
            mf.sharedMesh = item.Mesh;
        }

        if (item.Material != null)
        {
            mr.sharedMaterial = item.Material;
        }

        // ensure we have a collider that matches visuals
        SetupColliderFromMesh(mf.sharedMesh);
    }

    private void SetupColliderFromMesh(Mesh mesh)
    {
        if (mesh == null) { return; }

        // remove any existing collider types we don't want
        MeshCollider mc = GetComponent<MeshCollider>();
        BoxCollider bc = GetComponent<BoxCollider>();

        if (useMeshCollider)
        {
            if (bc != null) { Destroy(bc); }

            if (mc == null) { mc = gameObject.AddComponent<MeshCollider>(); }
            mc.sharedMesh = null;       // force refresh
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
    public void TryAddToInventory()
    {
        // placeholder, need a per player ver
        INV_Inventory inv = FindAnyObjectByType<INV_Inventory>();
        if (inv == null) { return; }

        bool added = inv.TryAddItem(item);
        if (added)
        {
            Destroy(gameObject);
        }
    }
}
