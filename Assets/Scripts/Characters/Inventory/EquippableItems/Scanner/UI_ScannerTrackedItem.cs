using TMPro;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Single scanner ui entry for one scanned item.
public class UI_ScannerTrackedItem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI distanceText;

    public RectTransform Root => root != null ? root : transform as RectTransform;

    public void SetData(string itemName, float distance)
    {
        if (nameText != null)
        {
            nameText.SetText(itemName);
        }

        if (distanceText != null)
        {
            distanceText.SetText($"{Mathf.RoundToInt(distance)}m");
        }
    }

    public void SetScreenPosition(Vector2 screenPosition)
    {
        RectTransform rt = Root;
        if (rt == null) { return; }

        rt.position = screenPosition;
    }
}