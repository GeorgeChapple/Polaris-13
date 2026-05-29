using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: Inventory item right click context menu.
// Builds from item type and runtime item state.

public class INV_ItemContextMenu : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Vector2 mouseOffset = Vector2.zero;
    [Tooltip("Needs to be higher than 0, or buttons don't work.")]
    [SerializeField] private float disappearDelay = 0.1f;

    [Header("Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform buttonRoot;
    [SerializeField] private Button buttonPrefab;

    private Canvas parentCanvas;
    private RectTransform canvasRect;

    private INV_Inventory inventory;
    private INV_Inventory.ItemInstance currentItem;
    private readonly List<Button> spawnedButtons = new List<Button>();

    public bool IsOpen => root != null && root.gameObject.activeSelf;


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

    private void Update()
    {
        bool mouseDown = Mouse.current.leftButton.isPressed;
        if (mouseDown) { StartCoroutine(WaitToHide()); }
    }

    IEnumerator WaitToHide()
    {
        yield return new WaitForSeconds(disappearDelay);
        HideImmediate();
        yield return null;
    }

    // setup this UI element with its target data.
    public void Init(INV_Inventory inventoryRef)
    {
        inventory = inventoryRef;
    }

    public void Show(INV_Inventory.ItemInstance item, Vector2 screenPoint)
    {
        if (inventory == null || item == null || item.data == null || root == null || buttonRoot == null || buttonPrefab == null)
        {
            HideImmediate();
            return;
        }

        currentItem = item;

        ClearButtons();

        List<INV_Inventory.ContextActionType> actions = inventory.GetContextActionsForItem(item);
        if (actions == null || actions.Count == 0)
        {
            HideImmediate();
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            INV_Inventory.ContextActionType action = actions[i];

            Button b = Instantiate(buttonPrefab, buttonRoot);
            spawnedButtons.Add(b);

            TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.SetText(inventory.GetContextActionLabel(action));
            }

            INV_Inventory.ContextActionType cachedAction = action;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() =>
            {
                if (inventory != null && currentItem != null)
                {
                    inventory.ExecuteContextAction(currentItem, cachedAction);
                }

                HideImmediate();
            });
        }

        PositionAtScreenPoint(screenPoint);
        root.gameObject.SetActive(true);
    }

    public void HideImmediate()
    {
        currentItem = null;
        ClearButtons();

        if (root != null)
        {
            root.gameObject.SetActive(false);
        }
    }

    // remove old crafting buttons before rebuilding the view.
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
    }

    private void PositionAtScreenPoint(Vector2 screenPoint)
    {
        if (canvasRect == null || root == null)
        {
            return;
        }

        Camera uiCamera = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out localPoint);

        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = localPoint + mouseOffset;
    }
}