using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Scanner usable item, scans nearby item drops into overlay ui acts like a camera.
// Scan -> Analyse -> Show Items -> Remove all overlay UIs after delay

public class IT_Scanner : CC_INV_UsableItems
{
    private string itemId;
    private NetworkObjectReference senderRef;

    [Header("Refs")]
    [SerializeField] private UI_ScannerOverlay scannerOverlay;
    [SerializeField] private TextMeshProUGUI scannerWorldText;

    [Header("Scan")]
    [SerializeField] private float scanRange = 25f;
    [SerializeField, Range(1f, 179f)] private float scanConeAngle = 50f;
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField] private bool requireLineOfSight = false;

    [Header("Timing")]
    [Tooltip("How long the scanner takes to analyse the captured scan.")]
    [SerializeField] private float scanAnalysisDelay = 1f;

    [Tooltip("How long scan results stay visible before disappearing.")]
    [SerializeField] private float scanResultVisibleTime = 1.25f;

    [Tooltip("How long before scanner can be used again.")]
    [SerializeField] private float scanCooldown = 2f;

    [Header("Scan Visual")]
    [Tooltip("How long it takes the scanner shader to play from start to finish once.")]
    [SerializeField] private float scanSweepDuration = 1f;

    [Tooltip("Renderer that owns the scanner 3d material.")]
    [SerializeField] private Renderer scannerVisualRenderer;

    [Tooltip("Material float property name for the scan progress.")]
    [SerializeField] private string scanProgressProperty = "_ScanProgress";

    private bool isAnalysing;
    private bool showingResults;
    private float analysisTimer;
    private float resultsTimer;
    private float cooldownTimer;
    private float scanVisualTimer;

    private CC_CharacterPlayerController cachedPlayer;
    private Camera cachedCamera;
    private Material scannerVisualMaterial;

    private readonly List<UI_ScannerOverlay.ScannerTrackedData> trackedItems = new List<UI_ScannerOverlay.ScannerTrackedData>();

    public override void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public override void SendSender(NetworkObjectReference netObj)
    {
        senderRef = netObj;
    }

    private void Awake()
    {
        CacheScannerVisualMaterial();
        SetScanShaderProgress(0f);
    }

    private void Update()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = ResolvePlayer(senderRef);
        }

        if (cachedPlayer == null) { return; }
        if (!cachedPlayer.IsOwner) { return; }

        if (cachedCamera == null)
        {
            cachedCamera = ResolveCamera(cachedPlayer);
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer < 0f) { cooldownTimer = 0f; }
        }

        if (isAnalysing)
        {
            analysisTimer -= Time.deltaTime;
            TickScannerVisual();

            UpdateWorldText();

            if (analysisTimer <= 0f)
            {
                FinishScan();
            }

            return;
        }

        if (showingResults)
        {
            resultsTimer -= Time.deltaTime;

            if (scannerOverlay != null && cachedCamera != null)
            {
                // keep labels tracking the screen while results are visible
                scannerOverlay.SetTrackedItems(cachedCamera, trackedItems);
            }

            UpdateWorldText();

            if (resultsTimer <= 0f)
            {
                EndScanDisplay();
            }

            return;
        }

        UpdateWorldText();
    }

    public override void OnUseLocally(NetworkObjectReference netObjRef)
    {
        CC_CharacterPlayerController player = ResolvePlayer(netObjRef);
        if (player == null) { return; }

        cachedPlayer = player;

        if (scannerOverlay == null)
        {
            scannerOverlay = player.ScannerOverlay;
        }

        if (scannerOverlay == null) { return; }

        if (cooldownTimer > 0f) { return; }
        if (isAnalysing) { return; }
        if (showingResults) { return; }

        cachedCamera = ResolveCamera(player);
        if (cachedCamera == null) { return; }

        trackedItems.Clear();

        isAnalysing = true;
        showingResults = false;
        analysisTimer = scanAnalysisDelay;
        resultsTimer = 0f;
        cooldownTimer = scanCooldown;
        scanVisualTimer = 0f;

        SetScanShaderProgress(0f);
        scannerOverlay.SetScannerActive(true);

        UpdateWorldText();
    }

    public override void OnEquippedLocally(NetworkObjectReference netObjRef)
    {
        StopScannerImmediate();
    }

    public override void OnUnequippedLocally(NetworkObjectReference netObjRef)
    {
        StopScannerImmediate();
    }

    private void FinishScan()
    {
        isAnalysing = false;
        showingResults = true;
        resultsTimer = scanResultVisibleTime;

        SetScanShaderProgress(1f);

        if (cachedCamera == null && cachedPlayer != null)
        {
            cachedCamera = ResolveCamera(cachedPlayer);
        }

        if (cachedCamera != null)
        {
            RefreshTrackedItems(cachedCamera);
        }

        if (scannerOverlay != null && cachedCamera != null)
        {
            scannerOverlay.SetTrackedItems(cachedCamera, trackedItems);
        }

        UpdateWorldText();
    }

    private void EndScanDisplay()
    {
        isAnalysing = false;
        showingResults = false;

        analysisTimer = 0f;
        resultsTimer = 0f;
        scanVisualTimer = 0f;

        trackedItems.Clear();

        SetScanShaderProgress(0f);

        if (scannerOverlay != null)
        {
            scannerOverlay.SetScannerActive(false);
        }

        UpdateWorldText();
    }

    private void StopScannerImmediate()
    {
        isAnalysing = false;
        showingResults = false;

        analysisTimer = 0f;
        resultsTimer = 0f;
        cooldownTimer = 0f;
        scanVisualTimer = 0f;

        trackedItems.Clear();

        SetScanShaderProgress(0f);

        if (scannerOverlay != null)
        {
            scannerOverlay.SetScannerActive(false);
        }

        cachedCamera = null;
        cachedPlayer = null;
    }

    private void RefreshTrackedItems(Camera scanCamera)
    {
        trackedItems.Clear();

        INV_ItemDrop[] allDrops = FindObjectsByType<INV_ItemDrop>(FindObjectsSortMode.None);
        if (allDrops == null || allDrops.Length == 0) { return; }

        Vector3 camPos = scanCamera.transform.position;
        Vector3 camForward = scanCamera.transform.forward;

        for (int i = 0; i < allDrops.Length; i++)
        {
            INV_ItemDrop drop = allDrops[i];
            if (drop == null) { continue; }
            if (!drop.isActiveAndEnabled) { continue; }
            if (!drop.IsSpawned) { continue; }

            Vector3 worldPos = drop.transform.position;
            Vector3 toDrop = worldPos - camPos;
            float distance = toDrop.magnitude;

            if (distance > scanRange) { continue; }
            if (distance <= 0.001f) { continue; }

            Vector3 direction = toDrop / distance;
            float angle = Vector3.Angle(camForward, direction);

            // camera perspective cone
            if (angle > (scanConeAngle * 0.5f)) { continue; }

            // in front of camera
            Vector3 viewport = scanCamera.WorldToViewportPoint(worldPos);
            if (viewport.z <= 0f) { continue; }

            if (requireLineOfSight)
            {
                if (Physics.Raycast(camPos, direction, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    INV_ItemDrop hitDrop = hit.collider != null ? hit.collider.GetComponentInParent<INV_ItemDrop>() : null;
                    if (hitDrop != drop) { continue; }
                }
            }

            trackedItems.Add(new UI_ScannerOverlay.ScannerTrackedData
            {
                netObjRef = drop.NetworkObject,
                itemDrop = drop,
                worldPosition = worldPos,
                distance = distance
            });
        }
    }

    private void TickScannerVisual()
    {
        if (scanSweepDuration <= 0f)
        {
            SetScanShaderProgress(1f);
            return;
        }

        scanVisualTimer += Time.deltaTime;

        float t = scanVisualTimer / scanSweepDuration;
        if (t > 1f) { t = 1f; }

        SetScanShaderProgress(t);
    }

    private void CacheScannerVisualMaterial()
    {
        if (scannerVisualRenderer == null) { return; }

        scannerVisualMaterial = scannerVisualRenderer.material;
    }

    private void SetScanShaderProgress(float value)
    {
        if (scannerVisualMaterial == null)
        {
            CacheScannerVisualMaterial();
        }

        if (scannerVisualMaterial == null) { return; }
        if (string.IsNullOrWhiteSpace(scanProgressProperty)) { return; }

        scannerVisualMaterial.SetFloat(scanProgressProperty, value);
    }

    private void UpdateWorldText()
    {
        if (scannerWorldText == null) { return; }

        if (isAnalysing)
        {
            scannerWorldText.SetText("Scanning...");
            return;
        }

        if (showingResults)
        {
            if (trackedItems.Count <= 0)
            {
                scannerWorldText.SetText("No Items Found");
            }
            else
            {
                scannerWorldText.SetText($"{trackedItems.Count} Items Found");
            }

            return;
        }

        if (cooldownTimer > 0f)
        {
            scannerWorldText.SetText($"Cooldown {cooldownTimer:0.0}s");
            return;
        }

        scannerWorldText.SetText("Ready");
    }

    private CC_CharacterPlayerController ResolvePlayer(NetworkObjectReference netObjRef)
    {
        if (!netObjRef.TryGet(out NetworkObject netObj) || netObj == null) { return null; }

        CC_CharacterPlayerController player = netObj.GetComponent<CC_CharacterPlayerController>();
        if (player == null)
        {
            player = netObj.GetComponentInChildren<CC_CharacterPlayerController>();
        }

        if (player == null) { return null; }
        if (!player.IsOwner) { return null; }

        return player;
    }

    private Camera ResolveCamera(CC_CharacterPlayerController player)
    {
        if (player == null) { return null; }

        Camera cam = player.GetComponentInChildren<Camera>(true);
        if (cam != null) { return cam; }

        return Camera.main;
    }
}