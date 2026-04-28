using UnityEngine;

// Made by: Jason Lodge
// Summary: Warning System Handler.
public class UI_WarningSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform warningParent;
    [SerializeField] private RectTransform warningTransform;

    [Header("Settings")]
    [SerializeField] private WarningData[] warnings;

    [System.Serializable]
    public class WarningData
    {
        public Sprite warningImage;
    }

    private void Update()
    {
        
    }
}
