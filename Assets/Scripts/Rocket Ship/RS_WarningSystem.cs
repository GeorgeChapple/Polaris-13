using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Made by: Jason Lodge
// Summary: Warning System Handler.
public class RS_WarningSystem : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject[] warningLights;

    [Header("Settings")]
    [SerializeField] private WarningData[] warnings;
    [Tooltip("Called by outside sources.")]
    [SerializeField] private UnityEvent<string>[] warningEvents;

    [System.Serializable]
    public class WarningData
    {
        public string name;
        public Image imageToAffect;
        public Sprite warningSprite;
        public int tickRate;
        public bool shipWarning;
        public bool active;
    }

    private void Update()
    {
        // tick active warnings on ui.
    }

    public void CallWarningByName(string warningName)
    {

    }

    public void EndWarningByName(string warningName)
    {

    }
}
