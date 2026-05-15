using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: Crafting button, spawned by INV_Crafting as a prefab.
// Displays the item name, requirements text and mesh visual,
// then calls back into INV_Crafting when pressed.

public class INV_CraftingButtonUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI requirementsText;

    [Header("Recipe UI")]
    [SerializeField] private Button previousRecipeButton;
    [SerializeField] private Button nextRecipeButton;
    [SerializeField] private TextMeshProUGUI recipeIndexText;

    [Header("Mesh Visual")]
    [SerializeField] private GameObject meshSourceObject;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

    private INV_Crafting crafting;
    private INV_Item item;
    private int selectedRecipeIndex;
    private bool itemInteractable;
    private RectTransform maskRect;

    public void Init(INV_Crafting craftingRef, INV_Item itemRef, bool interactable, int startingRecipeIndex)
    {
        crafting = craftingRef;
        item = itemRef;
        itemInteractable = interactable;
        selectedRecipeIndex = Mathf.Max(0, startingRecipeIndex);
        maskRect = crafting.CraftingPanelRectMask;

        SnapToUnlockedRecipe();

        ApplyMeshVisuals();
        WireButtons();
        RefreshVisuals();
    }

    private void Update()
    {
        UpdateMeshRendererVisibility();
    }

    private void UpdateMeshRendererVisibility()
    {
        if (meshRenderer == null || maskRect == null)
        {
            return;
        }

        bool validVisual = item != null && item.Mesh != null && item.Material != null;

        if (!validVisual)
        {
            meshRenderer.enabled = false;
            return;
        }

        Bounds meshBounds = meshRenderer.bounds;
        Bounds viewportBounds = GetRectTransformWorldBounds(maskRect);

        bool overlapsViewport = meshBounds.Intersects(viewportBounds);

        if (meshRenderer.enabled != overlapsViewport)
        {
            meshRenderer.enabled = overlapsViewport;
        }
    }

    private Bounds GetRectTransformWorldBounds(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Bounds bounds = new Bounds(corners[0], Vector3.zero);

        for (int i = 1; i < corners.Length; i++)
        {
            bounds.Encapsulate(corners[i]);
        }

        return bounds;
    }

    private void WireButtons()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnPressed);
        }

        if (previousRecipeButton != null)
        {
            previousRecipeButton.onClick.RemoveAllListeners();
            previousRecipeButton.onClick.AddListener(PreviousRecipe);
        }

        if (nextRecipeButton != null)
        {
            nextRecipeButton.onClick.RemoveAllListeners();
            nextRecipeButton.onClick.AddListener(NextRecipe);
        }
    }

    private void SnapToUnlockedRecipe()
    {
        if (item == null || item.RecipeCount <= 0)
        {
            selectedRecipeIndex = 0;
            return;
        }

        if (item.IsRecipeUnlocked(selectedRecipeIndex))
        {
            return;
        }

        selectedRecipeIndex = item.GetFirstUnlockedRecipeIndex();
    }

    private void RefreshVisuals()
    {
        if (item == null)
        {
            ApplyText("Null Item");
            ApplyRecipeUi();
            SetButtonInteractable(false);
            return;
        }

        if (item.RecipeCount > 0)
        {
            selectedRecipeIndex = Mathf.Clamp(selectedRecipeIndex, 0, item.RecipeCount - 1);
        }
        else
        {
            selectedRecipeIndex = 0;
        }

        SnapToUnlockedRecipe();

        if (!item.IsRecipeUnlocked(selectedRecipeIndex))
        {
            ApplyText("Recipe locked");
            ApplyRecipeUi();
            SetButtonInteractable(false);
            return;
        }

        string requirementString = crafting != null ? crafting.BuildRequirementText(item, selectedRecipeIndex) : string.Empty;
        ApplyText(requirementString);
        ApplyRecipeUi();

        bool canCraftThisRecipe = crafting != null && crafting.CanCraftRecipeRightNow(item, selectedRecipeIndex);
        SetButtonInteractable(itemInteractable && canCraftThisRecipe);
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void ApplyText(string requirementString)
    {
        if (itemNameText != null)
        {
            itemNameText.SetText(item != null ? $"{(item.Unlocked == true ? item.Name : "???")} x{item.GetRecipeReturnAmount(selectedRecipeIndex)}" : "Null Item");
        }

        if (requirementsText != null)
        {
            requirementsText.SetText(requirementString);
        }
    }

    private void ApplyRecipeUi()
    {
        int unlockedRecipeCount = item != null ? item.UnlockedRecipeCount : 0;
        bool hasMultipleRecipes = item != null && unlockedRecipeCount > 1;

        if (recipeIndexText != null)
        {
            if (hasMultipleRecipes)
            {
                recipeIndexText.gameObject.SetActive(true);
                recipeIndexText.SetText($"{item.GetUnlockedRecipeDisplayNumber(selectedRecipeIndex)}/{unlockedRecipeCount}");
            }
            else
            {
                recipeIndexText.gameObject.SetActive(false);
            }
        }

        if (previousRecipeButton != null)
        {
            previousRecipeButton.gameObject.SetActive(hasMultipleRecipes);
            previousRecipeButton.interactable = hasMultipleRecipes;
        }

        if (nextRecipeButton != null)
        {
            nextRecipeButton.gameObject.SetActive(hasMultipleRecipes);
            nextRecipeButton.interactable = hasMultipleRecipes;
        }
    }

    private void ApplyMeshVisuals()
    {
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = item != null ? item.Mesh : null;
        }

        if (meshRenderer == null)
        {
            return;
        }

        bool validVisual = item != null && item.Mesh != null && item.Material != null;

        meshRenderer.enabled = validVisual;
        if (!validVisual)
        {
            return;
        }

        meshRenderer.sharedMaterial = item.Material;
        meshRenderer.transform.localPosition = item.CraftingMeshOffset;
        meshRenderer.transform.localRotation = Quaternion.Euler(item.CraftingMeshRotation);
        meshRenderer.transform.localScale = Vector3.one * Mathf.Max(0f, item.CraftingMeshScale);
    }

    public void NextRecipe()
    {
        if (item == null || item.UnlockedRecipeCount <= 1)
        {
            return;
        }

        selectedRecipeIndex = item.GetNextUnlockedRecipeIndex(selectedRecipeIndex);

        RefreshVisuals();
    }

    public void PreviousRecipe()
    {
        if (item == null || item.UnlockedRecipeCount <= 1)
        {
            return;
        }

        selectedRecipeIndex = item.GetPreviousUnlockedRecipeIndex(selectedRecipeIndex);

        RefreshVisuals();
    }

    private void OnPressed()
    {
        if (crafting == null || item == null)
        {
            return;
        }

        crafting.TryCraftItem(item, selectedRecipeIndex);
    }
}