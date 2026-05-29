using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Handles overlay scanner ui for scanned world items.
public class UI_ScannerOverlay : MonoBehaviour
{
    [System.Serializable]
    public class ScannerTrackedData
    {
        public NetworkObjectReference netObjRef;
        public INV_ItemDrop itemDrop;
        public Vector3 worldPosition;
        public float distance;
    }

    [Header("Refs")]
    [SerializeField] private RectTransform markerParent;
    [SerializeField] private UI_ScannerTrackedItem markerPrefab;

    [Header("Settings")]
    [SerializeField] private Vector3 markerWorldOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private float edgePadding = 32f;

    private readonly Dictionary<ulong, UI_ScannerTrackedItem> activeMarkers = new Dictionary<ulong, UI_ScannerTrackedItem>();

    private bool scannerActive;

    public void SetScannerActive(bool state)
    {
        scannerActive = state;

        if (!scannerActive)
        {
            ClearMarkers();
        }
    }

    public void SetTrackedItems(Camera cam, List<ScannerTrackedData> trackedItems)
    {
        if (!scannerActive)
        {
            ClearMarkers();
            return;
        }

        if (cam == null)
        {
            ClearMarkers();
            return;
        }

        HashSet<ulong> usedIds = new HashSet<ulong>();

        for (int i = 0; i < trackedItems.Count; i++)
        {
            ScannerTrackedData data = trackedItems[i];
            if (data == null) { continue; }
            if (data.itemDrop == null) { continue; }

            if (!data.netObjRef.TryGet(out NetworkObject netObj) || netObj == null) { continue; }

            ulong netId = netObj.NetworkObjectId;
            usedIds.Add(netId);

            UI_ScannerTrackedItem marker = GetOrCreateMarker(netId);
            if (marker == null) { continue; }

            Vector3 worldPos = data.worldPosition + markerWorldOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // behind camera
            if (screenPos.z <= 0f)
            {
                marker.gameObject.SetActive(false);
                continue;
            }

            screenPos.x = Mathf.Clamp(screenPos.x, edgePadding, Screen.width - edgePadding);
            screenPos.y = Mathf.Clamp(screenPos.y, edgePadding, Screen.height - edgePadding);

            marker.gameObject.SetActive(true);
            marker.SetScreenPosition(screenPos);

            string itemName = "Unknown";
            if (data.itemDrop.Item != null && !string.IsNullOrWhiteSpace(data.itemDrop.Item.Name))
            {
                itemName = data.itemDrop.Item.Name;
            }

            marker.SetData(itemName, data.distance);
        }

        RemoveUnusedMarkers(usedIds);
    }

    // reuse the current marker or spawn one if needed.
    private UI_ScannerTrackedItem GetOrCreateMarker(ulong netId)
    {
        if (activeMarkers.TryGetValue(netId, out UI_ScannerTrackedItem existing) && existing != null)
        {
            return existing;
        }

        if (markerPrefab == null || markerParent == null) { return null; }

        UI_ScannerTrackedItem newMarker = Instantiate(markerPrefab, markerParent);
        activeMarkers[netId] = newMarker;
        return newMarker;
    }

    private void RemoveUnusedMarkers(HashSet<ulong> usedIds)
    {
        List<ulong> removeIds = null;

        foreach (var kvp in activeMarkers)
        {
            if (usedIds.Contains(kvp.Key)) { continue; }

            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }

            if (removeIds == null) { removeIds = new List<ulong>(); }
            removeIds.Add(kvp.Key);
        }

        if (removeIds == null) { return; }

        for (int i = 0; i < removeIds.Count; i++)
        {
            activeMarkers.Remove(removeIds[i]);
        }
    }

    public void ClearMarkers()
    {
        foreach (var kvp in activeMarkers)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }

        activeMarkers.Clear();
    }
}