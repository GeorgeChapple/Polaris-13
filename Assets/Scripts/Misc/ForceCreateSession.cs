using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ForceCreateSession: MonoBehaviour
{
    [SerializeField] private TMP_InputField inputFieldToUpdate;
    [SerializeField] private Button buttonToUpdate;
    [SerializeField] private string sessionName;

    void LateUpdate()
    {
        if (inputFieldToUpdate != null)
        {
            inputFieldToUpdate.text = sessionName;
        }
        if (buttonToUpdate != null)
        {
            buttonToUpdate.interactable = true;
        }
    }
}
