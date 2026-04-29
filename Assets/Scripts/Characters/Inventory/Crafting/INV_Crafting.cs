using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Made By: Jason Lodge.
// Summary: Handles crafting capabilities.
// Lives on the player and handles checking what can be crafted,
// setting up the crafting button list and requesting crafts through the server.

public class INV_Crafting : MonoBehaviour
{
    public enum CraftingFilterMode
    {
        All,
        CraftableOnly,
        NonCraftableOnly
    }

    public enum ItemTypeFilterMode
    {
        All = -1,
        Item = INV_Item.ItemType.Item,
        Consumable = INV_Item.ItemType.Consumable,
        Weapon = INV_Item.ItemType.Weapon,
        Tool = INV_Item.ItemType.Tool,
        Resource = INV_Item.ItemType.Resource,
        Placeable = INV_Item.ItemType.Placeable
    }

    [Header("Refs")]
    [SerializeField] private CC_CharacterPlayerController playerController;
    [SerializeField] private INV_Inventory inventory;
    [SerializeField] private INV_PlayerInventoryNet inventoryNet;

    [Header("Crafting UI")]
    [SerializeField] private RectTransform craftingContentRoot;
    [SerializeField] private GameObject craftingButtonPrefab;
    [SerializeField] private RectTransform craftingPanelRectMask;

    [Header("Filter")]
    [SerializeField] private string nameFilter;
    [SerializeField] private CraftingFilterMode filterMode = CraftingFilterMode.All;
    [SerializeField] private ItemTypeFilterMode itemTypeFilterMode = ItemTypeFilterMode.All;

    [Header("Runtime")]
    public List<INV_Item> craftables = new List<INV_Item>();
    public List<INV_Item> nonCraftables = new List<INV_Item>();

    private readonly List<INV_CraftingButtonUI> spawnedButtons = new List<INV_CraftingButtonUI>();

    private bool openedFromCraftingStation;

    public RectTransform CraftingPanelRectMask => craftingPanelRectMask;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<CC_CharacterPlayerController>();
        }

        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (inventoryNet == null)
        {
            inventoryNet = GetComponentInChildren<INV_PlayerInventoryNet>();
        }
    }

    private void OnEnable()
    {
        if (inventoryNet != null)
        {
            inventoryNet.OnCraftRequestFinished += OnCraftRequestFinished;
        }
    }

    private void OnDisable()
    {
        if (inventoryNet != null)
        {
            inventoryNet.OnCraftRequestFinished -= OnCraftRequestFinished;
        }
    }

    // Called by Player Controller (when opening the inventory) and the crafting interact bridge
    public void SetupEverything(bool fromCraftingStation)
    {
        openedFromCraftingStation = fromCraftingStation;

        RebuildCraftingView();

        if (fromCraftingStation && playerController != null)
        {
            playerController.SetMonitoringMenu(true, false);
        }
    }

    public void RebuildCraftingView()
    {
        SetupCraftingLists();
        SetupCraftingButtons();
    }

    public void SetupCraftingLists()
    {
        craftables.Clear();
        nonCraftables.Clear();

        if (inventory == null || INV_ItemDatabase.Instance == null)
        {
            return;
        }

        foreach (INV_Item item in INV_ItemDatabase.Instance.Items)
        {
            if (item == null || !item.Craftable)
            {
                continue;
            }

            if (!PassesFilter(item))
            {
                continue;
            }

            if (CanCraftItemRightNow(item))
            {
                craftables.Add(item);
            }
            else
            {
                nonCraftables.Add(item);
            }
        }

        SortCraftingList(craftables);
        SortCraftingList(nonCraftables);
    }

    public void SetupCraftingButtons()
    {
        ClearButtons();

        if (craftingContentRoot == null || craftingButtonPrefab == null)
        {
            return;
        }

        // craftables first, then non craftables
        SpawnButtonsForList(craftables, true);
        SpawnButtonsForList(nonCraftables, false);
    }

    public void SetNameFilter(string newFilter)
    {
        nameFilter = newFilter != null ? newFilter.Trim() : string.Empty;
        RebuildCraftingView();
    }

    public void SetFilterMode(CraftingFilterMode newFilterMode)
    {
        filterMode = newFilterMode;
        RebuildCraftingView();
    }

    public void SetFilterModeFromDropdown(int dropdownValue)
    {
        if (dropdownValue < 0 || dropdownValue >= System.Enum.GetValues(typeof(CraftingFilterMode)).Length)
        {
            filterMode = CraftingFilterMode.All;
            RebuildCraftingView();
            return;
        }

        filterMode = (CraftingFilterMode)dropdownValue;
        RebuildCraftingView();
    }

    public void SetItemTypeFilterMode(ItemTypeFilterMode newItemTypeFilterMode)
    {
        itemTypeFilterMode = newItemTypeFilterMode;
        RebuildCraftingView();
    }

    public void SetItemTypeFilterModeFromDropdown(int dropdownValue)
    {
        switch (dropdownValue)
        {
            case 0:
                itemTypeFilterMode = ItemTypeFilterMode.All;
                break;

            case 1:
                itemTypeFilterMode = ItemTypeFilterMode.Item;
                break;

            case 2:
                itemTypeFilterMode = ItemTypeFilterMode.Consumable;
                break;

            case 3:
                itemTypeFilterMode = ItemTypeFilterMode.Weapon;
                break;

            case 4:
                itemTypeFilterMode = ItemTypeFilterMode.Tool;
                break;

            case 5:
                itemTypeFilterMode = ItemTypeFilterMode.Resource;
                break;

            case 6:
                itemTypeFilterMode = ItemTypeFilterMode.Placeable;
                break;

            default:
                itemTypeFilterMode = ItemTypeFilterMode.All;
                break;
        }

        RebuildCraftingView();
    }

    private bool PassesFilter(INV_Item item)
    {
        if (item == null)
        {
            return false;
        }

        bool isCraftableNow = CanCraftItemRightNow(item);

        switch (filterMode)
        {
            case CraftingFilterMode.CraftableOnly:
                if (!isCraftableNow)
                {
                    return false;
                }
                break;

            case CraftingFilterMode.NonCraftableOnly:
                if (isCraftableNow)
                {
                    return false;
                }
                break;
        }

        if (itemTypeFilterMode != ItemTypeFilterMode.All)
        {
            if ((int)item.ItemTypeVal != (int)itemTypeFilterMode)
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(nameFilter))
        {
            return true;
        }

        string loweredFilter = nameFilter.ToLowerInvariant();
        string itemName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name.ToLowerInvariant() : string.Empty;
        string itemDescription = !string.IsNullOrWhiteSpace(item.Description) ? item.Description.ToLowerInvariant() : string.Empty;

        if (itemName.Contains(loweredFilter) || itemDescription.Contains(loweredFilter))
        {
            return true;
        }

        for (int i = 0; i < item.RecipeCount; i++)
        {
            string recipeName = item.GetRecipeName(i);
            if (!string.IsNullOrWhiteSpace(recipeName) && recipeName.ToLowerInvariant().Contains(loweredFilter))
            {
                return true;
            }

            List<INV_Item.CraftingStack> recipeRequirements = item.GetRecipeRequirements(i);
            if (recipeRequirements == null)
            {
                continue;
            }

            for (int r = 0; r < recipeRequirements.Count; r++)
            {
                INV_Item.CraftingStack req = recipeRequirements[r];
                if (req?.item == null || string.IsNullOrWhiteSpace(req.item.Name))
                {
                    continue;
                }

                if (req.item.Name.ToLowerInvariant().Contains(loweredFilter))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void SortCraftingList(List<INV_Item> items)
    {
        items.Sort((a, b) =>
        {
            if (a == b)
            {
                return 0;
            }

            if (a == null)
            {
                return 1;
            }

            if (b == null)
            {
                return -1;
            }

            return string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    private void SpawnButtonsForList(List<INV_Item> items, bool interactable)
    {
        for (int i = 0; i < items.Count; i++)
        {
            CreateButton(items[i], interactable);
        }
    }

    private void CreateButton(INV_Item item, bool interactable)
    {
        if (item == null || craftingContentRoot == null || craftingButtonPrefab == null)
        {
            return;
        }

        GameObject go = Instantiate(craftingButtonPrefab, craftingContentRoot);
        go.name = $"CraftButton_{item.Name}";

        INV_CraftingButtonUI buttonUi = go.GetComponent<INV_CraftingButtonUI>();
        if (buttonUi == null)
        {
            Destroy(go);
            return;
        }

        int bestRecipeIndex = GetBestRecipeIndex(item);
        buttonUi.Init(this, item, interactable, bestRecipeIndex);
        spawnedButtons.Add(buttonUi);
    }

    private void ClearButtons()
    {
        for (int i = spawnedButtons.Count - 1; i >= 0; i--)
        {
            if (spawnedButtons[i] != null)
            {
                Destroy(spawnedButtons[i].gameObject);
            }
        }

        spawnedButtons.Clear();

        if (craftingContentRoot == null)
        {
            return;
        }

        for (int i = craftingContentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(craftingContentRoot.GetChild(i).gameObject);
        }
    }

    public void TryCraftItem(INV_Item item, int recipeIndex)
    {
        if (item == null || inventoryNet == null)
        {
            return;
        }

        inventoryNet.RequestCraftItem(item.ItemID, recipeIndex);
    }

    private void OnCraftRequestFinished(bool success, string craftedItemId)
    {
        RebuildCraftingView();
    }

    public bool CanCraftItemRightNow(INV_Item item)
    {
        if (item == null)
        {
            return false;
        }

        for (int i = 0; i < item.RecipeCount; i++)
        {
            if (CanCraftRecipeRightNow(item, i))
            {
                return true;
            }
        }

        return false;
    }

    public bool CanCraftRecipeRightNow(INV_Item item, int recipeIndex)
    {
        if (item == null || inventory == null)
        {
            return false;
        }

        if (!item.CanCraftAnywhere && !openedFromCraftingStation)
        {
            return false;
        }

        List<INV_Item.CraftingStack> recipeRequirements = item.GetRecipeRequirements(recipeIndex);
        if (recipeRequirements == null || recipeRequirements.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < recipeRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = recipeRequirements[i];
            if (req?.item == null || req.amount <= 0)
            {
                continue;
            }

            if (inventory.GetItemCount(req.item.ItemID) < req.amount)
            {
                return false;
            }
        }

        return inventory.CanAddItem(item, item.GetRecipeReturnAmount(recipeIndex));
    }

    public int GetBestRecipeIndex(INV_Item item)
    {
        if (item == null || item.RecipeCount <= 0)
        {
            return 0;
        }

        for (int i = 0; i < item.RecipeCount; i++)
        {
            if (CanCraftRecipeRightNow(item, i))
            {
                return i;
            }
        }

        return 0;
    }

    public string BuildRequirementText(INV_Item item, int recipeIndex)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!item.CanCraftAnywhere && !openedFromCraftingStation)
        {
            return "Needs crafting station";
        }

        List<INV_Item.CraftingStack> recipeRequirements = item.GetRecipeRequirements(recipeIndex);
        if (recipeRequirements == null || recipeRequirements.Count == 0 || !item.Craftable)
        {
            return "Uncraftable";
        }

        List<string> lines = new List<string>();

        if (item.RecipeCount > 1)
        {
            lines.Add(item.GetRecipeName(recipeIndex));
        }

        for (int i = 0; i < recipeRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = recipeRequirements[i];
            if (req?.item == null)
            {
                continue;
            }

            int currentAmount = inventory != null ? inventory.GetItemCount(req.item.ItemID) : 0;
            lines.Add($"{req.item.Name} {currentAmount}/{req.amount}");
        }

        return string.Join("\n", lines);
    }
}