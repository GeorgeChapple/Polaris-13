using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static RS_WarningSystem;

// Made by: Jason Lodge
// Summary: Warning System listener and local handler for player.
public class UI_WarningSystem : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform warningParent;
    [SerializeField] private GameObject warningPrefab;

    [Header("Settings")]
    [SerializeField] private WarningData[] warnings;

    private Image[] warningUIs; // runtime refs to images on ui

    private void Update()
    {
        // tick active warnings on ui.
        foreach (var warning in warnings)
        {
            if (warning != null)
            {
                if (warning.active)
                {
                    // needs to flash.
                }
            }
        }
    }

    public void CallWarningByName(string warningName)
    {

    }
    public void EndWarningByName(string warningName)
    {

    }
}
