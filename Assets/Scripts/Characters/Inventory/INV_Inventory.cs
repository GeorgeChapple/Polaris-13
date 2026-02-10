using System.Runtime.CompilerServices;
using UnityEngine;
// Made By: Jason Lodge
// Summary: Inventory RE4 style, Inventory has grid and uses INV_Object as the data holder for it.

// This will handle Setup, update/refresh, and close.

// Objects on the grid no matter the size should have the pivot be in their top left corner,
// as having it be the same for all would help with organisation
public class INV_Inventory : MonoBehaviour
{
    [SerializeField] private float inventorySize = 30;
    [SerializeField] private float inventoryMaxWidth = 10;
    [SerializeField] private float inventoryMaxHeight = 10;

    [SerializeField] private InventoryGridSpace inventorySpace;

    [System.Serializable]
    public class InventoryGridSpace
    {
        public Vector2 position;
        public bool isOccupied;
        public INV_Object occupyingObject;
    }

    private void Awake()
    {
        GenerateGrid();
    }

    private void GenerateGrid()
    {

    }

}
