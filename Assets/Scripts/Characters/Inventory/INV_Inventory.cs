using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
// Made By: Jason Lodge
// Summary: Inventory RE4 style, Inventory has grid and uses INV_Object as the data holder for it.

// This will handle Setup, update/refresh, and close.

// Objects on the grid no matter the size should have the pivot be in their top left corner,
// as having it be the same for all would help with organisation
public class INV_Inventory : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("Max amount of spaces in inventory height wise")]
    [SerializeField] private int inventoryGridMaxHeight = 3;
    [Tooltip("Max amount of spaces in inventory width wise")]
    [SerializeField] private int inventoryGridMaxWidth = 10;

    [Tooltip("Grid parent with GridLayoutGroup.")]
    [SerializeField] private RectTransform gridRoot;

    [Tooltip("Single grid cell prefab.")]
    [SerializeField] private GameObject gridCellPrefab;

    [Header("Items")]
    [Tooltip("Item UI prefab (needs to have INV_ItemUI).")]
    [SerializeField] private GameObject itemUIPrefab;


    // runtime
    private bool gridGenerated;

    private GridLayoutGroup gridLayoutGroup;
    private Vector2 cellSize;

    // grid occupancy
    private InventoryGridSpace[,] spaces;

    // item instances
    private readonly List<ItemInstance> items = new List<ItemInstance>();

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
        public Vector2Int topLeft;
        public Vector2Int size;
        public RectTransform ui;
    }

    private void Start()
    {
        EnsureGrid();
    }

    // makes sure grid is setup before we update it
    private void EnsureGrid()
    {
        if (gridGenerated) { return; }
        if (gridRoot == null || gridCellPrefab == null)
        {
            Debug.LogWarning("Inventory missing grid references.", this);
            return;
        }

        gridLayoutGroup = gridRoot.GetComponent<GridLayoutGroup>();

        float cellSizeX = gridRoot.sizeDelta.x / inventoryGridMaxWidth;

        // cell size should be an equal square
        cellSize = new Vector2(cellSizeX, cellSizeX);

        // may change later but all ui should likely be scaled depending on screen size(just use scale dont bother changing w/h)
        // will need more logic making sure inventory height doesnt exceed inventory grid y size
        gridLayoutGroup.cellSize = cellSize;        

        spaces = new InventoryGridSpace[inventoryGridMaxWidth, inventoryGridMaxHeight];

        // generates all spaces once
        for (int y = 0; y < inventoryGridMaxHeight; y++)
        {
            for (int x = 0; x < inventoryGridMaxWidth; x++)
            {
                GameObject cell = Instantiate(gridCellPrefab, gridRoot);
                cell.name = $"Cell_X:{x},Y:{y}";

                InventoryGridSpace s = new InventoryGridSpace();
                s.position = new Vector2Int(x, y);
                s.isOccupied = false;
                s.occupyingItem = null;

                spaces[x,y] = s;
            }
        }

        gridGenerated = true;
    }
}
