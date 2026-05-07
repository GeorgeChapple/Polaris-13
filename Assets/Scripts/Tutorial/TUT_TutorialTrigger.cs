using UnityEngine;

// Made By: Jason Lodge
// Summary: Tutorial trigger.
// Lives on trigger collider and completes the current tutorial step when player enters.
public class TUT_TutorialTrigger : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TUT_TutorialManager tutorialManager;

    [Header("Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneShot = true;

    private bool used;

    private void OnTriggerEnter(Collider other)
    {
        if (oneShot && used) { return; }
        if (other == null) { return; }

        if (!string.IsNullOrWhiteSpace(playerTag) && !other.CompareTag(playerTag))
        {
            return;
        }

        if (tutorialManager == null)
        {
            tutorialManager = FindFirstObjectByType<TUT_TutorialManager>();
        }

        if (tutorialManager == null) { return; }

        used = true;
        tutorialManager.CompleteCurrentStep();
    }
}