using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static INV_Inventory;

// Made By: Jason Lodge.
// Summary: Inventory hover tooltip.
// Shows item name, rarity, item type, equipment benefits and description and positions itself at mouse position.

public class INV_ItemHoverTooltip : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Vector2 mouseOffset = Vector2.zero;

    [Header("Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemRarityText;
    [SerializeField] private TextMeshProUGUI itemTypeText;
    [SerializeField] private TextMeshProUGUI itemEquipmentBenefitsText;
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

        if (itemTypeText != null)
        {
            itemTypeText.SetText(GetItemTypeLabel(item));
        }

        if (itemEquipmentBenefitsText != null)
        {
            string benefits = GetEquipmentBenefitsText(item);
            itemEquipmentBenefitsText.SetText(benefits);
            itemEquipmentBenefitsText.gameObject.SetActive(!string.IsNullOrWhiteSpace(benefits));
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

    public KeyValuePair<string, Color> GetRarityLabel(INV_Item.ItemRarity action)
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

    private string GetItemTypeLabel(INV_Item item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (item.ItemTypeVal == INV_Item.ItemType.Equipment && item.EquipmentTypeVal != INV_Item.EquipmentType.None)
        {
            return $"{item.ItemTypeVal} - {item.EquipmentTypeVal}";
        }

        return item.ItemTypeVal.ToString();
    }

    private string GetEquipmentBenefitsText(INV_Item item)
    {
        if (item == null || !item.PassiveEquipment)
        {
            return string.Empty;
        }

        switch (item.EquipmentTypeVal)
        {
            case INV_Item.EquipmentType.Oxygen:
                return GetOxygenBenefitsText(item);

            case INV_Item.EquipmentType.Thruster:
                return GetThrusterBenefitsText(item);

            case INV_Item.EquipmentType.Radiation:
                return "Radiation protection placeholder.";

            case INV_Item.EquipmentType.None:
                return string.Empty;
        }

        return string.Empty;
    }

    private string GetOxygenBenefitsText(INV_Item item)
    {
        List<string> lines = new List<string>();

        if (!Mathf.Approximately(item.AdditionalMaxOxygen, 0f))
        {
            lines.Add($"+{item.AdditionalMaxOxygen} Max Oxygen");
        }

        if (!Mathf.Approximately(item.AdditionalOxygenRegenPerSecond, 0f))
        {
            lines.Add($"+{item.AdditionalOxygenRegenPerSecond}/s Oxygen Regen");
        }

        return string.Join("\n", lines);
    }

    private string GetThrusterBenefitsText(INV_Item item)
    {
        List<string> lines = new List<string>();

        if (!Mathf.Approximately(item.AdditionalGroundThrusterAccel, 0f))
        {
            lines.Add($"+{item.AdditionalGroundThrusterAccel} Ground Thruster Accel");
        }

        if (!Mathf.Approximately(item.AdditionalGroundThrusterUpSpeedCap, 0f))
        {
            lines.Add($"+{item.AdditionalGroundThrusterUpSpeedCap} Ground Thruster Up Speed Cap");
        }

        if (!Mathf.Approximately(item.AdditionalThrusterAccel, 0f))
        {
            lines.Add($"+{item.AdditionalThrusterAccel} Thruster Accel");
        }

        if (!Mathf.Approximately(item.AdditionalSpaceStabilisationAccel, 0f))
        {
            lines.Add($"+{item.AdditionalSpaceStabilisationAccel} Space Stabilisation Accel");
        }

        return string.Join("\n", lines);
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