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

            if (CanCraftItemRightNow(item))
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
            return;
        }

        SpawnButtonsForList(craftables, true);
        SpawnButtonsForList(nonCraftables, false);

        ExpandContentRoot();
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

        buttonUi.Init(this, item, interactable, BuildRequirementText(item));
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
            if (child == null)
            {
                continue;
            }

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
        if (item == null || inventoryNet == null)
        {
            return;
        }

        inventoryNet.RequestCraftItem(item.ItemID);
    }

    private void OnCraftRequestFinished(bool success, string craftedItemId)
    {
        RebuildCraftingView();
    }

    public bool CanCraftItemRightNow(INV_Item item)
    {
        if (item == null || inventory == null)
        {
            return false;
        }

        if (!item.CanCraftAnywhere && !openedFromCraftingStation)
        {
            return false;
        }

        for (int i = 0; i < item.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = item.CraftingRequirements[i];
            if (req?.item == null || req.amount <= 0)
            {
                continue;
            }

            if (inventory.GetItemCount(req.item.ItemID) < req.amount)
            {
                return false;
            }
        }

        return inventory.CanAddItem(item);
    }

    private string BuildRequirementText(INV_Item item)
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
            return "Uncraftable";
        }

        List<string> lines = new List<string>();

        for (int i = 0; i < item.CraftingRequirements.Count; i++)
        {
            INV_Item.CraftingStack req = item.CraftingRequirements[i];
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