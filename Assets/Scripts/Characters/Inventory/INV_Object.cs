using UnityEngine;
// Made By: Jason Lodge
// Summary: Data Holder for all objects.
[CreateAssetMenu(menuName = "Inventory/Object")]
public class INV_Object : ScriptableObject
{
    [SerializeField] private string m_name;
    [SerializeField] private string description;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Vector2 inventorySpace;
    public enum ObjectType { Item, Consumable, Weapon, Tool, Placeable };
    public ObjectType objectType = ObjectType.Item;



    // Name str
    // Description str
    // Mesh 
    // Invertory Space Vec2
    // 
}
