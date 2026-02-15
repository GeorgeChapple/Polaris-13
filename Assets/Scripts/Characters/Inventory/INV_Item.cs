using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Data Holder for all objects.
[CreateAssetMenu(menuName = "Inventory/Object")]
public class INV_Item : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string itemID;
    [SerializeField] private string m_name;
    [SerializeField] private string description;

    [Header("Specs")]
    [SerializeField] private float durability;

    [Header("Visuals")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;

    [Header("Inventory")]
    [Tooltip("Complex shape per row. '+' = occupies, '-' = empty. Each entry is the next line down.\nExample: '++', '+-'")]
    [SerializeField] private List<string> inventorySpaceShape = new List<string>() { "++", "+-" };

    [Tooltip("Fallback size (only used if inventorySpaceShape is empty). Grid size in cells (X = width, Y = height).")]
    [SerializeField] private Vector2 inventorySpace = new Vector2(1, 1);

    public enum ObjectType { Item, Consumable, Weapon, Tool, Placeable };
    public ObjectType objectType = ObjectType.Item;

    // getters
    public string ItemID => itemID;
    public string Name => m_name;
    public string Description => description;
    public Sprite Icon => icon;
    public Mesh Mesh => mesh;
    public Material Material => material;

    public List<string> InventorySpaceShape => inventorySpaceShape;

    // utility
    public Vector2Int ItemGridSize // forces a minimum size of 1,1
    {
        get
        {
            Vector2Int shapeSize = GetShapeSize();
            if (shapeSize.x > 0 && shapeSize.y > 0)
            {
                return new Vector2Int(Mathf.Max(1, shapeSize.x), Mathf.Max(1, shapeSize.y));
            }

            // fallback
            int w = Mathf.Max(1, Mathf.RoundToInt(inventorySpace.x));
            int h = Mathf.Max(1, Mathf.RoundToInt(inventorySpace.y));
            return new Vector2Int(w, h);
        }
    }

    // returns bounding size of the current shape list
    private Vector2Int GetShapeSize()
    {
        if (inventorySpaceShape == null || inventorySpaceShape.Count == 0) { return Vector2Int.zero; }

        int h = inventorySpaceShape.Count;
        int w = 0;

        for (int i = 0; i < inventorySpaceShape.Count; i++)
        {
            string row = inventorySpaceShape[i];
            if (string.IsNullOrEmpty(row)) { continue; }
            w = Mathf.Max(w, row.Length);
        }

        return new Vector2Int(w, h);
    }
}