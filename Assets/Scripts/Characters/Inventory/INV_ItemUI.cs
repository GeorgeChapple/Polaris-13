using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: UI dragging handler for inventory items.

public class INV_ItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private INV_Inventory inv;

    private RectTransform rt;
    private Canvas rootCanvas;

    private Vector2 startAnchoredPos;
    private Vector2Int startCell; // for snapping back to if we dont/cant drop on that grid cell

    public void Init(INV_Inventory inventory)
    {
        // do setup here
        // call by inventory
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // set up for drag
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.delta / rootCanvas.scaleFactor;
        rt.anchoredPosition += delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // snap it back to grid
    }
}
