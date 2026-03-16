using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: UI dragging handler for inventory items.

public class INV_ItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Occupied Space Visual")]
    [Tooltip("Root that will hold occupied space visuals behind the item.")]
    [SerializeField] private RectTransform occupiedSpaceHolderRoot;

    [Header("Mesh Visual")]
    [Tooltip("Child object containing MeshFilter + MeshRenderer.")]
    [SerializeField] private Transform meshVisualRoot;

    [Header("UI")]
    [SerializeField] private Image durabilityBar;

    [SerializeField] private float durabilityBarMaxHeight;
    [SerializeField] private float durabilityBarWidthFromEdge;
    [SerializeField] private float durabilityBarYOffset;

    [Tooltip("TMP label used to show stack amount.")]
    [SerializeField] private TextMeshProUGUI stackCountLabel;

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
    private INV_Inventory.ItemInstance.Rotation startRotation; // to swap rotation back

    private readonly List<RectTransform> occupiedSpaceVisuals = new List<RectTransform>();

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

        if (occupiedSpaceHolderRoot == null)
        {
            occupiedSpaceHolderRoot = transform as RectTransform;
        }

        // apply initial visuals
        RebuildOccupiedSpaceVisuals();
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

        if (stackCountLabel != null)
        {
            if (itemInst.quantity > 1)
            {
                stackCountLabel.gameObject.SetActive(true);
                stackCountLabel.text = $"x{itemInst.quantity.ToString()}";
            }
            else
            {
                stackCountLabel.text = string.Empty;
                stackCountLabel.gameObject.SetActive(false);
            }
        }

        INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
        if (hotBar != null && itemInst != null)
        {
            SetHotbarSlotLabel(hotBar.GetAssignedSlotForItem(itemInst));
        }

        // mesh visuals
        ApplyMeshVisuals();
    }

    public void RebuildOccupiedSpaceVisuals()
    {
        if (occupiedSpaceHolderRoot == null) { return; }
        if (inv == null || itemInst == null) { return; }

        for (int i = occupiedSpaceHolderRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = occupiedSpaceHolderRoot.GetChild(i);

            // dont delete the mesh visual if root is shared
            if (meshVisualRoot != null && child == meshVisualRoot) { continue; }

            Destroy(child.gameObject);
        }

        occupiedSpaceVisuals.Clear();

        GameObject visualPrefab = inv.OccupiedSpacePrefab;
        if (visualPrefab == null) { return; }

        Vector2 cellSize = inv.CellSize;
        Vector2 spacing = inv.GridSpacing;

        for (int i = 0; i < itemInst.occupiedOffsets.Count; i++)
        {
            Vector2Int off = itemInst.occupiedOffsets[i];

            GameObject go = Instantiate(visualPrefab, occupiedSpaceHolderRoot);
            go.name = $"Occupied_X:{off.x},Y:{off.y}";

            RectTransform cellRt = go.GetComponent<RectTransform>();
            if (cellRt == null)
            {
                cellRt = go.AddComponent<RectTransform>();
            }

            cellRt.pivot = new Vector2(0f, 1f);
            cellRt.anchorMin = new Vector2(0f, 1f);
            cellRt.anchorMax = new Vector2(0f, 1f);

            bool hasLeft = HasOccupiedOffset(new Vector2Int(off.x - 1, off.y));
            bool hasRight = HasOccupiedOffset(new Vector2Int(off.x + 1, off.y));
            bool hasUp = HasOccupiedOffset(new Vector2Int(off.x, off.y - 1));
            bool hasDown = HasOccupiedOffset(new Vector2Int(off.x, off.y + 1));

            // fill half the spacing on each touching side so neighbouring cells meet cleanly
            float padLeft = hasLeft ? spacing.x * 0.5f : 0f;
            float padRight = hasRight ? spacing.x * 0.5f : 0f;
            float padUp = hasUp ? spacing.y * 0.5f : 0f;
            float padDown = hasDown ? spacing.y * 0.5f : 0f;

            float width = cellSize.x + padLeft + padRight;
            float height = cellSize.y + padUp + padDown;

            // top-left anchored item layout
            // move into the shared gap by the amount we padded toward left / up
            float x = (off.x * (cellSize.x + spacing.x)) - padLeft;
            float y = (-off.y * (cellSize.y + spacing.y)) + padUp;

            cellRt.sizeDelta = new Vector2(width, height);
            cellRt.anchoredPosition = new Vector2(x, y);

            cellRt.SetAsFirstSibling();
            occupiedSpaceVisuals.Add(cellRt);
        }
    }

    private bool HasOccupiedOffset(Vector2Int check)
    {
        if (itemInst == null) { return false; }

        for (int i = 0; i < itemInst.occupiedOffsets.Count; i++)
        {
            if (itemInst.occupiedOffsets[i] == check)
            {
                return true;
            }
        }

        return false;
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
        if (itemScale <= 0f) { itemScale = 0f; }

        // center placement
        meshVisualRoot.localPosition = rectCenterLocal + itemOffset;
        meshVisualRoot.localScale = Vector3.one * itemScale;

        // rotation handling
        switch (itemInst.rotation)
        {
            case INV_Inventory.ItemInstance.Rotation.Up:
                meshVisualRoot.localEulerAngles = Vector3.zero;
                break;

            case INV_Inventory.ItemInstance.Rotation.Right:
                meshVisualRoot.localEulerAngles = new Vector3(0f, 0f, -90f);
                break;

            case INV_Inventory.ItemInstance.Rotation.Down:
                meshVisualRoot.localEulerAngles = new Vector3(0f, 0f, -180f);
                break;

            case INV_Inventory.ItemInstance.Rotation.Left:
                meshVisualRoot.localEulerAngles = new Vector3(0f, 0f, -270f);
                break;
        }
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
        startRotation = itemInst.rotation;

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
            // restore previous placed cell and previous rotation
            inv.RestoreItemToCellAndRotation(itemInst, startCell, startRotation);

            // keep local references in sync
            startAnchoredPos = rt.anchoredPosition;
            itemInst.cell = startCell;
            itemInst.rotation = startRotation;
        }
        else
        {
            // update snap back reference now that the item is placed somewhere new
            startCell = itemInst.cell;
            startRotation = itemInst.rotation;
            startAnchoredPos = rt.anchoredPosition;
        }

        inv.heldItem = null;
    }
}