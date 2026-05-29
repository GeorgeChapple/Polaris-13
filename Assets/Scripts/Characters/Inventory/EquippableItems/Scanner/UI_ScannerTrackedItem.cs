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

    [Header("Movement")]
    [Tooltip("How fast the marker lerps to its target screen position.")]
    [SerializeField] private float moveLerpSpeed = 12f;

    private Vector2 targetScreenPosition;
    private bool hasTargetPosition;

    public RectTransform Root => root != null ? root : transform as RectTransform;

    private void LateUpdate()
    {
        RectTransform rt = Root;
        if (rt == null) { return; }
        if (!hasTargetPosition) { return; }

        Vector2 currentPosition = rt.position;
        Vector2 newPosition = Vector2.Lerp(currentPosition, targetScreenPosition, Time.deltaTime * moveLerpSpeed);

        rt.position = newPosition;
    }

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

    // moves this marker to a screen position.
    public void SetScreenPosition(Vector2 screenPosition)
    {
        RectTransform rt = Root;
        if (rt == null) { return; }

        targetScreenPosition = screenPosition;

        if (!hasTargetPosition)
        {
            hasTargetPosition = true;
            rt.position = screenPosition;
        }
    }
}