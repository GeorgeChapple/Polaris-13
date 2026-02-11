using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: UI dragging handler for inventory items.

public class INV_ItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image icon;

    private INV_Inventory inv;
    private INV_Inventory.ItemInstance itemInst;

    private RectTransform rt;
    private Canvas rootCanvas;

    private Vector2 startAnchoredPos;
    private Vector2Int startCell; // for snapping back to if we dont/cant drop on that grid cell
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

        // initial cell, will be set when inventory places the item
        startCell = itemInst != null ? itemInst.cell : Vector2Int.zero;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // set up for drag
        if (inv == null || itemInst == null) { return; }

        if (rt == null) { rt = GetComponent<RectTransform>(); }
        if (rootCanvas == null) { rootCanvas = GetComponentInParent<Canvas>(); }

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
    }
}
