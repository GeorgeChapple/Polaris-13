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

    [Header("Visuals")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;

    [Header("Inventory")]
    [Tooltip("Grid size in cells (X = width, Y = height).")]
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

    // utility
    public Vector2Int ItemGridSize // forces a minimum size of 1,1
    {
        get
        {
            int w = Mathf.Max(1, Mathf.RoundToInt(inventorySpace.x));
            int h = Mathf.Max(1, Mathf.RoundToInt(inventorySpace.y));
            return new Vector2Int(w, h);
        }
    }
}