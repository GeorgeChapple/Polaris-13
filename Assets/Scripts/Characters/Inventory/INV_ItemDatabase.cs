using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Global instance for list of items, used by networked inventory to build items in inventory over network by item id.

[CreateAssetMenu(menuName = "Inventory/Item Database")]
public class INV_ItemDatabase : ScriptableObject
{
    [Header("Database Contents")]
    [SerializeField] private List<INV_Item> items = new List<INV_Item>();

    private static INV_ItemDatabase instance;
    private Dictionary<string, INV_Item> itemsById;

    public static INV_ItemDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<INV_ItemDatabase>("Inventory/INV_ItemDatabase");

                if (instance == null)
                {
                    Debug.LogError("Could not load INV_ItemDatabase from Resources/Inventory/INV_ItemDatabase.");
                }
                else
                {
                    instance.BuildLookup();
                }
            }

            return instance;
        }
    }

    public IReadOnlyList<INV_Item> Items => items;

    public void SetItems(List<INV_Item> newItems)
    {
        items = newItems ?? new List<INV_Item>();
        BuildLookup();
    }

    public void ResetItemsUnlocked()
    {
        foreach (INV_Item item in items)
        {
            item.ResetUnlocked();
        }
    }

    public void UnlockRecipesByItem(INV_Item item)
    {
        foreach (INV_Item itemCheck in items)
        {
            if (itemCheck != item)
            {
                itemCheck.UnlockRecipeByItem(item);
            }
        }
    }

    public INV_Item GetItemById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        if (itemsById == null)
        {
            BuildLookup();
        }

        itemsById.TryGetValue(itemId, out INV_Item item);
        return item;
    }

    public List<string> GetAllItemIds()
    {
        List<string> allIds = new List<string>();

        foreach (var item in items) { allIds.Add(item.ItemID); }

        return allIds;
    }

    public bool ContainsItemId(string itemId)
    {
        return GetItemById(itemId) != null;
    }

    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        if (itemsById == null)
        {
            itemsById = new Dictionary<string, INV_Item>();
        }
        else
        {
            itemsById.Clear();
        }

        if (items == null)
        {
            items = new List<INV_Item>();
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            INV_Item item = items[i];
            if (item == null) { continue; }

            string id = item.ItemID;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"INV_Item '{item.name}' has an empty ItemID and will be skipped in the database.", item);
                continue;
            }

            if (itemsById.ContainsKey(id))
            {
                Debug.LogWarning($"Duplicate INV_Item ItemID '{id}' found. Keeping first entry, skipping '{item.name}'.", item);
                continue;
            }

            itemsById.Add(id, item);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(INV_ItemDatabase))]
public class INV_ItemDatabaseEditor : Editor
{
    private const string DatabaseAssetPath = "Assets/Resources/Inventory/INV_ItemDatabase.asset";

    [MenuItem("Inventory/Rebuild Item Database")]
    public static void RebuildDatabase()
    {
        EnsureFolders();

        INV_ItemDatabase database = AssetDatabase.LoadAssetAtPath<INV_ItemDatabase>(DatabaseAssetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<INV_ItemDatabase>();
            AssetDatabase.CreateAsset(database, DatabaseAssetPath);
        }

        string[] guids = AssetDatabase.FindAssets("t:INV_Item");
        List<INV_Item> foundItems = new List<INV_Item>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            INV_Item item = AssetDatabase.LoadAssetAtPath<INV_Item>(path);

            if (item == null) { continue; }
            foundItems.Add(item);
        }

        foundItems.Sort((a, b) => string.CompareOrdinal(a.ItemID, b.ItemID));

        Undo.RecordObject(database, "Rebuild Item Database");
        database.SetItems(foundItems);
        EditorUtility.SetDirty(database);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Rebuilt INV_ItemDatabase with {foundItems.Count} items at '{DatabaseAssetPath}'.", database);
    }

    [MenuItem("Inventory/Select Item Database")]
    public static void SelectDatabase()
    {
        INV_ItemDatabase database = AssetDatabase.LoadAssetAtPath<INV_ItemDatabase>(DatabaseAssetPath);

        if (database == null)
        {
            Debug.LogWarning("INV_ItemDatabase does not exist yet. Use Inventory > Rebuild Item Database first.");
            return;
        }

        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
    }

    [MenuItem("Inventory/Reset All Items and Recipes Unlocked")]
    public static void ResetUnlocked()
    {
        INV_ItemDatabase database = AssetDatabase.LoadAssetAtPath<INV_ItemDatabase>(DatabaseAssetPath);
        database.ResetItemsUnlocked();
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Inventory"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Inventory");
        }
    }
}
#endif