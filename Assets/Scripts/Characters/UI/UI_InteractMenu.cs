using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Simple interact menu ui for interactable objects.
// Builds a list of buttons from the interactable action list.

public class UI_InteractMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GameObject root;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] Transform buttonParent;
    [SerializeField] UI_InteractMenuButton buttonPrefab;

    InteractableObject currentInteractable;
    GameObject currentInteractor;
    CC_CharacterPlayerController currentPlayerController;

    readonly List<UI_InteractMenuButton> spawnedButtons = new List<UI_InteractMenuButton>();

    void Awake()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public bool IsOpen()
    {
        if (root != null) { return root.activeSelf; }
        return gameObject.activeSelf;
    }

    public void OpenMenu(CC_CharacterPlayerController playerController, InteractableObject interactable, GameObject interactor)
    {
        currentPlayerController = playerController;
        currentInteractable = interactable;
        currentInteractor = interactor;

        if (currentInteractable == null) { return; }

        BuildButtons();

        if (titleText != null)
        {
            titleText.SetText(string.IsNullOrWhiteSpace(currentInteractable.interactMenuTitle) ? currentInteractable.name : currentInteractable.interactMenuTitle);
        }

        if (root != null)
        {
            root.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    public void CloseMenu()
    {
        CloseMenu(true);
    }

    public void CloseMenu(bool notifyPlayerController)
    {
        ClearButtons();

        currentInteractable = null;
        currentInteractor = null;

        if (root != null)
        {
            root.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        if (notifyPlayerController && currentPlayerController != null)
        {
            CC_CharacterPlayerController controller = currentPlayerController;
            currentPlayerController = null;
            controller.CloseInteractMenu();
            return;
        }

        currentPlayerController = null;
    }

    void BuildButtons()
    {
        ClearButtons();

        if (currentInteractable == null) { return; }
        if (buttonPrefab == null) { return; }
        if (buttonParent == null) { return; }

        int actionCount = currentInteractable.GetActionCount();

        for (int i = 0; i < actionCount; i++)
        {
            InteractableObject.InteractAction action = currentInteractable.GetAction(i);
            if (action == null) { continue; }

            UI_InteractMenuButton button = Instantiate(buttonPrefab, buttonParent);
            int actionIndex = i;

            button.Setup(action, () =>
                {
                    if (currentInteractable != null)
                    {
                        currentInteractable.Interact(actionIndex, currentInteractor);
                    }

                    CloseMenu();
                }
            );

            spawnedButtons.Add(button);
        }
    }

    void ClearButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
            {
                Destroy(spawnedButtons[i].gameObject);
            }
        }

        spawnedButtons.Clear();
    }
}