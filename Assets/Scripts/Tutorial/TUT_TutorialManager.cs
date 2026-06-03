using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

// Made By: Jason Lodge
// Summary: Shared quest manager.
// Waits for players to spawn, finds all player refs, syncs ordered quest steps,
// checks player inventory, updates local objective ui and moves marker over current objective target.
public class TUT_TutorialManager : NetworkBehaviour
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

    [System.Serializable]
    public class PlayerRefs
    {
        [Header("Refs")]
        public INV_PlayerInventoryNet inventoryNet;
        public Camera playerCamera;
        public CC_CharacterValues characterValues;
        public CC_Movement movement;

        [Header("Runtime")]
        public bool isLocal;
    }

    [Header("Runtime Refs")]
    [SerializeField] private INV_PlayerInventoryNet playerInventoryNet;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private CC_CharacterValues playerCharacterValues;

    [Header("Multiplayer Refs")]
    [SerializeField] private List<PlayerRefs> playerRefs = new List<PlayerRefs>();

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
    [SerializeField] private bool destroySessionCodeObject = false;
    [SerializeField] private string sessionCodeObjectTag = "SessionCode";
    [SerializeField] private bool hideItems = false;
    [SerializeField] private List<INV_Item> itemsToShow = new List<INV_Item>();

    [Header("Quest Settings")]
    [Tooltip("If true, every player must have the required inventory items. If false, any player can complete the item step.")]
    [SerializeField] private bool allPlayersNeedRequiredItems;

    private NetworkVariable<int> syncedStepIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> syncedQuestRunning = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> syncedQuestFinished = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private UI_TutorialObjectiveMarker currentMarker;
    private int currentStepIndex = -1;
    private bool tutorialRunning;
    private bool tutorialFinished;
    private bool findingRefs;
    private bool localQuestSettingsApplied;

    public int CurrentStepIndex => currentStepIndex;
    public TutorialStep CurrentStep => GetCurrentStep();
    public bool TutorialRunning => tutorialRunning;
    public bool TutorialFinished => tutorialFinished;
    public List<PlayerRefs> PlayerRefsList => playerRefs;

    private void Start()
    {
        if (!IsSpawned)
        {
            StartCoroutine(WaitForPlayerRefs());
        }
    }

    public override void OnNetworkSpawn()
    {
        syncedStepIndex.OnValueChanged += OnSyncedStepIndexChanged;
        syncedQuestRunning.OnValueChanged += OnSyncedQuestStateChanged;
        syncedQuestFinished.OnValueChanged += OnSyncedQuestStateChanged;

        ApplySyncedStateToLocal();

        StartCoroutine(WaitForPlayerRefs());
    }

    public override void OnNetworkDespawn()
    {
        syncedStepIndex.OnValueChanged -= OnSyncedStepIndexChanged;
        syncedQuestRunning.OnValueChanged -= OnSyncedQuestStateChanged;
        syncedQuestFinished.OnValueChanged -= OnSyncedQuestStateChanged;
    }

    private void Update()
    {
        CachePlayerRefs();

        if (!tutorialRunning || tutorialFinished) { return; }

        TutorialStep step = GetCurrentStep();
        if (step == null) { return; }

        // only the server decides if inventory steps are done.
        if (IsSpawned && !IsServer) { return; }

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

    // wait until the local player inventory and camera exist.
    private IEnumerator WaitForPlayerRefs()
    {
        if (findingRefs) { yield break; }
        findingRefs = true;

        while (!HasPlayerRefs())
        {
            CachePlayerRefs();

            // dedicated servers do not need local ui refs.
            if (IsSpawned && IsServer && !IsClient)
            {
                break;
            }

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

    // find all players and local refs needed by quest ui.
    private void CachePlayerRefs()
    {
        CacheAllPlayerRefs();

        PlayerRefs localRefs = GetLocalPlayerRefs();

        if (localRefs != null)
        {
            if (playerInventoryNet == null) { playerInventoryNet = localRefs.inventoryNet; }
            if (playerCamera == null) { playerCamera = localRefs.playerCamera; }
            if (playerCharacterValues == null) { playerCharacterValues = localRefs.characterValues; }
        }
        if (destroySessionCodeObject)
        {
            GameObject[] sessionCodeUI = GameObject.FindGameObjectsWithTag(sessionCodeObjectTag);
            foreach (GameObject obj in sessionCodeUI)
            {
                obj.SetActive(false);
            }
        }
    }

    // find every player inventory net, camera, movement and character values.
    private void CacheAllPlayerRefs()
    {
        playerRefs.Clear();

        INV_PlayerInventoryNet[] inventoryNets = FindObjectsByType<INV_PlayerInventoryNet>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < inventoryNets.Length; i++)
        {
            INV_PlayerInventoryNet inventoryNet = inventoryNets[i];
            if (inventoryNet == null) { continue; }

            CC_Movement movement = inventoryNet.GetComponent<CC_Movement>();
            if (movement == null)
            {
                movement = inventoryNet.GetComponentInParent<CC_Movement>();
            }

            CC_CharacterValues characterValues = inventoryNet.GetComponent<CC_CharacterValues>();
            if (characterValues == null)
            {
                characterValues = inventoryNet.GetComponentInChildren<CC_CharacterValues>(true);
            }
            if (characterValues == null)
            {
                characterValues = inventoryNet.GetComponentInParent<CC_CharacterValues>();
            }

            bool isLocal = inventoryNet.IsOwner;

            if (movement != null)
            {
                isLocal = movement.IsLocallyControlled();
            }

            PlayerRefs refs = new PlayerRefs();
            refs.inventoryNet = inventoryNet;
            refs.movement = movement;
            refs.characterValues = characterValues;
            refs.isLocal = isLocal;
            refs.playerCamera = FindCameraForMovement(movement, isLocal);

            playerRefs.Add(refs);
        }
    }

    private bool HasPlayerRefs()
    {
        return playerInventoryNet != null && playerCamera != null;
    }

    private PlayerRefs GetLocalPlayerRefs()
    {
        for (int i = 0; i < playerRefs.Count; i++)
        {
            PlayerRefs refs = playerRefs[i];
            if (refs == null) { continue; }
            if (!refs.isLocal) { continue; }

            return refs;
        }

        return null;
    }

    // find the camera attached to a player movement.
    private Camera FindCameraForMovement(CC_Movement movement, bool isLocal)
    {
        CC_CameraController[] controllers = FindObjectsByType<CC_CameraController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            CC_CameraController controller = controllers[i];
            if (controller == null || controller.movement == null) { continue; }
            if (movement != null && controller.movement != movement) { continue; }
            if (controller.cameraRoot == null) { continue; }

            Camera cam = controller.cameraRoot.GetComponentInChildren<Camera>(true);
            if (cam != null) { return cam; }
        }

        if (isLocal && Camera.main != null)
        {
            return Camera.main;
        }

        return null;
    }

    // reset quest state and begin at the first step.
    public void StartTutorial()
    {
        CachePlayerRefs();

        if (IsSpawned && !IsServer)
        {
            StartTutorialServerRpc();
            return;
        }

        StartTutorialServer();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void StartTutorialServerRpc()
    {
        StartTutorialServer();
    }

    // start the shared quest from the server.
    private void StartTutorialServer()
    {
        if (steps == null || steps.Count == 0)
        {
            FinishTutorialServer();
            return;
        }

        syncedQuestRunning.Value = true;
        syncedQuestFinished.Value = false;

        tutorialRunning = true;
        tutorialFinished = false;

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] == null) { continue; }
            steps[i].completed = false;
        }

        ApplyQuestSettingsClientRpc();
        SetStepServer(0);
    }

    // mark the current step done and move to the next one.
    public void CompleteCurrentStep()
    {
        if (IsSpawned && !IsServer)
        {
            CompleteCurrentStepServerRpc();
            return;
        }

        CompleteCurrentStepServer();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CompleteCurrentStepServerRpc()
    {
        CompleteCurrentStepServer();
    }

    // complete the current shared quest step from the server.
    private void CompleteCurrentStepServer()
    {
        if (!tutorialRunning || tutorialFinished) { return; }

        TutorialStep step = GetCurrentStep();
        if (step == null) { return; }
        if (step.completed) { return; }

        step.completed = true;
        StepCompletedClientRpc(currentStepIndex);

        SetStepServer(currentStepIndex + 1);
    }

    // switch quest state to a specific step.
    private void SetStepServer(int stepIndex)
    {
        if (stepIndex < 0 || steps == null || stepIndex >= steps.Count)
        {
            FinishTutorialServer();
            return;
        }

        currentStepIndex = stepIndex;
        syncedStepIndex.Value = stepIndex;

        TutorialStep step = GetCurrentStep();
        if (step == null)
        {
            SetStepServer(currentStepIndex + 1);
            return;
        }

        StepStartedClientRpc(stepIndex);
    }

    // clean up quest ui and restore hidden items.
    private void FinishTutorialServer()
    {
        tutorialRunning = false;
        tutorialFinished = true;
        currentStepIndex = -1;

        syncedQuestRunning.Value = false;
        syncedQuestFinished.Value = true;
        syncedStepIndex.Value = -1;

        QuestFinishedClientRpc();
    }

    [ClientRpc]
    private void ApplyQuestSettingsClientRpc()
    {
        ApplyLocalQuestSettings();
    }

    [ClientRpc]
    private void StepStartedClientRpc(int stepIndex)
    {
        if (stepIndex < 0 || steps == null || stepIndex >= steps.Count) { return; }

        tutorialRunning = true;
        tutorialFinished = false;
        currentStepIndex = stepIndex;

        ApplyLocalQuestSettings();

        TutorialStep step = GetCurrentStep();
        if (step == null) { return; }

        RefreshObjectiveUI(step);
        RefreshObjectiveMarker(step);

        step.onStepStarted?.Invoke();
    }

    [ClientRpc]
    private void StepCompletedClientRpc(int stepIndex)
    {
        if (stepIndex < 0 || steps == null || stepIndex >= steps.Count) { return; }

        TutorialStep step = steps[stepIndex];
        if (step == null) { return; }

        step.completed = true;
        step.onStepCompleted?.Invoke();
    }

    [ClientRpc]
    private void QuestFinishedClientRpc()
    {
        tutorialRunning = false;
        tutorialFinished = true;
        currentStepIndex = -1;

        localQuestSettingsApplied = false;

        if (INV_ItemDatabase.Instance != null)
        {
            INV_ItemDatabase.Instance.ResetHidden();
        }

        ClearObjectiveUI();
        ClearObjectiveMarker();
    }

    private void OnSyncedStepIndexChanged(int previousValue, int newValue)
    {
        ApplySyncedStateToLocal();
    }

    private void OnSyncedQuestStateChanged(bool previousValue, bool newValue)
    {
        ApplySyncedStateToLocal();
    }

    // apply networked quest state to this client without firing step events.
    private void ApplySyncedStateToLocal()
    {
        tutorialRunning = syncedQuestRunning.Value;
        tutorialFinished = syncedQuestFinished.Value;
        currentStepIndex = syncedStepIndex.Value;

        if (tutorialRunning && !tutorialFinished)
        {
            ApplyLocalQuestSettings();

            TutorialStep step = GetCurrentStep();
            if (step != null)
            {
                RefreshObjectiveUI(step);
                RefreshObjectiveMarker(step);
            }

            return;
        }

        if (tutorialFinished)
        {
            localQuestSettingsApplied = false;

            if (INV_ItemDatabase.Instance != null)
            {
                INV_ItemDatabase.Instance.ResetHidden();
            }

            ClearObjectiveUI();
            ClearObjectiveMarker();
        }
    }

    // apply local player only quest settings.
    private void ApplyLocalQuestSettings()
    {
        if (localQuestSettingsApplied) { return; }

        CachePlayerRefs();

        if (INV_ItemDatabase.Instance != null && hideItems)
        {
            INV_ItemDatabase.Instance.HideItems(itemsToShow, true);
        }

        if (playerCharacterValues != null)
        {
            playerCharacterValues.SetSurvivalToggles(false, false, false);
        }

        localQuestSettingsApplied = true;
    }

    private TutorialStep GetCurrentStep()
    {
        if (steps == null) { return null; }
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count) { return null; }

        return steps[currentStepIndex];
    }

    // check players have all items needed for this step.
    private bool HasRequiredItems(TutorialStep step)
    {
        if (step == null) { return false; }
        if (step.requiredItems == null || step.requiredItems.Count == 0) { return false; }

        CachePlayerRefs();

        if (allPlayersNeedRequiredItems)
        {
            return AllPlayersHaveRequiredItems(step);
        }

        return AnyPlayerHasRequiredItems(step);
    }

    // check if any player has the required items.
    private bool AnyPlayerHasRequiredItems(TutorialStep step)
    {
        for (int i = 0; i < playerRefs.Count; i++)
        {
            PlayerRefs refs = playerRefs[i];
            if (refs == null || refs.inventoryNet == null) { continue; }

            if (InventoryNetHasRequiredItems(refs.inventoryNet, step))
            {
                return true;
            }
        }

        return false;
    }

    // check if every player has the required items.
    private bool AllPlayersHaveRequiredItems(TutorialStep step)
    {
        bool foundPlayer = false;

        for (int i = 0; i < playerRefs.Count; i++)
        {
            PlayerRefs refs = playerRefs[i];
            if (refs == null || refs.inventoryNet == null) { continue; }

            foundPlayer = true;

            if (!InventoryNetHasRequiredItems(refs.inventoryNet, step))
            {
                return false;
            }
        }

        return foundPlayer;
    }

    // check one synced player inventory has all items needed for this step.
    private bool InventoryNetHasRequiredItems(INV_PlayerInventoryNet inventoryNet, TutorialStep step)
    {
        if (inventoryNet == null) { return false; }

        for (int i = 0; i < step.requiredItems.Count; i++)
        {
            TutorialRequiredItem req = step.requiredItems[i];
            if (req == null || req.item == null) { continue; }

            int amount = Mathf.Max(1, req.amount);

            if (!inventoryNet.HasNetworkItemAmount(req.item.ItemID, amount))
            {
                return false;
            }
        }

        return true;
    }

    // show the current quest objective text.
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

    // move the objective marker to the target screen position.
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

    // reuse the current marker or spawn one if needed.
    private UI_TutorialObjectiveMarker GetOrCreateMarker()
    {
        if (currentMarker != null) { return currentMarker; }
        if (markerPrefab == null || markerParent == null) { return null; }

        currentMarker = Instantiate(markerPrefab, markerParent);
        return currentMarker;
    }

    // remove the active quest marker.
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

        CachePlayerRefs();
        return playerCamera;
    }
}