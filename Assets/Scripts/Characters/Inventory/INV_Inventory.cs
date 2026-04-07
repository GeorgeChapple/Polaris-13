using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: Inventory multi space style, Inventory has grid and uses INV_Item as the data holder for it.

// Sets up a grid for inventory spaces, any item added goes through multiple checks before finally snapping to a spot on the grid
// grid is its own object and uses grid layout group to align grid spaces
// then items are instantiated into another object using the grid space top left corner world positions into local
// player inventory and chest inventory both use the same internal grid setup / placement functions now
public class INV_Inventory : MonoBehaviour
{
    [Header("Inventory Menu")]
    [Tooltip("Make sure the inventory menu root is active, so we can set up the grid then close the menu after.")]
    [SerializeField] private RectTransform inventoryMenuRoot;

    [Header("Player Inventory Grid")]
    [SerializeField] private GridRefs playerGrid = new GridRefs();

    [Header("Chest Inventory Grid")]
    [SerializeField] private GridRefs chestGrid = new GridRefs();

    [Header("Grid Shared Settings")]
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

    [Header("Context Menu")]
    [SerializeField] private INV_ItemContextMenu itemContextMenu;

    [Tooltip("Transform we will spawn dropped items from.")]
    public Transform dropItemTransform;

    [Header("Debug")]
    [SerializeField] private bool logPlacement;

    private INV_ItemUI lastHoverItem;
    private float hoverTimer;

    // runtime
    public INV_ItemUI heldItem;
    public INV_ItemUI hoverItem;

    private Vector2 cellSize;
    private GridRuntime playerRuntime = new GridRuntime();
    private GridRuntime chestRuntime = new GridRuntime();

    // currently opened chest for this player
    private INV_Chest activeChest;

    // if a chest refresh comes in while dragging a chest item, hold it until drag ends
    private List<INV_Chest.ChestItemData> pendingChestSnapshot;
    private INV_Chest pendingChestSnapshotSource;

    public List<ItemInstance> Items => playerRuntime.items;
    public INV_Chest ActiveChest => activeChest;
    public GameObject ItemPrefab => itemPrefab;
    public Vector2 CellSize => cellSize;
    public Vector2 GridSpacing => inventoryGridSpacing;
    public GameObject OccupiedSpacePrefab => occupiedSpacePrefab;

    public enum ContextActionType
    {
        Use,
        DropOne,
        DropStack,
        AssignHotbar1,
        AssignHotbar2,
        AssignHotbar3,
        AssignHotbar4,
        AssignSelectedHotbar,
        TakeOne,
        TakeStack
    }

    [System.Serializable]
    public class GridRefs
    {
        [Tooltip("Optional root object for this panel. Chest uses this so it can be toggled on/off.")]
        public GameObject panelRoot;

        [Tooltip("Grid parent with GridLayoutGroup.")]
        public RectTransform gridRoot;

        [Tooltip("Parent all item instances will be under.")]
        public RectTransform itemRoot;

        [Tooltip("Max amount of spaces in inventory height wise.")]
        public int maxHeight = 4;

        [Tooltip("Max amount of spaces in inventory width wise.")]
        public int maxWidth = 10;
    }

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

        public enum Rotation { Up, Right, Down, Left }
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

        // runtime
        public bool isChestItem;
        public string chestItemUniqueId;
        public string inventoryItemUniqueId;
    }

    private class GridRuntime
    {
        public bool generated;
        public GridLayoutGroup layout;
        public RectTransform[,] cellRects;
        public InventoryGridSpace[,] spaces;
        public readonly List<ItemInstance> items = new List<ItemInstance>();
    }

    private void Start()
    {
        cellSize = new Vector2(inventoryGridSpaceSize, inventoryGridSpaceSize);

        GenerateGrid(playerGrid, playerRuntime);
        GenerateGrid(chestGrid, chestRuntime);

        if (itemContextMenu != null)
        {
            itemContextMenu.Init(this);
            itemContextMenu.HideImmediate();
        }

        if (inventoryMenuRoot != null)
        {
            inventoryMenuRoot.gameObject.SetActive(false);
        }

        if (chestGrid.panelRoot != null)
        {
            chestGrid.panelRoot.SetActive(false);
        }
    }

    private void Update()
    {
        // if menu is closed, don't do UI hover checks.
        if (inventoryMenuRoot != null && !inventoryMenuRoot.gameObject.activeInHierarchy)
        {
            hoverItem = null;
            lastHoverItem = null;
            hoverTimer = 0f;

            if (activeChest != null)
            {
                CloseChestView();
            }

            if (hoverTooltip != null)
            {
                hoverTooltip.HideImmediate();
            }

            HideContextMenu();

            pendingChestSnapshot = null;
            pendingChestSnapshotSource = null;

            return;
        }

        RefreshRuntimeVisuals(playerRuntime);
        RefreshRuntimeVisuals(chestRuntime);

        UpdateHoverItem();
        UpdateHoverTooltip();
    }

    private void RefreshRuntimeVisuals(GridRuntime runtime)
    {
        for (int i = 0; i < runtime.items.Count; i++)
        {
            ItemInstance item = runtime.items[i];
            if (item?.uiHandler == null)
            {
                continue;
            }

            item.uiHandler.ApplyUpdatedVisuals();
        }
    }

    // generates a grid to use for this inventory type
    private void GenerateGrid(GridRefs refs, GridRuntime runtime)
    {
        if (runtime.generated || refs.gridRoot == null || refs.itemRoot == null || gridCellPrefab == null)
        {
            return;
        }

        runtime.layout = refs.gridRoot.GetComponent<GridLayoutGroup>();
        if (runtime.layout == null)
        {
            return;
        }

        runtime.layout.spacing = inventoryGridSpacing;
        runtime.layout.cellSize = cellSize;

        Vector2 fullSize = new Vector2
        (
            (inventoryGridSpaceSize + inventoryGridSpacing.x) * refs.maxWidth,
            (inventoryGridSpaceSize + inventoryGridSpacing.y) * refs.maxHeight
        ) - inventoryGridSpacing;

        refs.gridRoot.sizeDelta = fullSize;
        refs.itemRoot.sizeDelta = fullSize;

        refs.itemRoot.pivot = new Vector2(0f, 1f);
        refs.itemRoot.anchorMin = new Vector2(0f, 1f);
        refs.itemRoot.anchorMax = new Vector2(0f, 1f);

        runtime.spaces = new InventoryGridSpace[refs.maxWidth, refs.maxHeight];
        runtime.cellRects = new RectTransform[refs.maxWidth, refs.maxHeight];

        for (int i = refs.gridRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(refs.gridRoot.GetChild(i).gameObject);
        }

        for (int y = 0; y < refs.maxHeight; y++)
        {
            for (int x = 0; x < refs.maxWidth; x++)
            {
                GameObject cell = Instantiate(gridCellPrefab, refs.gridRoot);
                cell.name = $"{refs.gridRoot.name}_Cell_X:{x},Y:{y}";

                runtime.cellRects[x, y] = cell.GetComponent<RectTransform>();
                runtime.spaces[x, y] = new InventoryGridSpace
                {
                    position = new Vector2Int(x, y),
                    isOccupied = false,
                    occupyingItem = null
                };
            }
        }

        runtime.generated = true;
    }

    // makes sure the correct grid is generated before we do anything with it
    private void EnsureGrid(bool chest)
    {
        if (chest)
        {
            GenerateGrid(chestGrid, chestRuntime);
        }
        else
        {
            GenerateGrid(playerGrid, playerRuntime);
        }
    }

    private GridRefs GetRefs(bool chest)
    {
        return chest ? chestGrid : playerGrid;
    }

    private GridRuntime GetRuntime(bool chest)
    {
        return chest ? chestRuntime : playerRuntime;
    }

    // pickup, add, move items

    // used by drop/pickup scripts
    public bool TryAddItem(INV_Item item)
    {
        EnsureGrid(false);

        if (item == null)
        {
            return false;
        }

        // try stack identical items first
        if (TryStackItem(item, 1))
        {
            INV_HotBar hotBar = GetComponent<INV_HotBar>();
            if (hotBar != null)
            {
                hotBar.RefreshAllVisuals();
            }

            return true;
        }

        // create a fresh instance and look for first available spot
        ItemInstance inst;
        if (!CreateItemInstance(item, false, null, out inst))
        {
            return false;
        }

        Vector2Int cell;
        if (!LookForOccupyableSpace(playerRuntime, playerGrid, inst, out cell))
        {
            if (inst.ui != null)
            {
                Destroy(inst.ui.gameObject);
            }

            playerRuntime.items.Remove(inst);
            return false;
        }

        if (!TryPlaceItemAtCell(playerRuntime, playerGrid, cell.x, cell.y, inst, false))
        {
            if (inst.ui != null)
            {
                Destroy(inst.ui.gameObject);
            }

            playerRuntime.items.Remove(inst);
            return false;
        }

        INV_HotBar hotBar2 = GetComponent<INV_HotBar>();
        if (hotBar2 != null)
        {
            hotBar2.RefreshAllVisuals();
        }

        if (logPlacement)
        {
            Debug.Log($"INV, Added: {item.Name} at {cell}");
        }

        return true;
    }

    // used when we want to add an item to a specific target cell instead of first free space
    public bool TryAddItemAtCell(INV_Item item, Vector2Int cell, ItemInstance.Rotation rotation, int quantity = 1)
    {
        EnsureGrid(false);

        if (item == null || quantity <= 0)
        {
            return false;
        }

        // stacked items still use normal stacking first
        if (item.Stackable)
        {
            int remaining = quantity;

            while (remaining > 0)
            {
                if (TryStackItem(item, 1))
                {
                    remaining--;
                    continue;
                }

                ItemInstance newInst;
                if (!CreateItemInstance(item, false, null, out newInst))
                {
                    return false;
                }

                newInst.quantity = 1;
                newInst.rotation = rotation;

                BuildShapeForInstance(newInst, rotation);
                RebuildItemUISize(newInst);

                if (!TryPlaceItemAtCell(playerRuntime, playerGrid, cell.x, cell.y, newInst, false))
                {
                    if (newInst.ui != null)
                    {
                        Destroy(newInst.ui.gameObject);
                    }

                    playerRuntime.items.Remove(newInst);
                    return false;
                }

                remaining--;
            }

            return true;
        }

        // non stackables create one instance per amount
        for (int i = 0; i < quantity; i++)
        {
            ItemInstance inst;
            if (!CreateItemInstance(item, false, null, out inst))
            {
                return false;
            }

            inst.rotation = rotation;
            BuildShapeForInstance(inst, rotation);
            RebuildItemUISize(inst);

            if (!TryPlaceItemAtCell(playerRuntime, playerGrid, cell.x, cell.y, inst, false))
            {
                if (inst.ui != null)
                {
                    Destroy(inst.ui.gameObject);
                }

                playerRuntime.items.Remove(inst);
                return false;
            }
        }

        return true;
    }

    public bool CanAddItem(INV_Item item)
    {
        EnsureGrid(false);

        if (item == null)
        {
            return false;
        }

        // first see if it can stack
        if (item.Stackable)
        {
            for (int i = 0; i < playerRuntime.items.Count; i++)
            {
                ItemInstance inst = playerRuntime.items[i];
                if (inst?.data == null)
                {
                    continue;
                }

                if (inst.data.ItemID != item.ItemID || !inst.data.Stackable)
                {
                    continue;
                }

                if (inst.quantity < item.MaxStack)
                {
                    return true;
                }
            }
        }

        // otherwise test whether a fresh instance would fit anywhere
        ItemInstance temp = new ItemInstance
        {
            data = item,
            rotation = ItemInstance.Rotation.Up,
            quantity = 1
        };

        BuildShapeForInstance(temp, ItemInstance.Rotation.Up);

        Vector2Int foundCell;
        return LookForOccupyableSpace(playerRuntime, playerGrid, temp, out foundCell);
    }

    // called by INV_ItemUI when an item is dropped from screen point onto the player grid
    public bool TryMoveItemFromScreenPoint(ItemInstance item, Vector2 screenPoint, Camera uiCamera)
    {
        if (item == null || item.ui == null)
        {
            return false;
        }

        Vector2Int cell;
        if (!TryGetCellFromScreenPoint(playerRuntime, playerGrid, screenPoint, uiCamera, out cell))
        {
            return false;
        }

        return TryPlaceItemAtCell(playerRuntime, playerGrid, cell.x, cell.y, item, true);
    }

    // called by INV_ItemUI when a chest item is moved inside the chest grid
    public bool TryMoveChestItemFromScreenPoint(ItemInstance item, Vector2 screenPoint, Camera uiCamera)
    {
        if (item == null || item.ui == null || !item.isChestItem)
        {
            return false;
        }

        // preview items have no real chest index yet, so do not allow interaction with them
        if (string.IsNullOrWhiteSpace(item.chestItemUniqueId))
        {
            return false;
        }

        Vector2Int cell;
        if (!TryGetCellFromScreenPoint(chestRuntime, chestGrid, screenPoint, uiCamera, out cell))
        {
            return false;
        }

        if (!CheckSpaceOccupyable(chestRuntime, chestGrid, cell.x, cell.y, item, item))
        {
            return false;
        }

        INV_PlayerInventoryNet net = GetComponentInParent<INV_PlayerInventoryNet>();
        if (net == null)
        {
            return false;
        }

        // host should move the exact dragged visual immediately so duplicate items dont look like the wrong one moved
        if (net.IsServer && net.IsOwner)
        {
            RestoreItemToCellAndRotation(item, cell, item.rotation);
        }

        net.RequestMoveChestItemInOpenChest(item.chestItemUniqueId, cell, item.rotation);
        return true;
    }

    public Vector2 GetAnchoredPosForCell(int gridX, int gridY)
    {
        return GetAnchoredPosForCell(playerRuntime, playerGrid, gridX, gridY);
    }

    public Vector2 GetAnchoredPosForChestCell(int gridX, int gridY)
    {
        return GetAnchoredPosForCell(chestRuntime, chestGrid, gridX, gridY);
    }

    // gets the anchored position for the target cell inside the chosen grid
    private Vector2 GetAnchoredPosForCell(GridRuntime runtime, GridRefs refs, int gridX, int gridY)
    {
        EnsureGrid(refs == chestGrid);

        gridX = Mathf.Clamp(gridX, 0, refs.maxWidth - 1);
        gridY = Mathf.Clamp(gridY, 0, refs.maxHeight - 1);

        RectTransform cell = runtime.cellRects[gridX, gridY];
        if (cell == null)
        {
            return Vector2.zero;
        }

        Vector3[] corners = new Vector3[4];
        cell.GetWorldCorners(corners);

        Vector3 topLeftWorld = corners[1];
        Vector3 topLeftLocal = refs.itemRoot.InverseTransformPoint(topLeftWorld);

        return new Vector2(topLeftLocal.x, topLeftLocal.y);
    }

    // snaps an items rect so the correct corner matches the target cell anchored position
    private void SnapItemToTopLeft(ItemInstance item, Vector2 targetCellTopLeftAnchored)
    {
        if (item?.ui == null)
        {
            return;
        }

        RectTransform rect = item.ui;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);

        Vector3[] corners = new Vector3[4];
        rect.GetLocalCorners(corners);

        rect.anchoredPosition = targetCellTopLeftAnchored + (Vector2)corners[1];
    }

    // tries to place an item at the given grid cell
    private bool TryPlaceItemAtCell(GridRuntime runtime, GridRefs refs, int gridX, int gridY, ItemInstance item, bool clearOld)
    {
        if (item == null)
        {
            return false;
        }

        if (!CheckSpaceOccupyable(runtime, refs, gridX, gridY, item, item))
        {
            return false;
        }

        if (clearOld)
        {
            ClearItemOccupancy(runtime, refs, item);
        }

        OccupySpaces(runtime, gridX, gridY, item);

        SnapItemToTopLeft(item, GetAnchoredPosForCell(runtime, refs, gridX, gridY));
        item.cell = new Vector2Int(gridX, gridY);
        return true;
    }

    private void OccupySpaces(GridRuntime runtime, int gridX, int gridY, ItemInstance item)
    {
        for (int i = 0; i < item.occupiedOffsets.Count; i++)
        {
            Vector2Int off = item.occupiedOffsets[i];
            InventoryGridSpace s = runtime.spaces[gridX + off.x, gridY + off.y];
            s.isOccupied = true;
            s.occupyingItem = item;
        }
    }

    private void ClearItemOccupancy(GridRuntime runtime, GridRefs refs, ItemInstance item)
    {
        for (int y = 0; y < refs.maxHeight; y++)
        {
            for (int x = 0; x < refs.maxWidth; x++)
            {
                InventoryGridSpace s = runtime.spaces[x, y];
                if (s.occupyingItem != item)
                {
                    continue;
                }

                s.isOccupied = false;
                s.occupyingItem = null;
            }
        }
    }

    // searches the grid top left to bottom right for a valid placement spot
    private bool LookForOccupyableSpace(GridRuntime runtime, GridRefs refs, ItemInstance item, out Vector2Int foundCell)
    {
        for (int y = 0; y < refs.maxHeight; y++)
        {
            for (int x = 0; x < refs.maxWidth; x++)
            {
                if (!CheckSpaceOccupyable(runtime, refs, x, y, item, null))
                {
                    continue;
                }

                foundCell = new Vector2Int(x, y);
                return true;
            }
        }

        foundCell = new Vector2Int(-1, -1);
        return false;
    }

    // checks if the given item shape can fit at the target cell
    private bool CheckSpaceOccupyable(GridRuntime runtime, GridRefs refs, int gridX, int gridY, ItemInstance itemToPlace, ItemInstance ignoreItem)
    {
        if (itemToPlace == null || itemToPlace.size.x <= 0 || itemToPlace.size.y <= 0)
        {
            return false;
        }

        if (gridX < 0 || gridY < 0)
        {
            return false;
        }

        if (gridX + itemToPlace.size.x > refs.maxWidth)
        {
            return false;
        }

        if (gridY + itemToPlace.size.y > refs.maxHeight)
        {
            return false;
        }

        for (int i = 0; i < itemToPlace.occupiedOffsets.Count; i++)
        {
            Vector2Int off = itemToPlace.occupiedOffsets[i];
            InventoryGridSpace s = runtime.spaces[gridX + off.x, gridY + off.y];

            if (!s.isOccupied)
            {
                continue;
            }

            if (ignoreItem != null && s.occupyingItem == ignoreItem)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // creates a new item instance under either the player grid or chest grid
    private bool CreateItemInstance(INV_Item item, bool isChestItem, string chestItemUniqueId, out ItemInstance instance)
    {
        GridRefs refs = GetRefs(isChestItem);
        GridRuntime runtime = GetRuntime(isChestItem);

        instance = null;

        if (itemUIPrefab == null || refs.itemRoot == null)
        {
            return false;
        }

        GameObject go = Instantiate(itemUIPrefab, refs.itemRoot);
        go.name = isChestItem ? $"ChestItemUI_{item.Name}" : $"ItemUI_{item.Name}";

        RectTransform rt = go.GetComponent<RectTransform>();
        INV_ItemUI ui = go.GetComponent<INV_ItemUI>();

        if (rt == null || ui == null)
        {
            Destroy(go);
            return false;
        }

        ItemInstance inst = new ItemInstance
        {
            data = item,
            rotation = ItemInstance.Rotation.Up,
            quantity = 1,
            isChestItem = isChestItem,
            chestItemUniqueId = chestItemUniqueId,
            inventoryItemUniqueId = isChestItem ? null : System.Guid.NewGuid().ToString(),
            ui = rt,
            uiHandler = ui
        };

        BuildShapeForInstance(inst, ItemInstance.Rotation.Up);

        rt.pivot = new Vector2(0f, 1f);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);

        ui.Init(this, inst);
        RebuildItemUISize(inst);

        inst.uiHandler.RebuildOccupiedSpaceVisuals();
        inst.uiHandler.ApplyUpdatedVisuals();

        runtime.items.Add(inst);
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

        int h = shape.Count;
        int w = 0;

        for (int i = 0; i < shape.Count; i++)
        {
            if (!string.IsNullOrEmpty(shape[i]))
            {
                w = Mathf.Max(w, shape[i].Length);
            }
        }

        inst.shapeSize = new Vector2Int(Mathf.Max(1, w), Mathf.Max(1, h));

        List<Vector2Int> raw = new List<Vector2Int>();
        for (int y = 0; y < h; y++)
        {
            string row = string.IsNullOrEmpty(shape[y]) ? "" : shape[y];

            for (int x = 0; x < w; x++)
            {
                char c = x < row.Length ? row[x] : '-';
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

        int turns = rot == ItemInstance.Rotation.Up ? 0 :
                    rot == ItemInstance.Rotation.Right ? 1 :
                    rot == ItemInstance.Rotation.Down ? 2 : 3;

        for (int t = 0; t < turns; t++)
        {
            List<Vector2Int> next = new List<Vector2Int>(rotated.Count);

            for (int i = 0; i < rotated.Count; i++)
            {
                next.Add(new Vector2Int(rotated[i].y, width - 1 - rotated[i].x));
            }

            rotated = next;

            int oldWidth = width;
            width = height;
            height = oldWidth;
        }

        if (rotated.Count == 0)
        {
            inst.size = Vector2Int.one;
            return;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        for (int i = 0; i < rotated.Count; i++)
        {
            Vector2Int p = rotated[i];

            if (p.x < minX)
            {
                minX = p.x;
            }

            if (p.y < minY)
            {
                minY = p.y;
            }

            if (p.x > maxX)
            {
                maxX = p.x;
            }

            if (p.y > maxY)
            {
                maxY = p.y;
            }
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
        if (inst?.ui == null)
        {
            return;
        }

        float w = (inst.size.x * cellSize.x) + Mathf.Max(0, inst.size.x - 1) * inventoryGridSpacing.x;
        float h = (inst.size.y * cellSize.y) + Mathf.Max(0, inst.size.y - 1) * inventoryGridSpacing.y;

        inst.ui.sizeDelta = new Vector2(w, h);

        if (inst.uiHandler != null)
        {
            inst.uiHandler.RebuildOccupiedSpaceVisuals();
        }
    }

    private bool TryStackItem(INV_Item item, int amount)
    {
        if (item == null || amount <= 0 || !item.Stackable)
        {
            return false;
        }

        int maxStack = item.MaxStack;

        for (int i = 0; i < playerRuntime.items.Count; i++)
        {
            ItemInstance inst = playerRuntime.items[i];
            if (inst?.data == null)
            {
                continue;
            }

            if (inst.data.ItemID != item.ItemID || !inst.data.Stackable || inst.quantity >= maxStack)
            {
                continue;
            }

            int room = maxStack - inst.quantity;
            int add = Mathf.Min(room, amount);

            if (add <= 0)
            {
                continue;
            }

            inst.quantity += add;

            if (inst.uiHandler != null)
            {
                inst.uiHandler.ApplyUpdatedVisuals();
            }

            return true;
        }

        return false;
    }

    // figures out which cell the mouse is currently over for the chosen grid
    private bool TryGetCellFromScreenPoint(GridRuntime runtime, GridRefs refs, Vector2 screenPoint, Camera uiCamera, out Vector2Int cell)
    {
        for (int y = 0; y < refs.maxHeight; y++)
        {
            for (int x = 0; x < refs.maxWidth; x++)
            {
                RectTransform c = runtime.cellRects[x, y];
                if (c == null)
                {
                    continue;
                }

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
        if (heldItem != null || Mouse.current == null)
        {
            hoverItem = null;
            return;
        }

        Vector2 mouse = Mouse.current.position.ReadValue();
        Camera uiCam = Camera.main;

        INV_ItemUI found = null;
        int bestSibling = int.MinValue;

        FindTopHoveredItem(playerRuntime, mouse, uiCam, ref found, ref bestSibling);
        FindTopHoveredItem(chestRuntime, mouse, uiCam, ref found, ref bestSibling);

        hoverItem = found;
    }

    private void FindTopHoveredItem(GridRuntime runtime, Vector2 mouse, Camera uiCam, ref INV_ItemUI found, ref int bestSibling)
    {
        for (int i = 0; i < runtime.items.Count; i++)
        {
            ItemInstance inst = runtime.items[i];
            if (inst?.ui == null || inst.uiHandler == null)
            {
                continue;
            }

            if (!inst.uiHandler.IsScreenPointOverOccupiedSpace(mouse, uiCam))
            {
                continue;
            }

            int sib = inst.ui.GetSiblingIndex();
            if (sib < bestSibling)
            {
                continue;
            }

            bestSibling = sib;
            found = inst.uiHandler;
        }
    }

    private INV_ItemUI FindTopHoveredItemAtScreenPoint(GridRuntime runtime, Vector2 screenPoint, Camera uiCamera, ItemInstance ignore)
    {
        INV_ItemUI found = null;
        int bestSibling = int.MinValue;

        for (int i = 0; i < runtime.items.Count; i++)
        {
            ItemInstance inst = runtime.items[i];
            if (inst == null || inst == ignore || inst.ui == null || inst.uiHandler == null)
            {
                continue;
            }

            if (!inst.uiHandler.IsScreenPointOverOccupiedSpace(screenPoint, uiCamera))
            {
                continue;
            }

            int sib = inst.ui.GetSiblingIndex();
            if (sib < bestSibling)
            {
                continue;
            }

            bestSibling = sib;
            found = inst.uiHandler;
        }

        return found;
    }

    private void UpdateHoverTooltip()
    {
        if (hoverTooltip == null)
        {
            return;
        }

        if (heldItem != null)
        {
            hoverTooltip.HideImmediate();
            lastHoverItem = null;
            hoverTimer = 0f;
            return;
        }

        if (hoverItem?.Instance?.data == null)
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
        if (heldItem == null)
        {
            return false;
        }

        ItemInstance inst = heldItem.Instance;
        if (inst?.ui == null)
        {
            return false;
        }

        GridRuntime runtime = inst.isChestItem ? chestRuntime : playerRuntime;
        GridRefs refs = inst.isChestItem ? chestGrid : playerGrid;

        ClearItemOccupancy(runtime, refs, inst);

        inst.rotation = inst.rotation == ItemInstance.Rotation.Up ? ItemInstance.Rotation.Right :
                        inst.rotation == ItemInstance.Rotation.Right ? ItemInstance.Rotation.Down :
                        inst.rotation == ItemInstance.Rotation.Down ? ItemInstance.Rotation.Left :
                        ItemInstance.Rotation.Up;

        BuildShapeForInstance(inst, inst.rotation);
        RebuildItemUISize(inst);

        if (inst.uiHandler != null)
        {
            inst.uiHandler.ApplyUpdatedVisuals();
        }

        if (inst.isChestItem)
        {
            INV_PlayerInventoryNet net = GetComponentInParent<INV_PlayerInventoryNet>();
            if (net != null && !string.IsNullOrWhiteSpace(inst.chestItemUniqueId))
            {
                net.RequestMoveChestItemInOpenChest(inst.chestItemUniqueId, inst.cell, inst.rotation);
            }
        }

        if (logPlacement && inst.data != null)
        {
            Debug.Log($"INV, Rotated held item: {inst.data.Name}");
        }

        return true;
    }

    public bool DropHeldOrHoverItem()
    {
        if (heldItem != null && heldItem.Instance != null && !heldItem.Instance.isChestItem)
        {
            return DropItemInstanceToWorld(heldItem.Instance, false);
        }

        return DropHoverItem();
    }

    public bool DropHoverItem()
    {
        if (hoverItem == null || hoverItem.Instance == null || hoverItem.Instance.isChestItem)
        {
            return false;
        }

        return DropItemInstanceToWorld(hoverItem.Instance, false);
    }

    public bool DropItemInstanceToWorld(ItemInstance inst, bool dropWholeStack)
    {
        if (inst == null || inst.isChestItem || inst.data == null)
        {
            return false;
        }

        string itemId = inst.data != null ? inst.data.ItemID : string.Empty;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        int amountToDrop = dropWholeStack ? Mathf.Max(1, inst.quantity) : 1;

        INV_PlayerInventoryNet netInv = GetComponentInParent<INV_PlayerInventoryNet>();
        if (netInv == null)
        {
            return false;
        }

        for (int i = 0; i < amountToDrop; i++)
        {
            netInv.RequestDropItem(itemId);
        }

        if (dropWholeStack || inst.quantity <= 1)
        {
            RemoveLocalPlayerItemInstance(inst);
        }
        else
        {
            inst.quantity--;

            if (inst.uiHandler != null)
            {
                inst.uiHandler.ApplyUpdatedVisuals();
            }
        }

        hoverItem = null;
        heldItem = null;
        return true;
    }

    private void RemoveLocalPlayerItemInstance(ItemInstance inst)
    {
        if (inst == null)
        {
            return;
        }

        INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
        if (hotBar != null)
        {
            hotBar.ClearReferencesToItem(inst);
        }

        ClearItemOccupancy(playerRuntime, playerGrid, inst);
        playerRuntime.items.Remove(inst);

        if (inst.ui != null)
        {
            Destroy(inst.ui.gameObject);
        }
    }

    private void RemoveLocalChestItemInstance(ItemInstance inst)
    {
        if (inst == null)
        {
            return;
        }

        ClearItemOccupancy(chestRuntime, chestGrid, inst);
        chestRuntime.items.Remove(inst);

        if (inst.ui != null)
        {
            Destroy(inst.ui.gameObject);
        }
    }

    public bool RemovePlayerItemByUniqueId(string inventoryItemUniqueId, int amount)
    {
        if (string.IsNullOrWhiteSpace(inventoryItemUniqueId) || amount <= 0)
        {
            return false;
        }

        for (int i = 0; i < playerRuntime.items.Count; i++)
        {
            ItemInstance inst = playerRuntime.items[i];
            if (inst == null || inst.inventoryItemUniqueId != inventoryItemUniqueId)
            {
                continue;
            }

            int take = Mathf.Min(inst.quantity, amount);
            inst.quantity -= take;

            if (inst.quantity <= 0)
            {
                RemoveLocalPlayerItemInstance(inst);
            }
            else if (inst.uiHandler != null)
            {
                inst.uiHandler.ApplyUpdatedVisuals();
            }

            INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
            if (hotBar != null)
            {
                hotBar.RefreshAllVisuals();
            }

            return take > 0;
        }

        return false;
    }

    public bool RemoveLocalChestVisualByUniqueId(string chestItemUniqueId)
    {
        if (string.IsNullOrWhiteSpace(chestItemUniqueId))
        {
            return false;
        }

        for (int i = 0; i < chestRuntime.items.Count; i++)
        {
            ItemInstance inst = chestRuntime.items[i];
            if (inst == null)
            {
                continue;
            }

            if (inst.chestItemUniqueId != chestItemUniqueId)
            {
                continue;
            }

            RemoveLocalChestItemInstance(inst);
            return true;
        }

        return false;
    }

    public void RestoreItemToCellAndRotation(ItemInstance item, Vector2Int cell, ItemInstance.Rotation rotation)
    {
        if (item?.ui == null)
        {
            return;
        }

        GridRuntime runtime = item.isChestItem ? chestRuntime : playerRuntime;
        GridRefs refs = item.isChestItem ? chestGrid : playerGrid;

        ClearItemOccupancy(runtime, refs, item);

        item.rotation = rotation;
        BuildShapeForInstance(item, item.rotation);
        RebuildItemUISize(item);

        if (item.uiHandler != null)
        {
            item.uiHandler.ApplyUpdatedVisuals();
        }

        OccupySpaces(runtime, cell.x, cell.y, item);
        SnapItemToTopLeft(item, GetAnchoredPosForCell(runtime, refs, cell.x, cell.y));
        item.cell = cell;
    }

    private void ApplyChestGridSize(INV_Chest chest)
    {
        if (chest == null)
        {
            return;
        }

        if (chestGrid.maxWidth == chest.GridWidth && chestGrid.maxHeight == chest.GridHeight)
        {
            return;
        }

        chestGrid.maxWidth = chest.GridWidth;
        chestGrid.maxHeight = chest.GridHeight;
        chestRuntime.generated = false;

        if (chestGrid.gridRoot != null)
        {
            for (int i = chestGrid.gridRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(chestGrid.gridRoot.GetChild(i).gameObject);
            }
        }

        GenerateGrid(chestGrid, chestRuntime);
    }

    // chest setup / teardown
    public void OpenChestView(INV_Chest chest, List<INV_Chest.ChestItemData> snapshot)
    {
        // if we're dragging a chest item, defer the visual rebuild until drag ends
        if (heldItem != null && heldItem.Instance != null && heldItem.Instance.isChestItem)
        {
            pendingChestSnapshotSource = chest;
            pendingChestSnapshot = snapshot != null ? new List<INV_Chest.ChestItemData>(snapshot) : null;
            return;
        }

        ApplyOpenChestSnapshot(chest, snapshot);
    }

    private void ApplyOpenChestSnapshot(INV_Chest chest, List<INV_Chest.ChestItemData> snapshot)
    {
        ApplyChestGridSize(chest);
        EnsureGrid(true);

        activeChest = chest;

        if (chestGrid.panelRoot != null)
        {
            chestGrid.panelRoot.SetActive(true);
        }

        ClearChestView();

        if (snapshot == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            CreateChestVisualFromSnapshot(snapshot[i]);
        }
    }

    public void FlushPendingChestSnapshot()
    {
        if (pendingChestSnapshotSource == null)
        {
            return;
        }

        ApplyOpenChestSnapshot(pendingChestSnapshotSource, pendingChestSnapshot);

        pendingChestSnapshotSource = null;
        pendingChestSnapshot = null;
    }

    public void CloseChestView()
    {
        INV_Chest closingChest = activeChest;

        ClearChestView();
        activeChest = null;

        pendingChestSnapshot = null;
        pendingChestSnapshotSource = null;

        if (chestGrid.panelRoot != null)
        {
            chestGrid.panelRoot.SetActive(false);
        }

        HideContextMenu();

        INV_PlayerInventoryNet net = GetComponentInParent<INV_PlayerInventoryNet>();
        if (closingChest != null && net != null && net.IsOwner)
        {
            closingChest.RequestCloseChestRpc();
        }
    }

    private void ClearChestView()
    {
        for (int y = 0; y < chestGrid.maxHeight; y++)
        {
            for (int x = 0; x < chestGrid.maxWidth; x++)
            {
                if (chestRuntime.spaces == null)
                {
                    continue;
                }

                chestRuntime.spaces[x, y].isOccupied = false;
                chestRuntime.spaces[x, y].occupyingItem = null;
            }
        }

        for (int i = chestRuntime.items.Count - 1; i >= 0; i--)
        {
            ItemInstance inst = chestRuntime.items[i];
            if (inst?.ui != null)
            {
                Destroy(inst.ui.gameObject);
            }
        }

        chestRuntime.items.Clear();
    }

    private ItemInstance CreateChestVisualFromSnapshot(INV_Chest.ChestItemData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.itemId))
        {
            return null;
        }

        INV_Item item = INV_ItemDatabase.Instance != null ? INV_ItemDatabase.Instance.GetItemById(data.itemId) : null;
        if (item == null)
        {
            return null;
        }

        ItemInstance inst;
        if (!CreateItemInstance(item, true, data.uniqueId, out inst))
        {
            return null;
        }

        inst.quantity = Mathf.Max(1, data.quantity);
        inst.rotation = (ItemInstance.Rotation)data.rotation;

        BuildShapeForInstance(inst, inst.rotation);
        RebuildItemUISize(inst);

        if (inst.uiHandler != null)
        {
            inst.uiHandler.RebuildOccupiedSpaceVisuals();
            inst.uiHandler.ApplyUpdatedVisuals();
        }

        TryPlaceItemAtCell(chestRuntime, chestGrid, data.cellX, data.cellY, inst, false);
        return inst;
    }

    public void AddLocalChestVisualPreview(string itemId, int quantity, Vector2Int cell, ItemInstance.Rotation rotation)
    {
        INV_Chest.ChestItemData preview = new INV_Chest.ChestItemData
        {
            uniqueId = System.Guid.NewGuid().ToString(),
            itemId = itemId,
            quantity = quantity,
            cellX = cell.x,
            cellY = cell.y,
            rotation = (int)rotation
        };

        CreateChestVisualFromSnapshot(preview);
    }

    // called by item ui when releasing drag over the chest panel
    public bool TryStoreHeldItemInOpenChestFromScreenPoint(ItemInstance item, Vector2 screenPoint, Camera uiCamera, bool autoAdd)
    {
        EnsureGrid(true);

        if (activeChest == null || item == null || item.isChestItem)
        {
            return false;
        }

        if (item.data == null || string.IsNullOrWhiteSpace(item.inventoryItemUniqueId))
        {
            return false;
        }

        INV_PlayerInventoryNet net = GetComponentInParent<INV_PlayerInventoryNet>();
        if (net == null)
        {
            return false;
        }

        string inventoryItemUniqueId = item.inventoryItemUniqueId;
        string itemId = item.data.ItemID;
        int quantity = Mathf.Max(1, item.quantity);
        ItemInstance.Rotation rotation = item.rotation;

        Vector2Int chestCell;
        if (autoAdd)
        {
            if (!LookForOccupyableSpace(chestRuntime, chestGrid, item, out chestCell))
            {
                return false;
            }
        }
        else
        {
            if (!TryGetCellFromScreenPoint(chestRuntime, chestGrid, screenPoint, uiCamera, out chestCell))
            {
                return false;
            }

            if (!CheckSpaceOccupyable(chestRuntime, chestGrid, chestCell.x, chestCell.y, item, null))
            {
                return false;
            }
        }

        // host uses the real server path directly, so no local prediction
        if (net.IsServer)
        {
            net.RequestStoreItemInOpenChest(inventoryItemUniqueId, itemId, quantity, chestCell, rotation);
            return true;
        }

        // client predicts locally because server does not own the real inventory state for remote players
        RemoveLocalPlayerItemInstance(item);
        AddLocalChestVisualPreview(itemId, quantity, chestCell, rotation);

        net.RequestStoreItemInOpenChest(inventoryItemUniqueId, itemId, quantity, chestCell, rotation);
        return true;
    }

    // called by item ui when releasing a chest item over the player inventory
    public bool TryTakeChestItemToInventoryFromScreenPoint(ItemInstance item, Vector2 screenPoint, Camera uiCamera)
    {
        if (activeChest == null || item == null || !item.isChestItem)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.chestItemUniqueId))
        {
            return false;
        }

        if (item.data == null)
        {
            return false;
        }

        Vector2Int inventoryCell;
        if (!TryGetCellFromScreenPoint(playerRuntime, playerGrid, screenPoint, uiCamera, out inventoryCell))
        {
            return false;
        }

        if (!CheckSpaceOccupyable(playerRuntime, playerGrid, inventoryCell.x, inventoryCell.y, item, null))
        {
            return false;
        }

        INV_PlayerInventoryNet net = GetComponentInParent<INV_PlayerInventoryNet>();
        if (net == null)
        {
            return false;
        }

        // host uses the real server path directly, so no local prediction
        if (net.IsServer)
        {
            net.RequestTakeChestItemFromOpenChest(item.chestItemUniqueId, inventoryCell, item.rotation);
            return true;
        }

        // client waits for exact-cell local add rpc + chest refresh
        net.RequestTakeChestItemFromOpenChest(item.chestItemUniqueId, inventoryCell, item.rotation);
        return true;
    }

    public bool TryMoveLocalChestVisualByUniqueId(string chestItemUniqueId, Vector2Int cell, ItemInstance.Rotation rotation)
    {
        if (string.IsNullOrWhiteSpace(chestItemUniqueId))
        {
            return false;
        }

        for (int i = 0; i < chestRuntime.items.Count; i++)
        {
            ItemInstance inst = chestRuntime.items[i];
            if (inst == null || !inst.isChestItem)
            {
                continue;
            }

            if (inst.chestItemUniqueId != chestItemUniqueId)
            {
                continue;
            }

            RestoreItemToCellAndRotation(inst, cell, rotation);
            return true;
        }

        return false;
    }

    public bool TryMergeItemIntoHoveredStack(ItemInstance dragged, Vector2 screenPoint, Camera uiCamera)
    {
        if (dragged == null || dragged.data == null || !dragged.data.Stackable || dragged.isChestItem)
        {
            return false;
        }

        INV_ItemUI targetUI = FindTopHoveredItemAtScreenPoint(playerRuntime, screenPoint, uiCamera, dragged);
        if (targetUI == null || targetUI.Instance == null || targetUI.Instance == dragged)
        {
            return false;
        }

        ItemInstance target = targetUI.Instance;
        if (target.data == null || !target.data.Stackable || target.data.ItemID != dragged.data.ItemID)
        {
            return false;
        }

        int maxStack = target.data.MaxStack;
        if (target.quantity >= maxStack)
        {
            return false;
        }

        int room = maxStack - target.quantity;
        int add = Mathf.Min(room, dragged.quantity);

        target.quantity += add;
        dragged.quantity -= add;

        if (target.uiHandler != null)
        {
            target.uiHandler.ApplyUpdatedVisuals();
        }

        if (dragged.quantity <= 0)
        {
            RemoveLocalPlayerItemInstance(dragged);
        }
        else if (dragged.uiHandler != null)
        {
            dragged.uiHandler.ApplyUpdatedVisuals();
            RestoreItemToCellAndRotation(dragged, dragged.cell, dragged.rotation);
        }

        INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
        if (hotBar != null)
        {
            hotBar.RefreshAllVisuals();
        }

        return add > 0;
    }

    public bool TryQuickStoreItemInOpenChest(ItemInstance item)
    {
        EnsureGrid(true);

        if (activeChest == null || item == null || item.isChestItem || item.data == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.inventoryItemUniqueId))
        {
            return false;
        }

        INV_PlayerInventoryNet net = GetComponentInParent<INV_PlayerInventoryNet>();
        if (net == null)
        {
            return false;
        }

        Vector2Int chestCell;
        if (!LookForOccupyableSpace(chestRuntime, chestGrid, item, out chestCell))
        {
            return false;
        }

        string itemId = item.data.ItemID;
        int quantity = Mathf.Max(1, item.quantity);
        ItemInstance.Rotation rotation = item.rotation;

        if (net.IsServer)
        {
            net.RequestStoreItemInOpenChest(item.inventoryItemUniqueId, itemId, quantity, chestCell, rotation);
            return true;
        }

        RemoveLocalPlayerItemInstance(item);
        AddLocalChestVisualPreview(itemId, quantity, chestCell, rotation);

        net.RequestStoreItemInOpenChest(item.inventoryItemUniqueId, itemId, quantity, chestCell, rotation);
        return true;
    }

    public bool TryQuickTakeChestItemOne(ItemInstance item)
    {
        if (item == null || !item.isChestItem || item.data == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.chestItemUniqueId))
        {
            return false;
        }

        INV_PlayerInventoryNet net = GetComponentInParent<INV_PlayerInventoryNet>();
        if (net == null)
        {
            return false;
        }

        net.RequestTakeChestItemQuick(item.chestItemUniqueId, false);
        return true;
    }

    public bool TryQuickTakeChestItemStack(ItemInstance item)
    {
        if (item == null || !item.isChestItem || item.data == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.chestItemUniqueId))
        {
            return false;
        }

        INV_PlayerInventoryNet net = GetComponentInParent<INV_PlayerInventoryNet>();
        if (net == null)
        {
            return false;
        }

        net.RequestTakeChestItemQuick(item.chestItemUniqueId, true);
        return true;
    }

    public string GetFirstPlayerItemUniqueIdByItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        for (int i = 0; i < playerRuntime.items.Count; i++)
        {
            ItemInstance inst = playerRuntime.items[i];
            if (inst?.data == null)
            {
                continue;
            }

            if (inst.data.ItemID != itemId)
            {
                continue;
            }

            return inst.inventoryItemUniqueId;
        }

        return null;
    }

    public bool HasPlayerItemWithUniqueId(string inventoryItemUniqueId)
    {
        if (string.IsNullOrWhiteSpace(inventoryItemUniqueId))
        {
            return false;
        }

        for (int i = 0; i < playerRuntime.items.Count; i++)
        {
            ItemInstance inst = playerRuntime.items[i];
            if (inst == null)
            {
                continue;
            }

            if (inst.inventoryItemUniqueId == inventoryItemUniqueId)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryUseLocalConsumableByUniqueId(string inventoryItemUniqueId)
    {
        if (string.IsNullOrWhiteSpace(inventoryItemUniqueId))
        {
            return false;
        }

        for (int i = 0; i < playerRuntime.items.Count; i++)
        {
            ItemInstance inst = playerRuntime.items[i];
            if (inst?.data == null)
            {
                continue;
            }

            if (inst.inventoryItemUniqueId != inventoryItemUniqueId)
            {
                continue;
            }

            if (inst.data.ItemTypeVal != INV_Item.ItemType.Consumable)
            {
                return false;
            }

            return RemovePlayerItemByUniqueId(inventoryItemUniqueId, 1);
        }

        return false;
    }

    public bool TryUseLocalConsumableByItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        for (int i = 0; i < playerRuntime.items.Count; i++)
        {
            ItemInstance inst = playerRuntime.items[i];
            if (inst?.data == null)
            {
                continue;
            }

            if (inst.data.ItemID != itemId)
            {
                continue;
            }

            if (inst.data.ItemTypeVal != INV_Item.ItemType.Consumable)
            {
                return false;
            }

            return RemovePlayerItemByUniqueId(inst.inventoryItemUniqueId, 1);
        }

        return false;
    }

    // crafting
    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        int total = 0;

        for (int i = 0; i < playerRuntime.items.Count; i++)
        {
            ItemInstance inst = playerRuntime.items[i];
            if (inst?.data == null || inst.data.ItemID != itemId)
            {
                continue;
            }

            total += Mathf.Max(1, inst.quantity);
        }

        return total;
    }

    public bool HasItemAmount(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (amount <= 0)
        {
            return true;
        }

        return GetItemCount(itemId) >= amount;
    }

    // used by crafting
    public bool RemoveItemAmount(string itemId, int amount)
    {
        EnsureGrid(false);

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (amount <= 0)
        {
            return true;
        }

        if (!HasItemAmount(itemId, amount))
        {
            return false;
        }

        int remaining = amount;

        for (int i = playerRuntime.items.Count - 1; i >= 0; i--)
        {
            ItemInstance inst = playerRuntime.items[i];
            if (inst?.data == null || inst.data.ItemID != itemId)
            {
                continue;
            }

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

                ClearItemOccupancy(playerRuntime, playerGrid, inst);
                playerRuntime.items.RemoveAt(i);

                if (inst.ui != null)
                {
                    Destroy(inst.ui.gameObject);
                }
            }
            else if (inst.uiHandler != null)
            {
                inst.uiHandler.ApplyUpdatedVisuals();
            }

            if (remaining > 0)
            {
                continue;
            }

            INV_HotBar hotBar2 = GetComponent<INV_HotBar>();
            if (hotBar2 != null)
            {
                hotBar2.RefreshAllVisuals();
            }

            return true;
        }

        return remaining <= 0;
    }

    public void ShowContextMenuForItem(ItemInstance inst, Vector2 screenPoint)
    {
        if (itemContextMenu == null || inst == null || inst.data == null)
        {
            return;
        }

        itemContextMenu.Show(inst, screenPoint);
    }

    public void HideContextMenu()
    {
        if (itemContextMenu != null)
        {
            itemContextMenu.HideImmediate();
        }
    }

    public List<ContextActionType> GetContextActionsForItem(ItemInstance inst)
    {
        List<ContextActionType> actions = new List<ContextActionType>();

        if (inst == null || inst.data == null)
        {
            return actions;
        }

        // chest item context menu
        if (inst.isChestItem)
        {
            actions.Add(ContextActionType.TakeOne);
            if (inst.quantity > 1) { actions.Add(ContextActionType.TakeStack); }
            return actions;
        }

        INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
        bool hasSelectedHotbar = hotBar != null && hotBar.SelectedSlot >= 0;
        bool showDropStack = inst.quantity > 1;

        switch (inst.data.ItemTypeVal)
        {
            case INV_Item.ItemType.Consumable:
                actions.Add(ContextActionType.Use);
                actions.Add(ContextActionType.DropOne);
                if (showDropStack) { actions.Add(ContextActionType.DropStack); }
                AddHotbarAssignActions(actions);
                if (hasSelectedHotbar) { actions.Add(ContextActionType.AssignSelectedHotbar); }
                break;
            case INV_Item.ItemType.Weapon:
                actions.Add(ContextActionType.DropOne);
                if (showDropStack) { actions.Add(ContextActionType.DropStack); }
                AddHotbarAssignActions(actions);
                if (hasSelectedHotbar) { actions.Add(ContextActionType.AssignSelectedHotbar); }
                break;
            case INV_Item.ItemType.Tool:
                actions.Add(ContextActionType.DropOne);
                if (showDropStack) { actions.Add(ContextActionType.DropStack); }
                AddHotbarAssignActions(actions);
                if (hasSelectedHotbar) { actions.Add(ContextActionType.AssignSelectedHotbar); }
                break;
            case INV_Item.ItemType.Item:
                actions.Add(ContextActionType.DropOne);
                if (showDropStack) { actions.Add(ContextActionType.DropStack); }
                AddHotbarAssignActions(actions);
                if (hasSelectedHotbar) { actions.Add(ContextActionType.AssignSelectedHotbar); }
                break;
            case INV_Item.ItemType.Resource:
                actions.Add(ContextActionType.DropOne);
                if (showDropStack) { actions.Add(ContextActionType.DropStack); }
                AddHotbarAssignActions(actions);
                if (hasSelectedHotbar) { actions.Add(ContextActionType.AssignSelectedHotbar); }
                break;
            case INV_Item.ItemType.Placeable:
                actions.Add(ContextActionType.DropOne);
                if (showDropStack) { actions.Add(ContextActionType.DropStack); }
                AddHotbarAssignActions(actions);
                if (hasSelectedHotbar) { actions.Add(ContextActionType.AssignSelectedHotbar); }
                break;
        }

        return actions;
    }

    private void AddHotbarAssignActions(List<ContextActionType> actions)
    {
        INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
        int slotCount = hotBar != null ? hotBar.SlotCount : 0;

        for (int i = 0; i < slotCount; i++)
        {
            switch (i)
            {
                case 0: actions.Add(ContextActionType.AssignHotbar1); break;
                case 1: actions.Add(ContextActionType.AssignHotbar2); break;
                case 2: actions.Add(ContextActionType.AssignHotbar3); break;
                case 3: actions.Add(ContextActionType.AssignHotbar4); break;
            }
        }
    }

    public string GetContextActionLabel(ContextActionType action)
    {
        switch (action)
        {
            case ContextActionType.Use: { return "Use"; }
            case ContextActionType.DropOne: { return "Drop One"; }
            case ContextActionType.DropStack: { return "Drop Stack"; }
            case ContextActionType.AssignHotbar1: { return "Assign Hotbar 1"; }
            case ContextActionType.AssignHotbar2: { return "Assign Hotbar 2"; }
            case ContextActionType.AssignHotbar3: { return "Assign Hotbar 3"; }
            case ContextActionType.AssignHotbar4: { return "Assign Hotbar 4"; }
            case ContextActionType.AssignSelectedHotbar: { return "Assign Selected Hotbar"; }
            case ContextActionType.TakeOne: { return "Take One"; }
            case ContextActionType.TakeStack: { return "Take Stack"; }
        }

        return action.ToString();
    }

    public bool ExecuteContextAction(ItemInstance inst, ContextActionType action)
    {
        if (inst == null || inst.data == null)
        {
            return false;
        }

        INV_HotBar hotBar = GetComponentInParent<INV_HotBar>();
        INV_PlayerInventoryNet net = GetComponentInParent<INV_PlayerInventoryNet>();

        switch (action)
        {
            case ContextActionType.Use:
                if (net == null || inst.isChestItem)
                {
                    return false;
                }

                net.RequestUseItemInInventory(inst.inventoryItemUniqueId, inst.data.ItemID);
                return true;

            case ContextActionType.DropOne:
                if (inst.isChestItem)
                {
                    return false;
                }

                return DropItemInstanceToWorld(inst, false);

            case ContextActionType.DropStack:
                if (inst.isChestItem)
                {
                    return false;
                }

                return DropItemInstanceToWorld(inst, true);

            case ContextActionType.AssignHotbar1:
                if (hotBar == null || inst.isChestItem)
                {
                    return false;
                }

                return hotBar.AssignItemToSlot(0, inst);

            case ContextActionType.AssignHotbar2:
                if (hotBar == null || inst.isChestItem)
                {
                    return false;
                }

                return hotBar.AssignItemToSlot(1, inst);

            case ContextActionType.AssignHotbar3:
                if (hotBar == null || inst.isChestItem)
                {
                    return false;
                }

                return hotBar.AssignItemToSlot(2, inst);

            case ContextActionType.AssignHotbar4:
                if (hotBar == null || inst.isChestItem)
                {
                    return false;
                }

                return hotBar.AssignItemToSlot(3, inst);

            case ContextActionType.AssignSelectedHotbar:
                if (hotBar == null || inst.isChestItem || hotBar.SelectedSlot < 0)
                {
                    return false;
                }

                return hotBar.AssignItemToSlot(hotBar.SelectedSlot, inst);

            case ContextActionType.TakeOne:
                if (!inst.isChestItem) { return false; }
                return TryQuickTakeChestItemOne(inst);

            case ContextActionType.TakeStack:
                if (!inst.isChestItem) { return false; }
                return TryQuickTakeChestItemStack(inst);
        }

        return false;
    }
}