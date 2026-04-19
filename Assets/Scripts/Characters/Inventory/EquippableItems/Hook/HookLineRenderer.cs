using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HookLineRenderer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HookHead hookHeadPrefab;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private GameObject hookVisual;

    [Header("Visuals")]
    [SerializeField] private bool hideLineWhenNoHook = true;

    private LineRenderer line;
    private NetworkObject ownerNetworkObject;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        ownerNetworkObject = GetComponentInParent<NetworkObject>();

        if (line != null) { line.positionCount = 2; }

        SetHookVisual(true);
    }

    private void LateUpdate()
    {
        UpdateLine();
    }

    public bool HasActiveHook(NetworkObjectReference netObjRef)
    {
        NetworkObject shooterNetObj = ResolveShooter(netObjRef);
        if (shooterNetObj == null) { return false; }

        return HookHead.FindActiveHookForShooter(shooterNetObj.NetworkObjectId) != null;
    }

    public void ShootHook(NetworkObjectReference netObjRef, float throwPower, float maxDistance)
    {
        if (hookHeadPrefab == null || throwPoint == null) { return; }
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) { return; }

        NetworkObject shooterNetObj = ResolveShooter(netObjRef);
        if (shooterNetObj == null) { return; }

        HookHead existingHook = HookHead.FindActiveHookForShooter(shooterNetObj.NetworkObjectId);
        if (existingHook != null) { return; }

        HookHead newHook = Instantiate(hookHeadPrefab, throwPoint.position, throwPoint.rotation);
        NetworkObject hookNetObj = newHook.GetComponent<NetworkObject>();
        if (hookNetObj == null)
        {
            Destroy(newHook.gameObject);
            return;
        }

        hookNetObj.Spawn(true);
        newHook.Fire(shooterNetObj.NetworkObjectId, throwPoint, throwPower, maxDistance);

        SetHookVisual(false);
    }

    public void ReelHook(NetworkObjectReference netObjRef)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) { return; }

        NetworkObject shooterNetObj = ResolveShooter(netObjRef);
        if (shooterNetObj == null) { return; }

        HookHead existingHook = HookHead.FindActiveHookForShooter(shooterNetObj.NetworkObjectId);
        if (existingHook == null) { return; }

        existingHook.BeginReturn(throwPoint);
    }

    private void UpdateLine()
    {
        if (line == null || throwPoint == null) { return; }

        ulong shooterId = GetShooterNetworkObjectId();
        if (shooterId == 0)
        {
            DrawIdleLine();
            SetHookVisual(true);
            return;
        }

        HookHead activeHook = HookHead.FindActiveHookForShooter(shooterId);
        if (activeHook == null)
        {
            DrawIdleLine();
            SetHookVisual(true);
            return;
        }

        SetHookVisual(false);

        if (!line.enabled) { line.enabled = true; }

        line.SetPosition(0, throwPoint.position);
        line.SetPosition(1, activeHook.transform.position);
    }

    private void DrawIdleLine()
    {
        if (line == null || throwPoint == null) { return; }

        if (hideLineWhenNoHook)
        {
            line.enabled = false;
            return;
        }

        if (!line.enabled) { line.enabled = true; }

        line.SetPosition(0, throwPoint.position);
        line.SetPosition(1, throwPoint.position);
    }

    private void SetHookVisual(bool state)
    {
        if (hookVisual == null) { return; }
        if (hookVisual.activeSelf != state) { hookVisual.SetActive(state); }
    }

    private NetworkObject ResolveShooter(NetworkObjectReference netObjRef)
    {
        if (netObjRef.TryGet(out NetworkObject netObj) && netObj != null) { return netObj; }
        if (ownerNetworkObject != null) { return ownerNetworkObject; }

        ownerNetworkObject = GetComponentInParent<NetworkObject>();
        return ownerNetworkObject;
    }

    private ulong GetShooterNetworkObjectId()
    {
        if (ownerNetworkObject == null) { ownerNetworkObject = GetComponentInParent<NetworkObject>(); }
        if (ownerNetworkObject == null) { return 0; }

        return ownerNetworkObject.NetworkObjectId;
    }
}