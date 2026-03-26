using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: Inventory multi space style, Inventory has grid and uses INV_Item as the data holder for it.

// Sets up a grid for inventory spaces, any item added goes through multiple checks before finally snapping to a spot on the grid
// grid is its own object and uses grid layout group to align grid spaces
// then items are instantiated into another object using the grid space top left corner world positions into local
// grid doesnt get generated again, as we can just keep it there toggled off using set active for gameobject in player controller

// TODO:
// redo grid generation to be set up in here via cell space size --
// hotbar works like item stays in inventory but is hotkeyed, makes inventory management more of a challenge --
// this should hold chest logic too, network inventory will pull whats in there from host with item instance and grid space to set up grid properly
public class INV_Inventory : MonoBehaviour
{
    [Header("Inventory Menu")]
    [Tooltip("Make sure the inventory menu root is active, so we can set up the grid then close the menu after.")]
    [SerializeField] private RectTransform inventoryMenuRoot;
    [Tooltip("Grid parent with GridLayoutGroup.")]
    [SerializeField] private RectTransform gridRoot;
    [Tooltip("Parent all item instances will be under.")]
    [SerializeField] private RectTransform itemGridRoot;

    [Header("Grid")]
    [Tooltip("Max amount of spaces in inventory height wise.")]
    [SerializeField] private int inventoryGridMaxHeight = 4;
    [Tooltip("Max amount of spaces in inventory width wise.")]
    [SerializeField] private int inventoryGridMaxWidth = 10;

    [Tooltip("Size of the grid space we will use to set the size of the grid holding object.")]
    [SerializeField] private int inventoryGridSpaceSize = 40;
    [Tooltip("Spacing between grid slots")]
    [SerializeField] private Vector2 inventoryGridSpacing = new Vector2(2, 2);

    [Tooltip("Single grid cell prefab.")]
    [SerializeField] private GameObject gridCellPrefab;

    [Header("Items")]
    [Tooltip("Item UI prefab (needs to have INV_ItemUI).")]
    [SerializeField] private GameObject itemUIPrefab;

    [Tooltip("Prefab used behind items to show occupied spaces.")]
    [SerializeField] private GameObject occupiedSpacePrefab;

    [Tooltip("Item prefab we will use to drop an item with (needs to have INV_ItemDrop).")]
    [SerializeField] private GameObject itemPrefab;

    [Header("Hover Tooltip")]
    [SerializeField] private INV_ItemHoverTooltip hoverTooltip;
    [SerializeField] private float hoverTooltipDelay = 0.4f;

    private INV_ItemUI lastHoverItem;
    private float hoverTimer;

    [Tooltip("Transform we will spawn dropped items from.")]
    public Transform dropItemTransform;

    [Header("Debug")]
    [SerializeField] private bool logPlacement;

    // runtime
    private bool gridGenerated;
    public INV_ItemUI heldItem;
    public INV_ItemUI hoverItem;

    private GridLayoutGroup gridLayoutGroup;
    private Vector2 cellSize;

    // cell transforms
    private RectTransform[,] cellRects;

    // grid occupancy
    private InventoryGridSpace[,] spaces;

    // item instances
    [SerializeField] private List<ItemInstance> items = new List<ItemInstance>();
    public List<ItemInstance> Items => items;
    public GameObject ItemPrefab => itemPrefab;
    public Vector2 CellSize => cellSize;
    public Vector2 GridSpacing => gridLayoutGroup != null ? gridLayoutGroup.spacing : inventoryGridSpacing;
    public GameObject OccupiedSpacePrefab => occupiedSpacePrefab;

    [System.Serializable]
    public class InventoryGridSpace
    {
        public Vector2Int position;
        public bool isOccupied;
        public ItemInstance occupyingItem;
    }

    [System.Serializable]
    public class ItemInstance // used for saving items and inventory
    {
        public INV_Item data;

        // rectangular size in cells for UI and bounds
        public Vector2Int size;

        public enum Rotation { Up, Right, Down, Left };
        public Rotation rotation;

        // where the item is placed
        public Vector2Int cell;

        // occupancy, offsets relative from cell
        public List<Vector2Int> occupiedOffsets = new List<Vector2Int>();

        // original shape size before rotation
        public Vector2Int shapeSize;

        // item specifics
        public float maxDurability;
        public float currentDurability;

        // stack
        public int quantity = 1;

        // ui
        public RectTransform ui;
        public INV_ItemUI uiHandler;
    }

    private void Start()
    {
        GenerateGrid();
        if (inventoryMenuRoot != null) { inventoryMenuRoot.gameObject.SetActive(false); }
    }

    private void Update()
    {
        // if menu is closed, don't do UI hover checks.
        if (inventoryMenuRoot != null && !inventoryMenuRoot.gameObject.activeInHierarchy)
        {
            hoverItem = null;
            lastHoverItem = null;
            hoverTimer = 0f;

            if (hoverTooltip != null)
            {
                hoverTooltip.HideImmediate();
            }

            return;
        }

        if (inventoryMenuRoot.gameObject.activeInHierarchy)
        {
            // update all items when inventory menu open
            foreach (ItemInstance item in items)
            {
                item.uiHandler.ApplyUpdatedVisuals();
            }
        }

        // hover detection
        UpdateHoverItem();

        // tooltip
        UpdateHoverTooltip();
    }

    // generates the grid to use for inventory
    private void GenerateGrid()
    {
        if (gridGenerated) { return; }
        if (gridRoot == null || gridCellPrefab == null)
        {
            Debug.LogWarning("Inventory missing grid references.");
            return;
        }

        gridLayoutGroup = gridRoot.GetComponent<GridLayoutGroup>();
        if (gridLayoutGroup == null)
        {
            Debug.LogWarning("Inventory gridRoot needs GridLayoutGroup.");
            return;
        }

        // apply spacing and cell size
        gridLayoutGroup.spacing = inventoryGridSpacing;
        cellSize = new Vector2(inventoryGridSpaceSize, inventoryGridSpaceSize);
        gridLayoutGroup.cellSize = cellSize;

        // set grid size based on our grid size and spacing as one space multiplied by out height and width respectively
        gridRoot.sizeDelta = new Vector2
            ((inventoryGridSpaceSize + inventoryGridSpacing.x) * inventoryGridMaxWidth,
            (inventoryGridSpaceSize + inventoryGridSpacing.y) * inventoryGridMaxHeight);

        // remove last spacing added height and width so it sits flush with the edge
        gridRoot.sizeDelta -= inventoryGridSpacing;

        // vice versa
        itemGridRoot.sizeDelta = new Vector2
            ((inventoryGridSpaceSize + inventoryGridSpacing.x) * inventoryGridMaxWidth,
            (inventoryGridSpaceSize + inventoryGridSpacing.y) * inventoryGridMaxHeight);

        itemGridRoot.sizeDelta -= inventoryGridSpacing;

        spaces = new InventoryGridSpace[inventoryGridMaxWidth, inventoryGridMaxHeight];
        cellRects = new RectTransform[inventoryGridMaxWidth, inventoryGridMaxHeight];

        // clear existing children if any
        for (int i = gridRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(gridRoot.GetChild(i).gameObject);
        }

        // generates all spaces
        for (int y = 0; y < inventoryGridMaxHeight; y++)
        {
            for (int x = 0; x < inventoryGridMaxWidth; x++)
            {
                GameObject cell = Instantiate(gridCellPrefab, gridRoot);
                cell.name = $"Cell_X:{x},Y:{y}";

                RectTransform rt = cell.GetComponent<RectTransform>();
                cellRects[x, y] = rt;

                InventoryGridSpace s = new InventoryGridSpace();
                s.position = new Vector2Int(x, y);
                s.isOccupied = false;
                s.occupyingItem = null;

                spaces[x, y] = s;
            }
        }

        // make sure the roots are consistent with top left pivot
        if (itemGridRoot != null)
        {
            itemGridRoot.pivot = new Vector2(0f, 1f);
            itemGridRoot.anchorMin = new Vector2(0f, 1f);
            itemGridRoot.anchorMax = new Vector2(0f, 1f);
        }

        gridGenerated = true;
    }

    // makes sure grid is setup correctly before attempting to update it
    private void EnsureGrid()
    {
        if (gridGenerated) { return; }
        GenerateGrid();
    }

    // pickup, add, move items

    // used by drop/pickup scripts
    public bool TryAddItem(INV_Item item)
    {
        EnsureGrid();
        if (item == null) { return false; }

        // try stack identical items first
        if (TryStackItem(item, 1))
        {
            if (logPlacement) { Debug.Log($"INV, Stacked: {item.Name}"); }

            INV_HotBar hotBar = GetComponent<INV_HotBar>();
            if (hotBar != null)
            {
                hotBar.RefreshAllVisuals();
            }

            return true;
        }

        // create instance first so we can use its shape
        ItemInstance inst;
        if (!CreateItemInstance(item, out inst)) { return false; }

        // try find a spot that can fit it
        Vector2Int cell;
        if (!LookForOccupyableSpace(inst, out cell))
        {
            if (logPlacement) { Debug.Log($"INV, No space for: {item.Name}"); }

            // cleanup if we failed
            if (inst.ui != null) { Destroy(inst.ui.gameObject); }
            items.Remove(inst);
            return false;
        }

        // place
        if (!TryPlaceItemAtCell(cell.x, cell.y, inst, false))
        {
            // shouldnt happen since we already searched, but just in case
            if (inst.ui != null) { Destroy(inst.ui.gameObject); }
            items.Remove(inst);
            return false;
        }

        if (logPlacement) { Debug.Log($"INV, Added: {item.Name} at {cell}"); }

        INV_HotBar hotBar2 = GetComponent<INV_HotBar>();
        if (hotBar2 != null)
        {
            hotBar2.RefreshAllVisuals();
        }

        return true;
    }

    // called by INV_ItemUI on drop
    public bool TryMoveItemFromScreenPoint(ItemInstance item, Vector2 screenPoint, Camera uiCamera)
    {
        EnsureGrid();
        if (item == null || item.ui == null) { return false; }

        Vector2Int cell;
        if (!TryGetCellFromScreenPoint(screenPoint, uiCamera, out cell))
        {
            return false;
        }

        // try place at that cell
        return TryPlaceItemAtCell(cell.x, cell.y, item, true);
    }

    // used by INV_ItemUI for snap back
    public Vector2 GetAnchoredPosForCell(int gridX, int gridY)
    {
        EnsureGrid();

        gridX = Mathf.Clamp(gridX, 0, inventoryGridMaxWidth - 1);
        gridY = Mathf.Clamp(gridY, 0, inventoryGridMaxHeight - 1);

        RectTransform cell = cellRects[gridX, gridY];
        if (cell == null) { return Vector2.zero; }

        // convert gridRoot local position to itemGridRoot local position
        Vector3 world = cell.TransformPoint(cell.rect.center);
        Vector3 local = itemGridRoot.InverseTransformPoint(world);

        // because item pivots are top left, need the cell's top left corner, not the center
        Vector3[] corners = new Vector3[4];
        cell.GetWorldCorners(corners);
        Vector3 topLeftWorld = corners[1];
        Vector3 topLeftLocal = itemGridRoot.InverseTransformPoint(topLeftWorld);

        return new Vector2(topLeftLocal.x, topLeftLocal.y);
    }

    // snaps an items rect so the correct corner matches the target cell anchored position
    // vertical uses top-left, horizontal uses bottom-left
    private void SnapItemToTopLeft(ItemInstance item, Vector2 targetCellTopLeftAnchored)
    {
        if (item == null || item.ui == null) { return; }

        RectTransform rect = item.ui;

        // keep these consistent for snapping
        rect.pivot = new Vector2(0f, 1f);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);

        // get the rect corners in local space (relative to its pivot/transform)
        Vector3[] corners = new Vector3[4];
        rect.GetLocalCorners(corners);

        Vector2 offset = corners[1];
        rect.anchoredPosition = targetCellTopLeftAnchored + offset;
    }

    // placement
    private bool TryPlaceItemAtCell(int gridX, int gridY, ItemInstance item, bool clearOld)
    {
        if (item == null) { return false; }

        // bounds and occupancy check
        if (!CheckSpaceOccupyable(gridX, gridY, item, item))
        {
            if (logPlacement) { Debug.Log($"INV, Blocked placement at ({gridX},{gridY}) for {item.data.Name}"); }
            return false;
        }

        // clear old occupied spaces so moving works
        if (clearOld)
        {
            ClearItemOccupancy(item);
        }

        // occupy new spaces
        OccupySpaces(gridX, gridY, item);

        // snap ui
        Vector2 anchored = GetAnchoredPosForCell(gridX, gridY);
        SnapItemToTopLeft(item, anchored);

        // remember placed cell
        item.cell = new Vector2Int(gridX, gridY);

        return true;
    }

    private void OccupySpaces(int gridX, int gridY, ItemInstance item)
    {
        // only occupy offsets that are marked + in the shape
        for (int i = 0; i < item.occupiedOffsets.Count; i++)
        {
            Vector2Int off = item.occupiedOffsets[i];

            int x = gridX + off.x;
            int y = gridY + off.y;

            InventoryGridSpace s = spaces[x, y];
            s.isOccupied = true;
            s.occupyingItem = item;
        }
    }

    private void ClearItemOccupancy(ItemInstance item)
    {
        for (int y = 0; y < inventoryGridMaxHeight; y++)
        {
            for (int x = 0; x < inventoryGridMaxWidth; x++)
            {
                InventoryGridSpace s = spaces[x, y];
                if (s.occupyingItem == item)
                {
                    s.isOccupied = false;
                    s.occupyingItem = null;
                }
            }
        }
    }

    // checks if item can occupy space, called by dropped item using interact
    private bool LookForOccupyableSpace(ItemInstance item, out Vector2Int foundCell)
    {
        // scan top left to bottom right
        for (int y = 0; y < inventoryGridMaxHeight; y++)
        {
            for (int x = 0; x < inventoryGridMaxWidth; x++)
            {
                if (CheckSpaceOccupyable(x, y, item, null))
                {
                    foundCell = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        foundCell = new Vector2Int(-1, -1);
        return false;
    }

    private bool CheckSpaceOccupyable(int gridX, int gridY, ItemInstance itemToPlace, ItemInstance ignoreItem)
    {
        if (itemToPlace == null) { return false; }
        if (itemToPlace.size.x <= 0 || itemToPlace.size.y <= 0) { return false; }

        // bounds first use bounding box size so we don't set outside grid
        if (gridX < 0 || gridY < 0) { return false; }
        if (gridX + itemToPlace.size.x > inventoryGridMaxWidth) { return false; }
        if (gridY + itemToPlace.size.y > inventoryGridMaxHeight) { return false; }

        // occupancy check
        // only check occupied offsets
        for (int i = 0; i < itemToPlace.occupiedOffsets.Count; i++)
        {
            Vector2Int off = itemToPlace.occupiedOffsets[i];

            int x = gridX + off.x;
            int y = gridY + off.y;

            InventoryGridSpace s = spaces[x, y];

            if (!s.isOccupied) { continue; }

            // allow overlap with itself when moving
            if (ignoreItem != null && s.occupyingItem == ignoreItem) { continue; }

            return false;
        }

        return true;
    }

    // creates iteminstance to use in inventory
    private bool CreateItemInstance(INV_Item item, out ItemInstance instance)
    {
        instance = null;

        if (itemUIPrefab == null || itemGridRoot == null)
        {
            Debug.LogWarning("Inventory missing item UI references.");
            return false;
        }

        GameObject go = Instantiate(itemUIPrefab, itemGridRoot);
        go.name = $"ItemUI_{item.Name}";

        RectTransform rt = go.GetComponent<RectTransform>();
        INV_ItemUI ui = go.GetComponent<INV_ItemUI>();

        if (rt == null || ui == null)
        {
            Debug.LogWarning("Item UI prefab needs RectTransform + INV_ItemUI.");
            Destroy(go);
            return false;
        }

        ItemInstance inst = new ItemInstance();
        inst.data = item;
        inst.rotation = ItemInstance.Rotation.Up;
        inst.quantity = 1;

        // build occupancy offsets from the item shape
        BuildShapeForInstance(inst, ItemInstance.Rotation.Up);

        // enforce top left pivot/anchors for consistent snapping
        rt.pivot = new Vector2(0f, 1f);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);

        inst.ui = rt;
        inst.uiHandler = ui;

        // init ui handler
        ui.Init(this, inst);

        // apply correct UI size
        RebuildItemUISize(inst);

        if (inst.uiHandler != null)
        {
            inst.uiHandler.RebuildOccupiedSpaceVisuals();
            inst.uiHandler.ApplyUpdatedVisuals();
        }

        items.Add(inst);
        instance = inst;
        return true;
    }

    // builds occupied offsets and bounding size based on item shape and rotation
    private void BuildShapeForInstance(ItemInstance inst, ItemInstance.Rotation rot)
    {
        inst.occupiedOffsets.Clear();

        List<string> shape = inst.data != null ? inst.data.InventorySpaceShape : null;

        // if no shape then treat as filled rectangle
        if (shape == null || shape.Count == 0)
        {
            Vector2Int s = inst.data != null ? inst.data.ItemGridSize : Vector2Int.one;
            inst.shapeSize = s;

            List<Vector2Int> rawRect = new List<Vector2Int>();
            for (int y = 0; y < s.y; y++)
            {
                for (int x = 0; x < s.x; x++)
                {
                    rawRect.Add(new Vector2Int(x, y));
                }
            }

            ApplyRotatedOffsets(inst, rawRect, s, rot);
            return;
        }

        // normalize width/height
        int h = shape.Count;
        int w = 0;
        for (int i = 0; i < shape.Count; i++)
        {
            if (string.IsNullOrEmpty(shape[i])) { continue; }
            w = Mathf.Max(w, shape[i].Length);
        }

        // save original shape size
        inst.shapeSize = new Vector2Int(Mathf.Max(1, w), Mathf.Max(1, h));

        // collect + cells in up orientation first
        List<Vector2Int> raw = new List<Vector2Int>();
        for (int y = 0; y < h; y++)
        {
            string row = shape[y];
            if (string.IsNullOrEmpty(row)) { row = ""; }

            for (int x = 0; x < w; x++)
            {
                char c = (x < row.Length) ? row[x] : '-';
                if (c == '+')
                {
                    raw.Add(new Vector2Int(x, y));
                }
            }
        }

        ApplyRotatedOffsets(inst, raw, inst.shapeSize, rot);
    }

    private void ApplyRotatedOffsets(ItemInstance inst, List<Vector2Int> rawOffsets, Vector2Int baseSize, ItemInstance.Rotation rot)
    {
        List<Vector2Int> rotated = new List<Vector2Int>(rawOffsets);

        int width = baseSize.x;
        int height = baseSize.y;

        int turns = 0;
        switch (rot)
        {
            case ItemInstance.Rotation.Up: turns = 0; break;
            case ItemInstance.Rotation.Right: turns = 1; break;
            case ItemInstance.Rotation.Down: turns = 2; break;
            case ItemInstance.Rotation.Left: turns = 3; break;
        }

        // rotate clockwise in 90 degree steps
        for (int t = 0; t < turns; t++)
        {
            List<Vector2Int> next = new List<Vector2Int>(rotated.Count);

            for (int i = 0; i < rotated.Count; i++)
            {
                Vector2Int p = rotated[i];

                // clockwise rotate inside current bounds
                Vector2Int r = new Vector2Int(p.y, width - 1 - p.x);
                next.Add(r);
            }

            rotated = next;

            int oldWidth = width;
            width = height;
            height = oldWidth;
        }

        // normalize back to 0 based offsets just in case
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        for (int i = 0; i < rotated.Count; i++)
        {
            Vector2Int p = rotated[i];
            if (p.x < minX) { minX = p.x; }
            if (p.y < minY) { minY = p.y; }
            if (p.x > maxX) { maxX = p.x; }
            if (p.y > maxY) { maxY = p.y; }
        }

        if (rotated.Count == 0)
        {
            inst.size = Vector2Int.one;
            return;
        }

        for (int i = 0; i < rotated.Count; i++)
        {
            Vector2Int p = rotated[i];
            inst.occupiedOffsets.Add(new Vector2Int(p.x - minX, p.y - minY));
        }

        inst.size = new Vector2Int((maxX - minX) + 1, (maxY - minY) + 1);
    }

    // recalculates the UI size from instance.size so rotation can update it too
    private void RebuildItemUISize(ItemInstance inst)
    {
        if (inst == null || inst.ui == null) { return; }

        Vector2 spacing = gridLayoutGroup != null ? gridLayoutGroup.spacing : Vector2.zero;

        float w = (inst.size.x * cellSize.x) + Mathf.Max(0, inst.size.x - 1) * spacing.x;
        float h = (inst.size.y * cellSize.y) + Mathf.Max(0, inst.size.y - 1) * spacing.y;

        inst.ui.sizeDelta = new Vector2(w, h);

        if (inst.uiHandler != null)
        {
            inst.uiHandler.RebuildOccupiedSpaceVisuals();
        }
    }

    private bool TryStackItem(INV_Item item, int amount)
    {
        if (item == null) { return false; }
        if (amount <= 0) { return false; }
        if (!item.Stackable) { return false; }

        int maxStack = item.MaxStack;

        for (int i = 0; i < items.Count; i++)
        {
            ItemInstance inst = items[i];
            if (inst == null || inst.data == null) { continue; }

            if (inst.data.ItemID != item.ItemID) { continue; }
            if (!inst.data.Stackable) { continue; }
            if (inst.quantity >= maxStack) { continue; }

            int room = maxStack - inst.quantity;
            int add = Mathf.Min(room, amount);

            if (add <= 0) { continue; }

            inst.quantity += add;

            if (inst.uiHandler != null)
            {
                inst.uiHandler.ApplyUpdatedVisuals();
            }

            return true;
        }

        return false;
    }

    // drop logic
    private bool TryGetCellFromScreenPoint(Vector2 screenPoint, Camera uiCamera, out Vector2Int cell)
    {
        // find which cell rect contains the mouse
        // inventory isnt big so looping through each should be fine
        for (int y = 0; y < inventoryGridMaxHeight; y++)
        {
            for (int x = 0; x < inventoryGridMaxWidth; x++)
            {
                RectTransform c = cellRects[x, y];
                if (c == null) { continue; }

                if (RectTransformUtility.RectangleContainsScreenPoint(c, screenPoint, uiCamera))
                {
                    cell = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        cell = new Vector2Int(-1, -1);
        return false;
    }

    // Hover tracking
    private void UpdateHoverItem()
    {
        // if you're dragging something, hover is not used
        if (heldItem != null)
        {
            hoverItem = null;
            return;
        }

        if (Mouse.current == null)
        {
            hoverItem = null;
            return;
        }

        // read mouse position
        Vector2 mouse = Mouse.current.position.ReadValue();
        Camera uiCam = Camera.main;

        INV_ItemUI found = null;
        int bestSibling = int.MinValue;

        // check every item occupied space for mouse hit. choose the top most in hierarchy.
        for (int i = 0; i < items.Count; i++)
        {
            ItemInstance inst = items[i];
            if (inst == null || inst.ui == null || inst.uiHandler == null) { continue; }

            if (inst.uiHandler.IsScreenPointOverOccupiedSpace(mouse, uiCam))
            {
                int sib = inst.ui.GetSiblingIndex();
                if (sib >= bestSibling)
                {
                    bestSibling = sib;
                    found = inst.uiHandler;
                }
            }
        }

        hoverItem = found;
    }
    private void UpdateHoverTooltip()
    {
        if (hoverTooltip == null)
        {
            return;
        }

        // dont show while dragging
        if (heldItem != null)
        {
            hoverTooltip.HideImmediate();
            lastHoverItem = null;
            hoverTimer = 0f;
            return;
        }

        if (hoverItem == null || hoverItem.Instance == null || hoverItem.Instance.data == null)
        {
            hoverTooltip.HideImmediate();
            lastHoverItem = null;
            hoverTimer = 0f;
            return;
        }

        if (hoverItem != lastHoverItem)
        {
            lastHoverItem = hoverItem;
            hoverTimer = 0f;
            hoverTooltip.HideImmediate();
            return;
        }

        hoverTimer += Time.deltaTime;

        if (hoverTimer < hoverTooltipDelay)
        {
            return;
        }

        hoverTooltip.Show(hoverItem.Instance.data);
        hoverTooltip.RefreshPosition();
    }

    // Rotate held item
    public bool RotateItem()
    {
        // rotate should work via object being held
        if (heldItem == null) { return false; }

        ItemInstance inst = heldItem.Instance;
        if (inst == null || inst.ui == null) { return false; }

        // clear current occupied spaces before trying new shape
        ClearItemOccupancy(inst);

        // cycle through 4 orientations
        switch (inst.rotation)
        {
            case ItemInstance.Rotation.Up:
                inst.rotation = ItemInstance.Rotation.Right;
                break;

            case ItemInstance.Rotation.Right:
                inst.rotation = ItemInstance.Rotation.Down;
                break;

            case ItemInstance.Rotation.Down:
                inst.rotation = ItemInstance.Rotation.Left;
                break;

            default:
                inst.rotation = ItemInstance.Rotation.Up;
                break;
        }

        // rebuild offsets and bounding size from shape for that rotation
        BuildShapeForInstance(inst, inst.rotation);

        // rebuild UI size to match new size
        RebuildItemUISize(inst);

        if (inst.uiHandler != null)
        {
            inst.uiHandler.ApplyUpdatedVisuals();
        }

        if (logPlacement) { Debug.Log($"INV, Rotated held item: {inst.data.Name}, size: {inst.size}, rotation: {inst.rotation}"); }
        return true;
    }

    public bool DropHoverItem()
    {
        if (hoverItem == null) { return false; }

        ItemInstance inst = hoverItem.Instance;
        if (inst == null) { return false; }

        // cache item id before we destroy local ui/state
        string itemId = inst.data != null ? inst.data.ItemID : string.Empty;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogError("Cannot drop item with empty ItemID.");
            return false;
        }

        // if stacked, drop one and keep the rest
        if (inst.quantity > 1)
        {
            inst.quantity--;

            if (inst.uiHandler != null)
            {
                inst.uiHandler.ApplyUpdatedVisuals();
            }

            INV_PlayerInventoryNet netInvStack = GetComponentInParent<INV_PlayerInventoryNet>();
            if (netInvStack == null)
            {
                Debug.LogError("Could not find INV_PlayerInventoryNet in parent.");
                hoverItem = null;
                return false;
            }

            netInvStack.RequestDropItem(itemId);
            hoverItem = null;
            return true;
        }

        // clear hotbar refs before we remove the item
        INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
        if (hotBar != null)
        {
            hotBar.ClearReferencesToItem(inst);
        }

        // remove from grid and list
        ClearItemOccupancy(inst);
        items.Remove(inst);

        // destroy UI
        if (inst.ui != null)
        {
            Destroy(inst.ui.gameObject);
        }

        // ask player net bridge to spawn the world drop on server
        INV_PlayerInventoryNet netInv = GetComponentInParent<INV_PlayerInventoryNet>();
        if (netInv == null)
        {
            Debug.LogError("Could not find INV_PlayerInventoryNet in parent.");
            hoverItem = null;
            return false;
        }

        netInv.RequestDropItem(itemId);

        hoverItem = null;
        return true;
    }

    public void RestoreItemToCellAndRotation(ItemInstance item, Vector2Int cell, ItemInstance.Rotation rotation)
    {
        EnsureGrid();
        if (item == null || item.ui == null) { return; }

        // clear whatever state it currently has
        ClearItemOccupancy(item);

        // restore shape / rotation
        item.rotation = rotation;
        BuildShapeForInstance(item, item.rotation);
        RebuildItemUISize(item);

        if (item.uiHandler != null)
        {
            item.uiHandler.ApplyUpdatedVisuals();
        }

        // restore occupied spaces
        OccupySpaces(cell.x, cell.y, item);

        // restore snap position
        Vector2 anchored = GetAnchoredPosForCell(cell.x, cell.y);
        SnapItemToTopLeft(item, anchored);

        item.cell = cell;
    }

    // crafting
    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        int total = 0;

        for (int i = 0; i < items.Count; i++)
        {
            ItemInstance inst = items[i];
            if (inst == null || inst.data == null) { continue; }

            if (inst.data.ItemID != itemId) { continue; }

            total += Mathf.Max(1, inst.quantity);
        }

        return total;
    }

    public bool HasItemAmount(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId)) { return false; }
        if (amount <= 0) { return true; }

        return GetItemCount(itemId) >= amount;
    }

    public bool CanAddItem(INV_Item item)
    {
        EnsureGrid();
        if (item == null) { return false; }

        // stacking first
        if (item.Stackable)
        {
            int maxStack = item.MaxStack;

            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance inst = items[i];
                if (inst == null || inst.data == null) { continue; }

                if (inst.data.ItemID != item.ItemID) { continue; }
                if (!inst.data.Stackable) { continue; }
                if (inst.quantity < maxStack)
                {
                    return true;
                }
            }
        }

        // otherwise see if a fresh instance could fit
        ItemInstance temp = new ItemInstance();
        temp.data = item;
        temp.rotation = ItemInstance.Rotation.Up;
        temp.quantity = 1;

        BuildShapeForInstance(temp, ItemInstance.Rotation.Up);

        Vector2Int foundCell;
        return LookForOccupyableSpace(temp, out foundCell);
    }

    // used by crafting
    public bool RemoveItemAmount(string itemId, int amount)
    {
        EnsureGrid();

        if (string.IsNullOrWhiteSpace(itemId)) { return false; }
        if (amount <= 0) { return true; }

        if (!HasItemAmount(itemId, amount))
        {
            return false;
        }

        int remaining = amount;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            ItemInstance inst = items[i];
            if (inst == null || inst.data == null) { continue; }

            if (inst.data.ItemID != itemId) { continue; }

            int take = Mathf.Min(inst.quantity, remaining);
            inst.quantity -= take;
            remaining -= take;

            if (inst.quantity <= 0)
            {
                INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
                if (hotBar != null)
                {
                    hotBar.ClearReferencesToItem(inst);
                }

                ClearItemOccupancy(inst);
                items.RemoveAt(i);

                if (inst.ui != null)
                {
                    Destroy(inst.ui.gameObject);
                }
            }
            else if (inst.uiHandler != null)
            {
                inst.uiHandler.ApplyUpdatedVisuals();
            }

            if (remaining <= 0)
            {
                INV_HotBar hotBar = GetComponent<INV_HotBar>();
                if (hotBar != null)
                {
                    hotBar.RefreshAllVisuals();
                }

                return true;
            }
        }

        return remaining <= 0;
    }
}