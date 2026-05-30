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
    [SerializeField, TextArea(1, 5)] private string description;

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

    [Tooltip("Whether the item has been seen by the player, dictates whether any crafting recipes this item is in shows its name.")]
    [SerializeField] private bool unlocked = false;

    [Tooltip("Whether the item is hidden in the crafting menu.")]
    [SerializeField] private bool hidden = false;

    [Header("Shop")]
    [SerializeField] private int retailPrice;
    [SerializeField] private Vector2 shopMultiplierRange = new Vector2(0.75f, 2f);

    [Header("Probability")]
    [SerializeField, Range(0, 100)] private int chanceOfSpawnInChest = 0;
    [SerializeField] private Vector2Int amountSpawnedInChestRange = new Vector2Int(1, 5);
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
        public int amountGiven = 1;
        public bool unlocked = false;
    }

    [Header("Visuals")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    [SerializeField] private bool twoHanded;

    public enum DropCollider { MeshCollider, BoxCollider, SphereCollider }
    [Tooltip("What Collider to use when setting up the item drop.")]
    [SerializeField] private DropCollider dropCollider = DropCollider.MeshCollider;

    [Tooltip("If true, automatically sets scale of collider using bounds check.")]
    [SerializeField] private bool autoColliderScale = true;

    [Tooltip("Only changes collider scale if autoColliderScale is false.")]
    [SerializeField] private float colliderScale = 1f;

    [Header("Equipped Prefab")]
    [Tooltip("Prefab used when this item is equipped.")]
    [SerializeField] private GameObject equippedPrefab;

    [Tooltip("If true, the equipped prefab visual will be overwritten using the item mesh/material/offset/rotation/scale values below.")]
    [SerializeField] private bool applyEquippedPrefabVisuals;

    public enum ForwardAxisRot { X, Y, Z }
    [SerializeField] private ForwardAxisRot forwardAxisRot = ForwardAxisRot.Z;

    [Header("Inventory Mesh Visual")]
    [Tooltip("Local offset applied to the mesh visual when shown in inventory.")]
    [SerializeField] private Vector3 inventoryMeshOffset = Vector3.zero;

    [Tooltip("Local rotation applied to the mesh visual when shown in inventory.")]
    [SerializeField] private Vector3 inventoryMeshRotation = Vector3.zero;

    [Tooltip("Scale applied to the mesh visual when shown in inventory.")]
    [SerializeField] private float inventoryMeshScale = 1f;

    [Header("Hotbar Mesh Visual")]
    [Tooltip("Local offset applied to the mesh visual when shown in hotbar.")]
    [SerializeField] private Vector3 hotbarMestOffset = Vector3.zero;

    [Tooltip("Local rotation applied to the mesh visual when shown in hotbar.")]
    [SerializeField] private Vector3 hotbarMeshRotation = Vector3.zero;

    [Tooltip("Scale applied to the mesh visual when shown in hotbar.")]
    [SerializeField] private float hotbarMeshScale = 1f;

    [Header("Drop Mesh Visual")]
    [Tooltip("Scale applied to the mesh when dropped from inventory.")]
    [SerializeField] private float dropMeshScale = 1f;

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
    public bool Unlocked => unlocked;
    public bool Hidden => hidden;

    public int RetailPrice => retailPrice;
    public Vector2 ShopMultiplierRange => shopMultiplierRange;

    public int ChanceOfSpawnInChest => chanceOfSpawnInChest;
    public Vector2Int AmountSpawnedInChestRange => amountSpawnedInChestRange;
    public int ChanceOfSpawnAsDebris => chanceOfSpawnAsDebris;

    public List<BiomeProbability> BiomeProbablities => biomeProbabilities;

    public bool Stackable => stackable;
    public int MaxStack => stackable ? Mathf.Max(1, maxStack) : 1;

    public bool Craftable => craftable;
    public bool CanCraftAnywhere => canCraftAnywhere;

    public List<CraftingStack> LegacyCraftingRequirements => craftingRequirements;

    public Sprite Icon => icon;
    public Mesh Mesh => mesh;
    public Material Material => material;
    public bool TwoHanded => twoHanded;

    public DropCollider DropColliderVal => dropCollider;
    public bool AutoColliderScale => autoColliderScale;
    public float ColliderScale => Mathf.Max(0.01f, colliderScale);

    public ForwardAxisRot ForwardAxisRotVal => forwardAxisRot;

    public Vector3 InventoryMeshOffset => inventoryMeshOffset;
    public Vector3 InventoryMeshRotation => inventoryMeshRotation;
    public float InventoryMeshScale => inventoryMeshScale;

    public Vector3 HotbarMeshOffset => hotbarMestOffset;
    public Vector3 HotbarMeshRotation => hotbarMeshRotation;
    public float HotbarMeshScale => hotbarMeshScale;

    public float DropMeshScale => dropMeshScale;

    public GameObject EquippedPrefab => equippedPrefab;
    public bool ApplyEquippedPrefabVisuals => applyEquippedPrefabVisuals;
    public Vector3 EquippedMeshOffset => equippedMeshOffset;
    public Vector3 EquippedMeshRotation => equippedMeshRotation;
    public float EquippedMeshScale => equippedMeshScale;

    public Vector3 CraftingMeshOffset => craftingMeshOffset;
    public Vector3 CraftingMeshRotation => craftingMeshRotation;
    public float CraftingMeshScale => craftingMeshScale;

    public List<string> InventorySpaceShape => inventorySpaceShape;
    public Vector2 InventorySpace => inventorySpace;

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

    public int UnlockedRecipeCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < RecipeCount; i++)
            {
                if (IsRecipeUnlocked(i))
                {
                    count++;
                }
            }

            return count;
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

    // return the selected recipe or fall back to the old single recipe list.
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

    // check whether a recipe can currently be shown/used.
    public bool IsRecipeUnlocked(int recipeIndex)
    {
        if (craftingRecipes != null && craftingRecipes.Count > 0)
        {
            if (recipeIndex >= 0 && recipeIndex < craftingRecipes.Count)
            {
                CraftingRecipe recipe = craftingRecipes[recipeIndex];
                return recipe != null && recipe.unlocked;
            }

            return false;
        }
        return false;
    }

    // find the first recipe the player has unlocked.
    public int GetFirstUnlockedRecipeIndex()
    {
        for (int i = 0; i < RecipeCount; i++)
        {
            if (IsRecipeUnlocked(i))
            {
                return i;
            }
        }

        return 0;
    }

    // cycle forwards through unlocked recipes only.
    public int GetNextUnlockedRecipeIndex(int currentRecipeIndex)
    {
        if (RecipeCount <= 0)
        {
            return 0;
        }

        for (int i = 1; i <= RecipeCount; i++)
        {
            int index = currentRecipeIndex + i;

            if (index >= RecipeCount)
            {
                index = 0;
            }

            if (IsRecipeUnlocked(index))
            {
                return index;
            }
        }

        return Mathf.Clamp(currentRecipeIndex, 0, RecipeCount - 1);
    }

    // cycle backwards through unlocked recipes only.
    public int GetPreviousUnlockedRecipeIndex(int currentRecipeIndex)
    {
        if (RecipeCount <= 0)
        {
            return 0;
        }

        for (int i = 1; i <= RecipeCount; i++)
        {
            int index = currentRecipeIndex - i;

            if (index < 0)
            {
                index = RecipeCount - 1;
            }

            if (IsRecipeUnlocked(index))
            {
                return index;
            }
        }

        return Mathf.Clamp(currentRecipeIndex, 0, RecipeCount - 1);
    }

    // convert the real recipe index into the visible unlocked recipe number.
    public int GetUnlockedRecipeDisplayNumber(int recipeIndex)
    {
        int displayNumber = 0;

        for (int i = 0; i < RecipeCount; i++)
        {
            if (!IsRecipeUnlocked(i))
            {
                continue;
            }

            displayNumber++;

            if (i == recipeIndex)
            {
                return displayNumber;
            }
        }

        return 0;
    }

    // reset item and recipe unlock states as editor playmode saves scriptable object assets when edited, build won't.
    public void ResetUnlocked()
    {
        unlocked = false;
        foreach (CraftingRecipe recipe in craftingRecipes)
        {
            recipe.unlocked = false;
        }
    }

    // unlock this item and reveal recipes that depend on it.
    public void UnlockItem()
    {
        unlocked = true;
        INV_ItemDatabase.Instance.UnlockRecipesByItem(this);
    }

    // unlock any recipe that uses the newly found item.
    public void UnlockRecipeByItem(INV_Item item)
    {
        for (int i = 0; i < craftingRecipes.Count; i++)
        {
            for (int j = 0; j < craftingRecipes[i].requirements.Count; j++)
            {
                if (craftingRecipes[i].requirements[j].item == item)
                {
                    craftingRecipes[i].unlocked = true;
                }
            }
        }
    }

    public void HideItem(bool hide)
    {
        hidden = hide;
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

    public int GetRecipeReturnAmount(int recipeIndex)
    {
        if (craftingRecipes != null && craftingRecipes.Count > 0)
        {
            if (recipeIndex >= 0 && recipeIndex < craftingRecipes.Count)
            {
                CraftingRecipe recipe = craftingRecipes[recipeIndex];
                if (recipe != null && recipe.amountGiven > 0)
                {
                    return recipe.amountGiven;
                }
            }
        }
        return 1;
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
// Made By: Jason Lodge
// Summary: Custom inspector tools for normalising item inventory shapes.
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