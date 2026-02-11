using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: Inventory multi space style, Inventory has grid and uses INV_Item as the data holder for it.
// Sets up a grid for inventory spaces, any item added goes through multiple checks before finally snapping to a spot on the grid
// grid is its own object and uses grid layout group to align grid spaces
// then items are instantiated into another object using the grid space top left corner world positions into local
// grid doesnt get generated again, as we can just keep it there toggled off using set active for gameobject in player controller
public class INV_Inventory : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("Max amount of spaces in inventory height wise")]
    [SerializeField] private int inventoryGridMaxHeight = 3;
    [Tooltip("Max amount of spaces in inventory width wise")]
    [SerializeField] private int inventoryGridMaxWidth = 10;

    [Tooltip("Grid parent with GridLayoutGroup.")]
    [SerializeField] private RectTransform gridRoot;

    [Tooltip("Parent all item instances will be under.")]
    [SerializeField] private RectTransform itemGridRoot;

    [Tooltip("Single grid cell prefab.")]
    [SerializeField] private GameObject gridCellPrefab;

    [Header("Items")]
    [Tooltip("Item UI prefab (needs to have INV_ItemUI).")]
    [SerializeField] private GameObject itemUIPrefab;

    [Header("Debug")]
    [SerializeField] private bool logPlacement;

    // runtime
    private bool gridGenerated;

    private GridLayoutGroup gridLayoutGroup;
    private Vector2 cellSize;

    // cell transforms
    private RectTransform[,] cellRects;

    // grid occupancy
    private InventoryGridSpace[,] spaces;

    // item instances
    [SerializeField] private readonly List<ItemInstance> items = new List<ItemInstance>();

    [System.Serializable] 
    public class InventoryGridSpace
    {
        public Vector2Int position;
        public bool isOccupied;
        public ItemInstance occupyingItem;
    }

    public class ItemInstance
    {
        public INV_Item data;

        // size in cells
        public Vector2Int size;

        public enum Rotation { Vertical, Horizontal };
        public Rotation rotation;

        // where the item is placed
        public Vector2Int cell;

        // ui
        public RectTransform ui;
        public INV_ItemUI uiHandler;
    }

    private void Start()
    {
        GenerateGrid();
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

        float cellSizeX = gridRoot.sizeDelta.x / inventoryGridMaxWidth;

        // cell size should be an equal square
        cellSize = new Vector2(cellSizeX, cellSizeX);

        // may change later but all ui should likely be scaled depending on screen size
        // will need more logic making sure inventory height doesnt exceed inventory grid y size
        gridLayoutGroup.cellSize = cellSize;

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

        // try find a spot that can fit it
        Vector2Int size = item.ItemGridSize;
        Vector2Int cell;
        if (!LookForOccupyableSpace(size, out cell))
        {
            if (logPlacement) { Debug.Log($"INV, No space for: {item.Name}"); }
            return false;
        }

        // create + place
        ItemInstance inst;
        if (!CreateItemInstance(item, out inst)) { return false; }

        if (!TryPlaceItemAtCell(cell.x, cell.y, inst, false))
        {
            // shouldnt happen since we already searched, but just in case
            if (inst.ui != null) { Destroy(inst.ui.gameObject); }
            items.Remove(inst);
            return false;
        }

        if (logPlacement) { Debug.Log($"INV, Added: {item.Name} at {cell}"); }
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

        // because item pivots are top-left, need the cell's top-left corner, not the center
        Vector3[] corners = new Vector3[4];
        cell.GetWorldCorners(corners);
        Vector3 topLeftWorld = corners[1];
        Vector3 topLeftLocal = itemGridRoot.InverseTransformPoint(topLeftWorld);

        return new Vector2(topLeftLocal.x, topLeftLocal.y);
    }

    // placement
    private bool TryPlaceItemAtCell(int gridX, int gridY, ItemInstance item, bool clearOld)
    {
        if (item == null) { return false; }

        // bounds + occupancy check
        if (!CheckSpaceOccupyable(gridX, gridY, item.size, item))
        {
            if (logPlacement) { Debug.Log($"INV, Blocked placement at ({gridX},{gridY}) for {item.data.Name}"); }
            return false;
        }

        // clear old occupied spaces (so moving works)
        if (clearOld)
        {
            ClearItemOccupancy(item);
        }

        // occupy new spaces
        OccupySpaces(gridX, gridY, item);

        // snap ui
        Vector2 anchored = GetAnchoredPosForCell(gridX, gridY);
        item.ui.anchoredPosition = anchored;

        // remember placed cell
        item.cell = new Vector2Int(gridX, gridY);

        return true;
    }

    private void OccupySpaces(int gridX, int gridY, ItemInstance item)
    {
        for (int oy = 0; oy < item.size.y; oy++)
        {
            for (int ox = 0; ox < item.size.x; ox++)
            {
                int x = gridX + ox;
                int y = gridY + oy;

                InventoryGridSpace s = spaces[x, y];
                s.isOccupied = true;
                s.occupyingItem = item;
            }
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
    private bool LookForOccupyableSpace(Vector2Int size, out Vector2Int foundCell)
    {
        // scan top-left to bottom-right
        for (int y = 0; y < inventoryGridMaxHeight; y++)
        {
            for (int x = 0; x < inventoryGridMaxWidth; x++)
            {
                if (CheckSpaceOccupyable(x, y, size, null))
                {
                    foundCell = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        foundCell = new Vector2Int(-1, -1);
        return false;
    }

    private bool CheckSpaceOccupyable(int gridX, int gridY, Vector2Int size, ItemInstance ignoreItem)
    {
        if (size.x <= 0 || size.y <= 0) { return false; }

        // bounds first
        if (gridX < 0 || gridY < 0) { return false; }
        if (gridX + size.x > inventoryGridMaxWidth) { return false; }
        if (gridY + size.y > inventoryGridMaxHeight) { return false; }

        // occupancy check
        for (int oy = 0; oy < size.y; oy++)
        {
            for (int ox = 0; ox < size.x; ox++)
            {
                int x = gridX + ox;
                int y = gridY + oy;

                InventoryGridSpace s = spaces[x, y];

                if (!s.isOccupied) { continue; }

                // allow overlap with itself when moving
                if (ignoreItem != null && s.occupyingItem == ignoreItem) { continue; }

                return false;
            }
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
        inst.rotation = ItemInstance.Rotation.Vertical; // default it for now, sort out rotation later

        // after creating the instance, force its size to be per item grid size in INV_Item
        inst.size = item.ItemGridSize;

        // cell size (one space) multiplied by item grid size (how many spots its supposed to occupy) for its respective x and y
        Vector2 spacing = gridLayoutGroup != null ? gridLayoutGroup.spacing : Vector2.zero;

        float w = (inst.size.x * cellSize.x) + Mathf.Max(0, inst.size.x - 1) * spacing.x;
        float h = (inst.size.y * cellSize.y) + Mathf.Max(0, inst.size.y - 1) * spacing.y;

        rt.sizeDelta = new Vector2(w, h);

        // enforce top-left pivot/anchors for consistent snapping
        rt.pivot = new Vector2(0f, 1f);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);

        inst.ui = rt;
        inst.uiHandler = ui;

        // init ui handler
        ui.Init(this, inst);

        items.Add(inst);
        instance = inst;
        return true;
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
}
