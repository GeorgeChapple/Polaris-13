using System.Collections.Generic;
using UnityEngine;
using System;

using static SP_SpawnSettings;


#if UNITY_EDITOR
using UnityEditor;
#endif

// Made By: Jason Lodge
// Summary: Data Holder for all items.
[CreateAssetMenu(menuName = "Inventory/Item")]
public class INV_Item : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string itemID;
    [SerializeField] private string m_name;
    [SerializeField][TextArea(1,5)] private string description;

    public enum ItemType { Item, Consumable, Weapon, Tool, Resource, Placeable }
    [SerializeField] private ItemType itemType = ItemType.Item;
    public enum ItemRarity { Common, Uncommon, Rare, Epic }
    [SerializeField] private ItemRarity itemRarity = ItemRarity.Common;

    [Header("Specs")]
    [SerializeField] private float durability;
    [SerializeField] private float hungerReplenish;
    [SerializeField] private float hungerDrainDelay;
    [SerializeField] private float thirstReplenish;
    [SerializeField] private float thirstDrainDelay;

    [Header("Probability")]
    [SerializeField, Range(0, 100)] private int chanceOfSpawnInChest = 0;
    [SerializeField, Range(0, 100)] private int chanceOfSpawnAsDebris = 0;
    [SerializeField] private List<BiomeProbability> biomeProbabilities;

    [Serializable]
    public struct BiomeProbability
    {
        public BiomeType biomeType;

        public float multiplier;
    }

    [Header("Stack")]
    [SerializeField] private bool stackable;
    [SerializeField, Min(1)] private int maxStack = 1;

    [Header("Crafting")]
    [SerializeField] private bool craftable;
    [SerializeField] private bool canCraftAnywhere;

    // Old ver so I dont break recipes while changing to new ver, will probably also just keep in here as a fallback.
    [Tooltip("Old single recipe. If Crafting Recipes is empty, this will be used as recipe 0.")]
    [SerializeField] private List<CraftingStack> craftingRequirements = new List<CraftingStack>();

    [SerializeField] private List<CraftingRecipe> craftingRecipes = new List<CraftingRecipe>();

    public List<CraftingStack> CraftingRequirements => GetRecipeRequirements(0);
    public List<CraftingRecipe> CraftingRecipes => craftingRecipes;

    [System.Serializable]
    public class CraftingStack
    {
        public INV_Item item;
        public int amount;
    }

    [System.Serializable]
    public class CraftingRecipe
    {
        public string recipeName = "Recipe";
        public List<CraftingStack> requirements = new List<CraftingStack>();
    }

    [Header("Visuals")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;

    [Header("Inventory Mesh Visual")]
    [Tooltip("Local offset applied to the mesh visual when shown in inventory.")]
    [SerializeField] private Vector3 inventoryMeshOffset = Vector3.zero;

    [Tooltip("Local rotation applied to the mesh visual when shown in inventory.")]
    [SerializeField] private Vector3 inventoryMeshRotation = Vector3.zero;

    [Tooltip("Scale applied to the mesh visual when shown in inventory.")]
    [SerializeField] private float inventoryMeshScale = 1f;

    [Header("Equipped Prefab")]
    [Tooltip("Prefab used when this item is equipped.")]
    [SerializeField] private GameObject equippedPrefab;

    [Tooltip("If true, the equipped prefab visual will be overwritten using the item mesh/material/offset/rotation/scale values below.")]
    [SerializeField] private bool applyEquippedPrefabVisuals;

    [Header("Equipped Mesh Visual")]
    [Tooltip("Local offset applied to the mesh visual when equipped.")]
    [SerializeField] private Vector3 equippedMeshOffset = Vector3.zero;

    [Tooltip("Local rotation applied to the mesh visual when equipped.")]
    [SerializeField] private Vector3 equippedMeshRotation = Vector3.zero;

    [Tooltip("Scale applied to the mesh visual when equipped.")]
    [SerializeField] private float equippedMeshScale = 1f;

    [Header("Crafting Mesh Visual")]
    [Tooltip("Local offset applied to the mesh visual when shown in crafting menu.")]
    [SerializeField] private Vector3 craftingMeshOffset = Vector3.zero;

    [Tooltip("Local rotation applied to the mesh visual when shown in crafting menu.")]
    [SerializeField] private Vector3 craftingMeshRotation = Vector3.zero;

    [Tooltip("Scale applied to the mesh visual when shown in crafting menu.")]
    [SerializeField] private float craftingMeshScale = 1f;

    [Header("Inventory")]
    [Tooltip("Complex shape per row. '+' = occupies, '-' = empty. Each entry is the next line down.\nExample: '++', '+-'")]
    [SerializeField] private List<string> inventorySpaceShape = new List<string>() { "++", "+-" };

    [Tooltip("Fallback size (only used if inventorySpaceShape is empty). Grid size in cells (X = width, Y = height).")]
    [SerializeField] private Vector2 inventorySpace = new Vector2(1, 1);

    // getters
    public string ItemID => itemID;
    public string Name => m_name;
    public string Description => description;
    public ItemType ItemTypeVal => itemType;
    public ItemRarity ItemRarityVal => itemRarity;

    public float Durability => durability;
    public float HungerReplenish => hungerReplenish;
    public float HungerDrainDelay => hungerDrainDelay;
    public float ThirstReplenish => thirstReplenish;
    public float ThirstDrainDelay => thirstDrainDelay;

    public int ChanceOfSpawnInChest => chanceOfSpawnInChest;
    public int ChanceOfSpawnAsDebris => chanceOfSpawnAsDebris;

    public List<BiomeProbability> BiomeProbablities => biomeProbabilities;

    public bool Stackable => stackable;
    public int MaxStack => stackable ? Mathf.Max(1, maxStack) : 1;

    public bool Craftable => craftable;
    public bool CanCraftAnywhere => canCraftAnywhere;

    public Sprite Icon => icon;
    public Mesh Mesh => mesh;
    public Material Material => material;

    public Vector3 InventoryMeshOffset => inventoryMeshOffset;
    public Vector3 InventoryMeshRotation => inventoryMeshRotation;
    public float InventoryMeshScale => inventoryMeshScale;

    public GameObject EquippedPrefab => equippedPrefab;
    public bool ApplyEquippedPrefabVisuals => applyEquippedPrefabVisuals;
    public Vector3 EquippedMeshOffset => equippedMeshOffset;
    public Vector3 EquippedMeshRotation => equippedMeshRotation;
    public float EquippedMeshScale => equippedMeshScale;

    public Vector3 CraftingMeshOffset => craftingMeshOffset;
    public Vector3 CraftingMeshRotation => craftingMeshRotation;
    public float CraftingMeshScale => craftingMeshScale;

    public List<string> InventorySpaceShape => inventorySpaceShape;

    public int RecipeCount
    {
        get
        {
            if (craftingRecipes != null && craftingRecipes.Count > 0)
            {
                return craftingRecipes.Count;
            }

            if (craftingRequirements != null && craftingRequirements.Count > 0)
            {
                return 1;
            }

            return 0;
        }
    }

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

    public List<CraftingStack> GetRecipeRequirements(int recipeIndex)
    {
        if (craftingRecipes != null && craftingRecipes.Count > 0)
        {
            if (recipeIndex >= 0 && recipeIndex < craftingRecipes.Count)
            {
                return craftingRecipes[recipeIndex] != null ? craftingRecipes[recipeIndex].requirements : null;
            }

            return null;
        }

        if (recipeIndex == 0)
        {
            return craftingRequirements;
        }

        return null;
    }

    public string GetRecipeName(int recipeIndex)
    {
        if (craftingRecipes != null && craftingRecipes.Count > 0)
        {
            if (recipeIndex >= 0 && recipeIndex < craftingRecipes.Count)
            {
                CraftingRecipe recipe = craftingRecipes[recipeIndex];
                if (recipe != null && !string.IsNullOrWhiteSpace(recipe.recipeName))
                {
                    return recipe.recipeName;
                }
            }
        }

        if (RecipeCount > 1)
        {
            return $"Recipe {recipeIndex + 1}";
        }

        return "Recipe";
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
        // if empty, do nothing
        if (inventorySpaceShape == null) { inventorySpaceShape = new List<string>(); }
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
                if (chars[c] != '+' && chars[c] != '-') { chars[c] = '-'; }
            }
            row = new string(chars);

            // pad to max width
            if (row.Length < maxW) { row = row.PadRight(maxW, '-'); }
            inventorySpaceShape[i] = row;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(INV_Item))]
[CanEditMultipleObjects]
public class INV_ItemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Inventory Shape Tool", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox("Normalize will:\nConvert invalid chars to '-'\nPad rows to equal width using '-'", MessageType.Info);

        if (GUILayout.Button("Normalize Inventory Shape"))
        {
            for (int i = 0; i < targets.Length; i++)
            {
                INV_Item item = targets[i] as INV_Item;
                if (item == null)
                {
                    continue;
                }

                Undo.RecordObject(item, "Normalize Inventory Shape");
                item.NormalizeInventoryShape();
                EditorUtility.SetDirty(item);
            }

            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif