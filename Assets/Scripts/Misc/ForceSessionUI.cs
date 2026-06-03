using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

public class ForceSessionUI : MonoBehaviour
{
    [SerializeField] private bool forceInputFieldText;
    [SerializeField] private TMP_InputField inputFieldToUpdate;
    [SerializeField] private Button buttonToUpdate;
    [SerializeField] private string sessionName;

    void LateUpdate()
    {
        if (forceInputFieldText)
        {
            if (inputFieldToUpdate != null)
            {
                inputFieldToUpdate.text = sessionName;
            }
        }
        if (buttonToUpdate != null)
        {
            if (!inputFieldToUpdate.text.IsNullOrEmpty())
            {
                buttonToUpdate.interactable = true;
            }
        }
    }
}
