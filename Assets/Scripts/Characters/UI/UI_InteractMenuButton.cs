using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Made by: Jason Lodge
// Summary: Simple interact menu button ui entry.

public class UI_InteractMenuButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI nameText;

    public void Setup(InteractableObject.InteractAction action, UnityAction onClicked)
    {
        if (action == null) { return; }

        if (nameText != null)
        {
            nameText.SetText(string.IsNullOrWhiteSpace(action.actionName) ? "Interact" : action.actionName);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();

            if (onClicked != null)
            {
                button.onClick.AddListener(onClicked);
            }
        }
    }
}