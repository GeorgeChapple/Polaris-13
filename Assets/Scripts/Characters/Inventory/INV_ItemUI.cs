using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Made By: Jason Lodge
// Summary: UI dragging handler for inventory items.

public class INV_ItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Mesh Visual")]
    [Tooltip("Child object containing MeshFilter + MeshRenderer.")]
    [SerializeField] private Transform meshVisualRoot;

    [Header("UI")]
    [SerializeField] private Image durabilityBar;

    [SerializeField] private float durabilityBarMaxHeight;
    [SerializeField] private float durabilityBarWidthFromEdge;
    [SerializeField] private float durabilityBarYOffset;

    [Header("Hotbar")]
    [Tooltip("TMP label used to show hotbar slot assignment.")]
    [SerializeField] private TextMeshProUGUI hotbarSlotLabel;

    private INV_Inventory inv;
    private INV_Inventory.ItemInstance itemInst;

    private RectTransform rt;
    private Canvas rootCanvas;

    private MeshFilter mf;
    private MeshRenderer mr;

    private Vector2 startAnchoredPos;
    private Vector2Int startCell; // for snapping back to if we dont/cant drop on that grid cell

    public INV_Inventory.ItemInstance Instance => itemInst;

    // setup, called by inventory
    public void Init(INV_Inventory inventory, INV_Inventory.ItemInstance instance)
    {
        inv = inventory;
        itemInst = instance;

        rt = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();

        // cache mesh components
        if (meshVisualRoot != null)
        {
            mf = meshVisualRoot.GetComponent<MeshFilter>();
            mr = meshVisualRoot.GetComponent<MeshRenderer>();
        }

        // apply initial visuals
        ApplyUpdatedVisuals();

        // initial cell, will be set when inventory places the item
        startCell = itemInst != null ? itemInst.cell : Vector2Int.zero;
    }

    // called by inventory after rotation / rebuild
    public void ApplyUpdatedVisuals()
    {
        if (itemInst == null || rt == null) { return; }

        // durability bar stays as UI for now
        if (durabilityBar != null)
        {
            durabilityBar.rectTransform.anchoredPosition = new Vector2(0f, durabilityBarYOffset);
            durabilityBar.rectTransform.sizeDelta = new Vector2(rt.sizeDelta.x - durabilityBarWidthFromEdge, durabilityBarMaxHeight);
        }

        INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
        if (hotBar != null && itemInst != null)
        {
            SetHotbarSlotLabel(hotBar.GetAssignedSlotForItem(itemInst));
        }

        // mesh visuals
        ApplyMeshVisuals();
    }

    private void ApplyMeshVisuals()
    {
        if (meshVisualRoot == null) { return; }
        if (itemInst == null || itemInst.data == null) { return; }

        if (mf == null) { mf = meshVisualRoot.GetComponent<MeshFilter>(); }
        if (mr == null) { mr = meshVisualRoot.GetComponent<MeshRenderer>(); }

        if (mf == null || mr == null) { return; }

        // assign mesh/material
        if (itemInst.data.Mesh != null)
        {
            mf.sharedMesh = itemInst.data.Mesh;
        }

        if (itemInst.data.Material != null)
        {
            mr.sharedMaterial = itemInst.data.Material;
        }

        // position and scale
        // item root is top left pivot so get center
        Vector2 rectSize = rt.rect.size;
        Vector3 rectCenterLocal = new Vector3(rectSize.x * 0.5f, -rectSize.y * 0.5f, 0f);

        Vector3 itemOffset = itemInst.data.InventoryMeshOffset;
        float itemScale = itemInst.data.InventoryMeshScale;

        // make sure we dont put a negative scale on it
        if (itemScale <= 0f) { itemScale = 0; }

        // center placement
        meshVisualRoot.localPosition = rectCenterLocal + itemOffset;
        meshVisualRoot.localScale = Vector3.one * itemScale;

        // rotation handling
        if (itemInst.rotation == INV_Inventory.ItemInstance.Rotation.Vertical)
        {
            meshVisualRoot.localEulerAngles = Vector3.zero;
            return;
        }

        // horizontal rotate
        meshVisualRoot.localEulerAngles = new Vector3(0f, 0f, -90f);
    }

    public void SetHotbarSlotLabel(int slotIndex)
    {
        if (hotbarSlotLabel == null) { return; }

        if (slotIndex < 0)
        {
            hotbarSlotLabel.text = string.Empty;
            hotbarSlotLabel.gameObject.SetActive(false);
            return;
        }

        hotbarSlotLabel.gameObject.SetActive(true);
        hotbarSlotLabel.text = (slotIndex + 1).ToString();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // set up for drag
        if (inv == null || itemInst == null) { return; }

        if (rt == null) { rt = GetComponent<RectTransform>(); }
        if (rootCanvas == null) { rootCanvas = GetComponentInParent<Canvas>(); }

        inv.heldItem = this;

        // prevent drop logic from seeing an old hover while dragging
        inv.hoverItem = null;

        startAnchoredPos = rt.anchoredPosition;
        startCell = itemInst.cell;

        // bring to front
        rt.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rt == null || rootCanvas == null) { return; }

        Vector2 delta = eventData.delta / rootCanvas.scaleFactor;
        rt.anchoredPosition += delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // snap it back to grid
        if (inv == null || itemInst == null) { return; }

        Camera uiCam = eventData.pressEventCamera;

        bool moved = inv.TryMoveItemFromScreenPoint(itemInst, eventData.position, uiCam);
        if (!moved)
        {
            // return to previous placed cell
            Vector2 anchored = inv.GetAnchoredPosForCell(startCell.x, startCell.y);
            rt.anchoredPosition = anchored;

            // restore the logical cell in case something changed
            itemInst.cell = startCell;
        }
        else
        {
            // update snap back reference now that the item is placed somewhere new
            startCell = itemInst.cell;
            startAnchoredPos = rt.anchoredPosition;
        }

        inv.heldItem = null;
    }
}
