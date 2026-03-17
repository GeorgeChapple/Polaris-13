using System;
using System.Collections.Generic;
using Unity.Netcode;
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

    [Header("Stack")]
    [SerializeField] private bool stackable;
    [SerializeField, Min(1)] private int maxStack = 1;

    [Header("Crafting")]
    [SerializeField] private bool craftable;
    [SerializeField] private bool canCraftAnywhere;
    [SerializeField] private List<CraftingStack> craftingRequirements = new List<CraftingStack>();
    public List<CraftingStack> CraftingRequirements => craftingRequirements;

    [Serializable]
    public class CraftingStack
    {
        public INV_Item item;
        public int amount;
    }

    [Header("Visuals")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;

    [Header("Inventory Mesh Visual")]
    [Tooltip("Local offset applied to the mesh visual when shown in inventory.")]
    [SerializeField] private Vector3 inventoryMeshOffset = Vector3.zero;

    [Tooltip("Scale applied to the mesh visual when shown in inventory.")]
    [SerializeField] private float inventoryMeshScale = 1f;

    [Header("Equipped Mesh Visual")]
    [Tooltip("Local offset applied to the mesh visual when equipped.")]
    [SerializeField] private Vector3 equippedMeshOffset = Vector3.zero;

    [Tooltip("Scale applied to the mesh visual when equipped.")]
    [SerializeField] private float equippedMeshScale = 1f;

    [Header("Item Use Script")]
    [SerializeField] private MonoBehaviour itemUseScript;

    [Header("Inventory")]
    [Tooltip("Complex shape per row. '+' = occupies, '-' = empty. Each entry is the next line down.\nExample: '++', '+-'")]
    [SerializeField] private List<string> inventorySpaceShape = new List<string>() { "++", "+-" };

    [Tooltip("Fallback size (only used if inventorySpaceShape is empty). Grid size in cells (X = width, Y = height).")]
    [SerializeField] private Vector2 inventorySpace = new Vector2(1, 1);

    public enum ObjectType { Item, Consumable, Weapon, Tool, Resource, Placeable };
    public ObjectType objectType = ObjectType.Item;

    // getters
    public string ItemID => itemID;
    public string Name => m_name;
    public string Description => description;
    public float Durability => durability;

    public bool Stackable => stackable;
    public int MaxStack => stackable ? Mathf.Max(1, maxStack) : 1;

    public bool Craftable => craftable;
    public bool CanCraftAnywhere => canCraftAnywhere;

    public Sprite Icon => icon;
    public Mesh Mesh => mesh;
    public Material Material => material;

    public Vector3 InventoryMeshOffset => inventoryMeshOffset;
    public float InventoryMeshScale => inventoryMeshScale;
    public Vector3 EquippedMeshOffset => equippedMeshOffset;
    public float EquippedMeshScale => equippedMeshScale;

    public MonoBehaviour ItemUseScript => itemUseScript;

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

    // called by editor button, we dont want this in onvalidate as it would run after every character we type
    public void NormalizeInventoryShape()
    {
        if (inventorySpaceShape == null)
        {
            inventorySpaceShape = new List<string>();
        }

        // if empty, do nothing
        if (inventorySpaceShape.Count == 0) { return; }

        // find max width
        int maxW = 0;
        for (int i = 0; i < inventorySpaceShape.Count; i++)
        {
            string row = inventorySpaceShape[i];
            if (string.IsNullOrEmpty(row)) { row = ""; }
            maxW = Mathf.Max(maxW, row.Length);
        }

        maxW = Mathf.Max(1, maxW);

        // validate each row
        // pad shorter rows with - and replace each invalid char with -
        for (int i = 0; i < inventorySpaceShape.Count; i++)
        {
            string row = inventorySpaceShape[i];
            if (string.IsNullOrEmpty(row)) { row = ""; }

            char[] chars = row.ToCharArray();
            for (int c = 0; c < chars.Length; c++)
            {
                if (chars[c] != '+' && chars[c] != '-')
                {
                    chars[c] = '-';
                }
            }

            row = new string(chars);

            // pad to max width
            if (row.Length < maxW)
            {
                row = row.PadRight(maxW, '-');
            }

            inventorySpaceShape[i] = row;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(INV_Item))]
public class INV_ItemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        INV_Item item = (INV_Item)target;
        if (item == null) { return; }

        EditorGUILayout.Space(8);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Inventory Shape Tool", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox("Normalize will:\nConvert invalid chars to '-'\nPad rows to equal width using '-'", MessageType.Info);

        if (GUILayout.Button("Normalize Inventory Shape"))
        {
            Undo.RecordObject(item, "Normalize Inventory Shape");
            item.NormalizeInventoryShape();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.EndVertical();
    }
}
#endif