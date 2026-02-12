using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: UI dragging handler for inventory items.

public class INV_ItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image icon;

    [SerializeField] private Image durabilityBar;

    [SerializeField] private float durabilityBarMaxHeight;
    [SerializeField] private float durabilityBarWidthFromEdge;
    [SerializeField] private float durabilityBarYOffset;

    private INV_Inventory inv;
    private INV_Inventory.ItemInstance itemInst;

    private RectTransform rt;
    private Canvas rootCanvas;

    // visual rect we rotate (keeps root rect stable for snapping)
    private RectTransform visualRT;

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

        if (icon != null && itemInst != null && itemInst.data != null)
        {
            icon.sprite = itemInst.data.Icon;
            icon.preserveAspect = true;
        }

        // default visual to rotate is the icon rect
        if (icon != null)
        {
            visualRT = icon.rectTransform;

            // make sure the icon uses predictable anchors/pivot
            visualRT.anchorMin = new Vector2(0f, 1f);
            visualRT.anchorMax = new Vector2(0f, 1f);
            visualRT.pivot = new Vector2(0f, 1f);
        }

        // initial cell, will be set when inventory places the item
        startCell = itemInst != null ? itemInst.cell : Vector2Int.zero;
    }

    // called by inventory after rotation / rebuild
    public void ApplyUpdatedVisuals()
    {
        if (itemInst == null || rt == null) { return; }
        if (visualRT == null) { return; }

        durabilityBar.rectTransform.anchoredPosition = new Vector2(0f, durabilityBarYOffset);
        durabilityBar.rectTransform.sizeDelta = new Vector2(rt.sizeDelta.x - durabilityBarWidthFromEdge, durabilityBarMaxHeight);

        // reset first so we get consistent results
        visualRT.localEulerAngles = Vector3.zero;
        visualRT.anchoredPosition = Vector2.zero;

        if (itemInst.rotation == INV_Inventory.ItemInstance.Rotation.Vertical)
        {
            visualRT.pivot = new Vector2(0.5f, 0.5f);
            visualRT.anchorMin = new Vector2(0.5f, 0.5f);
            visualRT.anchorMax = new Vector2(0.5f, 0.5f);
            visualRT.sizeDelta = rt.sizeDelta;

            visualRT.localEulerAngles = Vector3.zero;
            visualRT.anchoredPosition = Vector2.zero;
            return;
        }

        visualRT.pivot = new Vector2(0.5f, 0.5f);
        visualRT.anchorMin = new Vector2(0.5f, 0.5f);
        visualRT.anchorMax = new Vector2(0.5f, 0.5f);

        visualRT.localEulerAngles = new Vector3(0f, 0f, -90f);

        visualRT.sizeDelta = new Vector2(rt.sizeDelta.y, rt.sizeDelta.x);
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
            // update snap-back reference now that the item is placed somewhere new
            startCell = itemInst.cell;
            startAnchoredPos = rt.anchoredPosition;
        }

        inv.heldItem = null;
    }
}
