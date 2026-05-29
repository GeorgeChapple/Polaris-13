using UnityEngine;

// Made By: Jason Lodge
// Summary: Single tutorial objective marker ui.
// Lives on objective marker prefab and lerps to target screen position.
public class UI_TutorialObjectiveMarker : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform root;

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

    // move this marker to a screen position.
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