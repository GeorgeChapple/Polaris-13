using Unity.Netcode;
using UnityEngine;

public class IT_Hook : CC_INV_UsableItems
{
    private string itemId;

    [Header("Refs")]
    [SerializeField] private HookLineRenderer hook;
    [SerializeField] private Transform throwPoint;

    [Header("Charge")]
    [SerializeField] private float minThrowPower = 8f;
    [SerializeField] private float maxThrowPower = 30f;
    [SerializeField] private float chargeRate = 18f;
    [SerializeField] private float maxHookDistance = 18f;

    private float currentChargePower;
    private bool isCharging;

    public override void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public override void OnUse(NetworkObjectReference netObjRef)
    {
        if (hook == null) { return; }

        // dont start charging while a hook is already active
        if (hook.HasActiveHook(netObjRef)) { return; }

        isCharging = true;
        currentChargePower = minThrowPower;
    }

    public override void OnUseHeld(NetworkObjectReference netObjRef)
    {
        if (!isCharging) { return; }

        currentChargePower += chargeRate * Time.deltaTime;
        currentChargePower = Mathf.Clamp(currentChargePower, minThrowPower, maxThrowPower);
    }

    public override void OnUseReleased(NetworkObjectReference netObjRef)
    {
        if (!isCharging) { return; }

        isCharging = false;

        if (hook == null)
        {
            currentChargePower = 0f;
            return;
        }

        hook.ShootHook(netObjRef, currentChargePower, maxHookDistance);
        currentChargePower = 0f;
    }

    public override void OnAltUse(NetworkObjectReference netObjRef)
    {
        if (hook == null) { return; }

        hook.ReelHook(netObjRef);
    }
}