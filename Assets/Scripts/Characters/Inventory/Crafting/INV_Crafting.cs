using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Made By: Jason Lodge.
// Summary: Handles crafting capabilities.
// Lives on the player and handles checking what can be crafted,
// setting up the crafting button list and requesting crafts through the server.

public class INV_Crafting : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CC_CharacterPlayerController playerController;
    [SerializeField] private INV_Inventory inventory;
    [SerializeField] private INV_PlayerInventoryNet inventoryNet;

    [Header("Crafting UI")]
    [SerializeField] private RectTransform craftingContentRoot;
    [SerializeField] private GameObject craftingButtonPrefab;

    [Header("Runtime")]
    public List<INV_Item> craftables = new List<INV_Item>();
    public List<INV_Item> nonCraftables = new List<INV_Item>();

    private readonly List<INV_CraftingButtonUI> spawnedButtons = new List<INV_CraftingButtonUI>();

    private bool openedFromCraftingStation;

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

        // set up lists
        SetupCraftingLists();

        // set up buttons
        SetupCraftingButtons();

        if (fromCraftingStation)
        {
            // open inventory menu root
            if (playerController != null)
            {
                playerController.SetMonitoringMenu(true, false);
            }
        }
    }

    public void SetupCraftingLists()
    {
        craftables.Clear();
        nonCraftables.Clear();

        if (inventory == null || INV_ItemDatabase.Instance == null)
        {
            return;
        }

        foreach (var item in INV_ItemDatabase.Instance.Items)
        {
            if (item == null)
            {
                continue;
            }

            // only items actually marked craftable appear in the list
            if (!item.Craftable)
            {
                continue;
            }

            bool canCraft = CanCraftItemRightNow(item);

            if (canCraft)
            {
                craftables.Add(item);
            }
            else
            {
                nonCraftables.Add(item);
            }
        }
    }

    public void SetupCraftingButtons()
    {
        ClearButtons();

        if (craftingContentRoot == null || craftingButtonPrefab == null)
        {
            Debug.LogWarning("INV_Crafting is missing crafting ui refs.", this);
            return;
        }

        // craftables first
        for (int i = 0; i < craftables.Count; i++)
        {
            CreateButton(craftables[i], true);
        }

        // then unavailable craftables
        for (int i = 0; i < nonCraftables.Count; i++)
        {
            CreateButton(nonCraftables[i], false);
        }

        ExpandContentRoot();
    }

    private void CreateButton(INV_Item item, bool interactable)
    {
        if (item == null) { return; }

        GameObject go = Instantiate(craftingButtonPrefab, craftingContentRoot);
        go.name = $"CraftButton_{item.Name}";

        INV_CraftingButtonUI buttonUi = go.GetComponent<INV_CraftingButtonUI>();
        if (buttonUi == null)
        {
            Debug.LogWarning("Crafting button prefab needs INV_CraftingButtonUI.", go);
            Destroy(go);
            return;
        }

        buttonUi.Init(this, item, interactable, GetRequirementText(item));

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

        if (craftingContentRoot == null) { return; }

        for (int i = craftingContentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(craftingContentRoot.GetChild(i).gameObject);
        }
    }

    private void ExpandContentRoot()
    {
        if (craftingContentRoot == null)
        {
            return;
        }

        float totalHeight = 0f;

        VerticalLayoutGroup layoutGroup = craftingContentRoot.GetComponent<VerticalLayoutGroup>();
        float spacing = layoutGroup != null ? layoutGroup.spacing : 0f;

        for (int i = 0; i < craftingContentRoot.childCount; i++)
        {
            RectTransform child = craftingContentRoot.GetChild(i) as RectTransform;
            if (child == null) { continue; }

            totalHeight += child.sizeDelta.y;

            if (i < craftingContentRoot.childCount - 1)
            {
                totalHeight += spacing;
            }
        }

        Vector2 size = craftingContentRoot.sizeDelta;
        size.y = totalHeight;
        craftingContentRoot.sizeDelta = size;

        LayoutRebuilder.ForceRebuildLayoutImmediate(craftingContentRoot);
    }

    public void TryCraftItem(INV_Item item)
    {
        if (item == null) { return; }
        if (inventoryNet == null) { return; }

        inventoryNet.RequestCraftItem(item.ItemID);
    }

    private void OnCraftRequestFinished(bool success, string craftedItemId)
    {
        // rebuild after every craft result so buttons update
        SetupCraftingLists();
        SetupCraftingButtons();
    }

    private bool CanCraftItemRightNow(INV_Item item)
    {
        if (item == null) { return false; }
        if (inventory == null) { return false; }

        if (!item.CanCraftAnywhere && !openedFromCraftingStation)
        {
            return false;
        }

        for (int i = 0; i < item.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = item.CraftingRequirements[i];
            if (req == null || req.item == null || req.amount <= 0)
            {
                continue;
            }

            int currentCount = inventory.GetItemCount(req.item.ItemID);

            if (currentCount < req.amount)
            {
                Debug.Log($"Craft check failed for {item.Name}: needs {req.amount}x {req.item.Name}, only found {currentCount}.", this);
                return false;
            }
        }

        if (!inventory.CanAddItem(item))
        {
            Debug.Log($"Craft check failed for {item.Name}: no room in inventory for crafted item.", this);
            return false;
        }

        return true;
    }

    private string GetRequirementText(INV_Item item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!item.CanCraftAnywhere && !openedFromCraftingStation)
        {
            return "Needs crafting station";
        }

        if (item.CraftingRequirements == null || item.CraftingRequirements.Count == 0 || !item.Craftable)
        {
            return "Uncraftable. If you see this, I messed up somewhere.";
        }

        string text = "";

        for (int i = 0; i < item.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = item.CraftingRequirements[i];
            if (req == null || req.item == null) { continue; }

            int currentAmount = inventory != null ? inventory.GetItemCount(req.item.ItemID) : 0;

            text += $"{req.item.Name} {currentAmount}/{req.amount}";

            if (i < item.CraftingRequirements.Count - 1)
            {
                text += "\n";
            }
        }

        return text;
    }
}