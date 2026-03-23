using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Data Holder for all items.
[CreateAssetMenu(menuName = "Inventory/Item")]
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

    [Header("Item Use Scripts")]
    [Tooltip("Assembly qualified type names for MonoBehaviours to add to the equipped item at runtime.")]
    [SerializeField] private List<string> itemUseScriptTypeNames = new List<string>();

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

    public IReadOnlyList<string> ItemUseScriptTypeNames => itemUseScriptTypeNames;
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

    public List<Type> GetItemUseScriptTypes()
    {
        List<Type> result = new List<Type>();

        if (itemUseScriptTypeNames == null) { return result; }

        for (int i = 0; i < itemUseScriptTypeNames.Count; i++)
        {
            string typeName = itemUseScriptTypeNames[i];
            if (string.IsNullOrWhiteSpace(typeName)) { continue; }

            Type type = Type.GetType(typeName);
            if (type == null)
            {
                Debug.LogWarning($"Could not resolve item use script type '{typeName}' on item '{name}'.", this);
                continue;
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                Debug.LogWarning($"Type '{typeName}' is not a MonoBehaviour on item '{name}'.", this);
                continue;
            }

            result.Add(type);
        }

        return result;
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

#if UNITY_EDITOR
    public void Editor_SetItemUseScriptTypeNames(List<string> newTypeNames)
    {
        itemUseScriptTypeNames = newTypeNames ?? new List<string>();
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(INV_Item))]
public class INV_ItemEditor : Editor
{
    private readonly List<MonoScript> scriptRefs = new List<MonoScript>();
    private bool scriptRefsLoaded;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        INV_Item item = (INV_Item)target;
        if (item == null) { return; }

        EditorGUILayout.Space(8);

        DrawItemUseScriptsSection(item);

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

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawItemUseScriptsSection(INV_Item item)
    {
        LoadScriptRefsIfNeeded(item);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Item Use Script Picker", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assign MonoBehaviour scripts here. These script types will be added to the equipped item at runtime using AddComponent(type).", MessageType.Info);

        int removeIndex = -1;

        for (int i = 0; i < scriptRefs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            MonoScript newScript = (MonoScript)EditorGUILayout.ObjectField(
                $"Use Script {i + 1}",
                scriptRefs[i],
                typeof(MonoScript),
                false);

            if (newScript != scriptRefs[i])
            {
                scriptRefs[i] = ValidateMonoBehaviourScript(newScript);
                SaveScriptRefs(item);
            }

            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                removeIndex = i;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
        {
            scriptRefs.RemoveAt(removeIndex);
            SaveScriptRefs(item);
        }

        if (GUILayout.Button("Add Use Script"))
        {
            scriptRefs.Add(null);
            SaveScriptRefs(item);
        }

        EditorGUILayout.EndVertical();
    }

    private void LoadScriptRefsIfNeeded(INV_Item item)
    {
        if (scriptRefsLoaded) { return; }
        scriptRefsLoaded = true;

        scriptRefs.Clear();

        IReadOnlyList<string> typeNames = item.ItemUseScriptTypeNames;
        if (typeNames == null) { return; }

        for (int i = 0; i < typeNames.Count; i++)
        {
            string typeName = typeNames[i];

            if (string.IsNullOrWhiteSpace(typeName))
            {
                scriptRefs.Add(null);
                continue;
            }

            Type type = Type.GetType(typeName);
            if (type == null)
            {
                scriptRefs.Add(null);
                continue;
            }

            scriptRefs.Add(FindMonoScriptByType(type));
        }
    }

    private void SaveScriptRefs(INV_Item item)
    {
        List<string> typeNames = new List<string>();

        for (int i = 0; i < scriptRefs.Count; i++)
        {
            MonoScript script = scriptRefs[i];

            if (script == null)
            {
                typeNames.Add(string.Empty);
                continue;
            }

            Type type = script.GetClass();
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type) || type.IsAbstract)
            {
                typeNames.Add(string.Empty);
                continue;
            }

            typeNames.Add(type.AssemblyQualifiedName);
        }

        Undo.RecordObject(item, "Change Item Use Scripts");
        item.Editor_SetItemUseScriptTypeNames(typeNames);
        EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssets();
    }

    private MonoScript ValidateMonoBehaviourScript(MonoScript script)
    {
        if (script == null) { return null; }

        Type type = script.GetClass();
        if (type == null)
        {
            Debug.LogWarning($"'{script.name}' does not define a valid class.");
            return null;
        }

        if (!typeof(MonoBehaviour).IsAssignableFrom(type))
        {
            Debug.LogWarning($"'{script.name}' is not a MonoBehaviour.");
            return null;
        }

        if (type.IsAbstract)
        {
            Debug.LogWarning($"'{script.name}' is abstract and cannot be added as a component.");
            return null;
        }

        return script;
    }

    private MonoScript FindMonoScriptByType(Type type)
    {
        string[] guids = AssetDatabase.FindAssets("t:MonoScript");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

            if (script == null) { continue; }
            if (script.GetClass() == type)
            {
                return script;
            }
        }

        return null;
    }
}
#endif