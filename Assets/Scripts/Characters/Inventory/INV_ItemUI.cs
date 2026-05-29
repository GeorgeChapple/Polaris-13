using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: UI dragging handler for inventory items.

public class INV_ItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
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

    [Header("Overlay Snap")]
    [Tooltip("Inset used when snapping stack amount under the top-right occupied visual.")]
    [SerializeField] private float stackLabelInset = 4f;

    [Tooltip("Inset used when snapping hotbar slot under the top-left occupied visual.")]
    [SerializeField] private float hotbarLabelInset = 4f;

    [Tooltip("Vertical offset downward from the top edge of the occupied visual.")]
    [SerializeField] private float labelDownOffset = 2f;

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
    public RectTransform RectTransform => rt;

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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inv == null || itemInst == null) { return; }
        if (!IsScreenPointOverOccupiedSpace(eventData.position, eventData.pressEventCamera)) { return; }

        bool shiftHeld = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        if (eventData.button == PointerEventData.InputButton.Left && shiftHeld)
        {
            // shift click player inventory item into open chest
            if (!itemInst.isChestItem && inv.ActiveChest != null)
            {
                inv.TryQuickStoreItemInOpenChest(itemInst);
                return;
            }

            // shift click chest item into player inventory
            if (itemInst.isChestItem)
            {
                inv.TryQuickTakeChestItemStack(itemInst);
                return;
            }
        }
        if (eventData.button != PointerEventData.InputButton.Right) { return; }
        inv.ShowContextMenuForItem(itemInst, eventData.position);
    }

    // called by inventory after rotation / rebuild
    public void ApplyUpdatedVisuals()
    {
        if (itemInst == null || rt == null) { return; }

        // labels are snapped from the currently built occupied visuals
        // so do before mesh visuals too so overlay is always refreshed
        SnapOverlayLabelsUnderCornerOccupiedSpaces();

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
                stackCountLabel.SetText($"x{itemInst.quantity}");
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

        // and once more after all ui text changes
        SnapOverlayLabelsUnderCornerOccupiedSpaces();
    }

    public void RebuildOccupiedSpaceVisuals()
    {
        if (occupiedSpaceHolderRoot == null) { return; }
        if (inv == null || itemInst == null) { return; }

        // before rebuilding, move labels back to item root so they don't get caught in child cleanup logic
        RestoreOverlayParentsToItemRoot();

        for (int i = occupiedSpaceHolderRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = occupiedSpaceHolderRoot.GetChild(i);

            // dont delete the mesh visual if root is shared
            if (meshVisualRoot != null && child == meshVisualRoot) { continue; }

            // dont delete overlay labels if they happen to be under this root
            if (stackCountLabel != null && child == stackCountLabel.transform) { continue; }
            if (hotbarSlotLabel != null && child == hotbarSlotLabel.transform) { continue; }

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

        SnapOverlayLabelsUnderCornerOccupiedSpaces();
    }

    private void RestoreOverlayParentsToItemRoot()
    {
        if (rt == null)
        {
            rt = GetComponent<RectTransform>();
        }

        if (rt == null) { return; }

        if (hotbarSlotLabel != null && hotbarSlotLabel.transform.parent != rt)
        {
            hotbarSlotLabel.transform.SetParent(rt, true);
        }

        if (stackCountLabel != null && stackCountLabel.transform.parent != rt)
        {
            stackCountLabel.transform.SetParent(rt, true);
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

    // apply the item mesh/material setup for this crafting entry.
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
        Vector3 itemRotation = itemInst.data.InventoryMeshRotation;
        float itemScale = itemInst.data.InventoryMeshScale;

        // make sure we dont put a negative scale on it
        if (itemScale <= 0f) { itemScale = 0f; }

        // center placement
        meshVisualRoot.localPosition = rectCenterLocal + itemOffset;
        meshVisualRoot.localScale = Vector3.one * itemScale;

        // base rotation + pseudo forward axis item rotation
        Quaternion baseRotation = Quaternion.Euler(itemRotation);
        Quaternion gridRotation = GetGridRotationForPseudoForwardAxis(itemInst.data.ForwardAxisRotVal, itemInst.rotation);

        meshVisualRoot.localRotation = baseRotation * gridRotation;
    }

    private Quaternion GetGridRotationForPseudoForwardAxis(INV_Item.ForwardAxisRot forwardAxis, INV_Inventory.ItemInstance.Rotation rotation)
    {
        float angle = rotation == INV_Inventory.ItemInstance.Rotation.Up ? 0f :
                      rotation == INV_Inventory.ItemInstance.Rotation.Right ? -90f :
                      rotation == INV_Inventory.ItemInstance.Rotation.Down ? -180f : -270f;

        Vector3 axis = forwardAxis == INV_Item.ForwardAxisRot.X ? Vector3.right :
                       forwardAxis == INV_Item.ForwardAxisRot.Y ? Vector3.up :
                       Vector3.forward;

        return Quaternion.AngleAxis(angle, axis);
    }

    public void SetHotbarSlotLabel(int slotIndex)
    {
        if (hotbarSlotLabel == null) { return; }

        if (slotIndex < 0)
        {
            hotbarSlotLabel.SetText(string.Empty);
            hotbarSlotLabel.gameObject.SetActive(false);
            return;
        }

        hotbarSlotLabel.gameObject.SetActive(true);
        hotbarSlotLabel.SetText((slotIndex + 1).ToString());

        SnapOverlayLabelsUnderCornerOccupiedSpaces();
    }

    private void SnapOverlayLabelsUnderCornerOccupiedSpaces()
    {
        if (occupiedSpaceVisuals.Count == 0)
        {
            return;
        }

        RectTransform topLeftOcc = null;
        RectTransform topRightOcc = null;

        float bestTopYForLeft = float.MinValue;
        float bestLeftX = float.MaxValue;

        float bestTopYForRight = float.MinValue;
        float bestRightX = float.MinValue;

        for (int i = 0; i < occupiedSpaceVisuals.Count; i++)
        {
            RectTransform occ = occupiedSpaceVisuals[i];
            if (occ == null) { continue; }

            float left = occ.anchoredPosition.x;
            float top = occ.anchoredPosition.y;
            float right = left + occ.sizeDelta.x;

            // top left occupied visual
            if (top > bestTopYForLeft || (Mathf.Approximately(top, bestTopYForLeft) && left < bestLeftX))
            {
                bestTopYForLeft = top;
                bestLeftX = left;
                topLeftOcc = occ;
            }

            // top right occupied visual
            if (top > bestTopYForRight || (Mathf.Approximately(top, bestTopYForRight) && right > bestRightX))
            {
                bestTopYForRight = top;
                bestRightX = right;
                topRightOcc = occ;
            }
        }

        if (topLeftOcc != null && hotbarSlotLabel != null)
        {
            RectTransform hotbarRt = hotbarSlotLabel.rectTransform;

            hotbarRt.SetParent(topLeftOcc, false);
            hotbarRt.anchorMin = new Vector2(0f, 1f);
            hotbarRt.anchorMax = new Vector2(0f, 1f);
            hotbarRt.pivot = new Vector2(0f, 1f);
            hotbarRt.anchoredPosition = new Vector2(hotbarLabelInset, -labelDownOffset - hotbarLabelInset);
            hotbarRt.SetAsLastSibling();
        }

        if (topRightOcc != null && stackCountLabel != null)
        {
            RectTransform stackRt = stackCountLabel.rectTransform;

            stackRt.SetParent(topRightOcc, false);
            stackRt.anchorMin = new Vector2(1f, 1f);
            stackRt.anchorMax = new Vector2(1f, 1f);
            stackRt.pivot = new Vector2(1f, 1f);
            stackRt.anchoredPosition = new Vector2(-stackLabelInset, -labelDownOffset - stackLabelInset);
            stackRt.SetAsLastSibling();
        }
    }

    // used by inventory hover logic and drag start checks
    public bool IsScreenPointOverOccupiedSpace(Vector2 screenPoint, Camera uiCamera)
    {
        for (int i = 0; i < occupiedSpaceVisuals.Count; i++)
        {
            RectTransform occ = occupiedSpaceVisuals[i];
            if (occ == null) { continue; }

            if (RectTransformUtility.RectangleContainsScreenPoint(occ, screenPoint, uiCamera))
            {
                return true;
            }
        }

        return false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // set up for drag
        if (inv == null || itemInst == null) { return; }

        if (rt == null) { rt = GetComponent<RectTransform>(); }
        if (rootCanvas == null) { rootCanvas = GetComponentInParent<Canvas>(); }

        // only allow dragging if the pointer is actually over an occupied space
        if (!IsScreenPointOverOccupiedSpace(eventData.position, eventData.pressEventCamera))
        {
            return;
        }

        inv.HideContextMenu();
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
        if (inv == null || inv.heldItem != this) { return; }
        if (rt == null || rootCanvas == null) { return; }

        Vector2 delta = eventData.delta / rootCanvas.scaleFactor;
        rt.anchoredPosition += delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // only finish drag if we were actually the held item
        if (inv == null || itemInst == null) { return; }
        if (inv.heldItem != this) { return; }

        Camera uiCam = eventData.pressEventCamera;
        bool shiftHeld = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        // chest item flow
        if (itemInst.isChestItem)
        {
            if (inv.TryTakeChestItemToInventoryFromScreenPoint(itemInst, eventData.position, uiCam))
            {
                inv.heldItem = null;
                inv.FlushPendingChestSnapshot();
                return;
            }

            bool movedChest = inv.TryMoveChestItemFromScreenPoint(itemInst, eventData.position, uiCam);
            if (!movedChest)
            {
                inv.RestoreItemToCellAndRotation(itemInst, startCell, startRotation);

                startAnchoredPos = rt.anchoredPosition;
                itemInst.cell = startCell;
                itemInst.rotation = startRotation;
            }
            else
            {
                startCell = itemInst.cell;
                startRotation = itemInst.rotation;
                startAnchoredPos = rt.anchoredPosition;
            }

            inv.heldItem = null;
            inv.FlushPendingChestSnapshot();
            return;
        }

        // player inventory item flow
        if (inv.TryStoreHeldItemInOpenChestFromScreenPoint(itemInst, eventData.position, uiCam, shiftHeld))
        {
            inv.heldItem = null;
            return;
        }

        if (inv.TryMergeItemIntoHoveredStack(itemInst, eventData.position, uiCam))
        {
            inv.heldItem = null;
            return;
        }

        bool moved = inv.TryMoveItemFromScreenPoint(itemInst, eventData.position, uiCam);
        if (!moved)
        {
            // dragging outside inventory drops the stack
            if (inv.DropItemInstanceToWorld(itemInst, true))
            {
                inv.heldItem = null;
                return;
            }

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