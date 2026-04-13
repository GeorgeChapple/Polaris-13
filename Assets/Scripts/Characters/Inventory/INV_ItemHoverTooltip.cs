using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static INV_Inventory;

// Made By: Jason Lodge.
// Summary: Inventory hover tooltip.
// Shows item name and description and positions itself at mouse position.

public class INV_ItemHoverTooltip : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Vector2 mouseOffset = Vector2.zero;

    [Header("Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemRarityText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;

    private Canvas parentCanvas;
    private RectTransform canvasRect;

    private void Awake()
    {
        if (root == null)
        {
            root = transform as RectTransform;
        }

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            canvasRect = parentCanvas.transform as RectTransform;
        }

        HideImmediate();
    }

    public void Show(INV_Item item)
    {
        if (item == null || root == null)
        {
            HideImmediate();
            return;
        }

        if (itemNameText != null)
        {
            itemNameText.SetText(item.Name);
        }

        if (itemRarityText != null)
        {
            KeyValuePair<string, Color> keyValuePair = GetRarityLabel(item.ItemRarityVal);
            itemRarityText.SetText(keyValuePair.Key);
            itemRarityText.color = keyValuePair.Value;
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.SetText(item.Description);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);

        PositionAtMouse();

        if (!root.gameObject.activeSelf)
        {
            root.gameObject.SetActive(true);
        }
    }

    public KeyValuePair<string,Color> GetRarityLabel(INV_Item.ItemRarity action)
    {
        switch (action)
        {
            case INV_Item.ItemRarity.Common: { return new KeyValuePair<string, Color>("Common", Color.white); }
            case INV_Item.ItemRarity.Uncommon: { return new KeyValuePair<string, Color>("Uncommon", Color.green); }
            case INV_Item.ItemRarity.Rare: { return new KeyValuePair<string, Color>("Rare", Color.blue); }
            case INV_Item.ItemRarity.Epic: { return new KeyValuePair<string, Color>("Epic", Color.magenta); }
        }
        return new KeyValuePair<string, Color>(action.ToString(), Color.white);
    }

    public void HideImmediate()
    {
        if (root != null)
        {
            root.gameObject.SetActive(false);
        }
    }

    public void RefreshPosition()
    {
        if (root == null || !root.gameObject.activeSelf)
        {
            return;
        }

        PositionAtMouse();
    }

    private void PositionAtMouse()
    {
        if (canvasRect == null || root == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        Camera uiCamera = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            mouseScreenPos + mouseOffset,
            uiCamera,
            out localPoint
        );

        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);

        root.anchoredPosition = localPoint;
    }
}