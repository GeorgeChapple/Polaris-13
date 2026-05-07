using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Made By: Jason Lodge
// Summary: Tutorial manager.
// Waits for the player to spawn, finds player refs, handles ordered tutorial steps,
// checks player inventory, updates objective ui and moves marker over current objective target.
public class TUT_TutorialManager : MonoBehaviour
{
    public enum TutorialCompleteType
    {
        Manual,
        InventoryItems
    }

    [System.Serializable]
    public class TutorialRequiredItem
    {
        [Header("Item")]
        public INV_Item item;
        public int amount = 1;
    }

    [System.Serializable]
    public class TutorialStep
    {
        [Header("Info")]
        public string title;
        [TextArea] public string description;

        [Header("Completion")]
        public TutorialCompleteType completeType = TutorialCompleteType.Manual;
        public List<TutorialRequiredItem> requiredItems = new List<TutorialRequiredItem>();

        [Header("World Marker")]
        public Transform objectiveTarget;
        public Vector3 markerWorldOffset = new Vector3(0f, 1.25f, 0f);

        [Header("Events")]
        public UnityEvent onStepStarted;
        public UnityEvent onStepCompleted;

        [Header("Runtime")]
        public bool completed;
    }

    [Header("Runtime Refs")]
    [SerializeField] private INV_Inventory playerInventory;
    [SerializeField] private Camera playerCamera;

    [Header("Objective UI")]
    [SerializeField] private TextMeshProUGUI objectiveTitleText;
    [SerializeField] private TextMeshProUGUI objectiveDescriptionText;

    [Header("World Objective UI")]
    [SerializeField] private RectTransform markerParent;
    [SerializeField] private UI_TutorialObjectiveMarker markerPrefab;

    [Header("Steps")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    [Header("Settings")]
    [SerializeField] private bool startWhenPlayerRefsFound = true;
    [SerializeField] private bool hideMarkerWhenBehindCamera = true;
    [SerializeField] private float edgePadding = 32f;
    [SerializeField] private string sessionCodeObjectTag = "SessionCode";

    private UI_TutorialObjectiveMarker currentMarker;
    private int currentStepIndex = -1;
    private bool tutorialRunning;
    private bool tutorialFinished;
    private bool findingRefs;

    public int CurrentStepIndex => currentStepIndex;
    public TutorialStep CurrentStep => GetCurrentStep();
    public bool TutorialRunning => tutorialRunning;
    public bool TutorialFinished => tutorialFinished;

    private void Start()
    {
        StartCoroutine(WaitForPlayerRefs());
    }

    private void Update()
    {
        if (!tutorialRunning || tutorialFinished) { return; }

        TutorialStep step = GetCurrentStep();
        if (step == null) { return; }

        if (step.completeType == TutorialCompleteType.InventoryItems)
        {
            if (HasRequiredItems(step))
            {
                CompleteCurrentStep();
            }
        }
    }

    private void LateUpdate()
    {
        UpdateObjectiveMarker();
    }

    private IEnumerator WaitForPlayerRefs()
    {
        if (findingRefs) { yield break; }
        findingRefs = true;

        while (!HasPlayerRefs())
        {
            CachePlayerRefs();

            if (HasPlayerRefs())
            {
                break;
            }

            yield return null;
        }

        findingRefs = false;

        if (startWhenPlayerRefsFound)
        {
            StartTutorial();
        }
    }

    private void CachePlayerRefs()
    {
        if (playerInventory == null)
        {
            playerInventory = FindLocalInventory();
        }

        if (playerCamera == null)
        {
            playerCamera = FindLocalCamera();
        }

        GameObject[] sessionCodeUI = GameObject.FindGameObjectsWithTag(sessionCodeObjectTag);
        foreach (GameObject obj in sessionCodeUI)
        {
            obj.SetActive(false);
        }
    }

    private bool HasPlayerRefs()
    {
        return playerInventory != null && playerCamera != null;
    }

    private INV_Inventory FindLocalInventory()
    {
        INV_Inventory[] inventories = FindObjectsByType<INV_Inventory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < inventories.Length; i++)
        {
            INV_Inventory inventory = inventories[i];
            if (inventory == null) { continue; }

            CC_Movement movement = inventory.GetComponentInParent<CC_Movement>();
            if (movement != null && !movement.IsLocallyControlled()) { continue; }

            return inventory;
        }

        return null;
    }

    private Camera FindLocalCamera()
    {
        CC_CameraController[] controllers = FindObjectsByType<CC_CameraController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            CC_CameraController controller = controllers[i];
            if (controller == null || controller.movement == null) { continue; }
            if (!controller.movement.IsLocallyControlled()) { continue; }
            if (controller.cameraRoot == null) { continue; }

            Camera cam = controller.cameraRoot.GetComponentInChildren<Camera>(true);
            if (cam != null) { return cam; }
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

        return null;
    }

    public void StartTutorial()
    {
        CachePlayerRefs();

        if (!HasPlayerRefs())
        {
            StartCoroutine(WaitForPlayerRefs());
            return;
        }

        if (steps == null || steps.Count == 0)
        {
            tutorialFinished = true;
            tutorialRunning = false;
            ClearObjectiveUI();
            ClearObjectiveMarker();
            return;
        }

        tutorialRunning = true;
        tutorialFinished = false;

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] == null) { continue; }
            steps[i].completed = false;
        }

        SetStep(0);
    }

    public void CompleteCurrentStep()
    {
        TutorialStep step = GetCurrentStep();
        if (step == null) { return; }
        if (step.completed) { return; }

        step.completed = true;
        step.onStepCompleted?.Invoke();

        SetStep(currentStepIndex + 1);
    }

    private void SetStep(int stepIndex)
    {
        if (stepIndex < 0 || steps == null || stepIndex >= steps.Count)
        {
            FinishTutorial();
            return;
        }

        currentStepIndex = stepIndex;

        TutorialStep step = GetCurrentStep();
        if (step == null)
        {
            SetStep(currentStepIndex + 1);
            return;
        }

        RefreshObjectiveUI(step);
        RefreshObjectiveMarker(step);

        step.onStepStarted?.Invoke();
    }

    private void FinishTutorial()
    {
        tutorialRunning = false;
        tutorialFinished = true;
        currentStepIndex = -1;

        ClearObjectiveUI();
        ClearObjectiveMarker();
    }

    private TutorialStep GetCurrentStep()
    {
        if (steps == null) { return null; }
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count) { return null; }

        return steps[currentStepIndex];
    }

    private bool HasRequiredItems(TutorialStep step)
    {
        if (step == null) { return false; }

        if (playerInventory == null)
        {
            playerInventory = FindLocalInventory();
        }

        if (playerInventory == null) { return false; }
        if (step.requiredItems == null || step.requiredItems.Count == 0) { return false; }

        for (int i = 0; i < step.requiredItems.Count; i++)
        {
            TutorialRequiredItem req = step.requiredItems[i];
            if (req == null || req.item == null) { continue; }

            int amount = Mathf.Max(1, req.amount);

            if (!playerInventory.HasItemAmount(req.item.ItemID, amount))
            {
                return false;
            }
        }

        return true;
    }

    private void RefreshObjectiveUI(TutorialStep step)
    {
        if (objectiveTitleText != null)
        {
            objectiveTitleText.SetText(step != null ? step.title : string.Empty);
        }

        if (objectiveDescriptionText != null)
        {
            objectiveDescriptionText.SetText(step != null ? step.description : string.Empty);
        }
    }

    private void ClearObjectiveUI()
    {
        if (objectiveTitleText != null)
        {
            objectiveTitleText.SetText(string.Empty);
        }

        if (objectiveDescriptionText != null)
        {
            objectiveDescriptionText.SetText(string.Empty);
        }
    }

    private void RefreshObjectiveMarker(TutorialStep step)
    {
        if (step == null || step.objectiveTarget == null)
        {
            ClearObjectiveMarker();
            return;
        }

        UI_TutorialObjectiveMarker marker = GetOrCreateMarker();
        if (marker == null) { return; }

        marker.gameObject.SetActive(true);
    }

    private void UpdateObjectiveMarker()
    {
        if (!tutorialRunning || tutorialFinished) { return; }

        TutorialStep step = GetCurrentStep();
        if (step == null || step.objectiveTarget == null)
        {
            ClearObjectiveMarker();
            return;
        }

        UI_TutorialObjectiveMarker marker = GetOrCreateMarker();
        if (marker == null) { return; }

        Camera cam = GetPlayerCamera();
        if (cam == null)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        Vector3 worldPos = step.objectiveTarget.position + step.markerWorldOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0f && hideMarkerWhenBehindCamera)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        screenPos.x = Mathf.Clamp(screenPos.x, edgePadding, Screen.width - edgePadding);
        screenPos.y = Mathf.Clamp(screenPos.y, edgePadding, Screen.height - edgePadding);

        marker.gameObject.SetActive(true);
        marker.SetScreenPosition(screenPos);
    }

    private UI_TutorialObjectiveMarker GetOrCreateMarker()
    {
        if (currentMarker != null) { return currentMarker; }
        if (markerPrefab == null || markerParent == null) { return null; }

        currentMarker = Instantiate(markerPrefab, markerParent);
        return currentMarker;
    }

    private void ClearObjectiveMarker()
    {
        if (currentMarker != null)
        {
            Destroy(currentMarker.gameObject);
        }

        currentMarker = null;
    }

    private Camera GetPlayerCamera()
    {
        if (playerCamera != null) { return playerCamera; }

        playerCamera = FindLocalCamera();
        return playerCamera;
    }
}