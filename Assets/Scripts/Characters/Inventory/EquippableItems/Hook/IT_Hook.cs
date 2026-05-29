using Unity.Netcode;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Equippable hook tool behaviour.
public class IT_Hook : CC_INV_UsableItems
{
    private string itemId;

    [Header("Refs")]
    [SerializeField] private HookLineRenderer hook;
    [SerializeField] private Animator hookAnimator;

    [Header("Charge")]
    [SerializeField] private float minThrowPower = 8f;
    [SerializeField] private float maxThrowPower = 30f;
    [SerializeField] private float chargeRate = 18f;
    [SerializeField] private float maxHookDistance = 18f;

    // server authoritative charge values
    private float currentChargePower;
    private bool isCharging;

    // local visual only
    private bool isChargingLocally;

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

    public override void OnUseLocally(NetworkObjectReference netObjRef)
    {
        if (hook == null) { return; }

        // dont start local charge visual while a hook is already active
        if (hook.HasActiveHook(netObjRef)) { return; }

        isChargingLocally = true;

        if (hookAnimator != null) { hookAnimator.SetBool("IsCharging", isChargingLocally); }
        // if we had the character animations, we'd set them on network from here for third person anims being updated for all viewers.
    }

    public override void OnUseHeld(NetworkObjectReference netObjRef)
    {
        if (!isCharging) { return; }

        currentChargePower += chargeRate * Time.deltaTime;
        currentChargePower = Mathf.Clamp(currentChargePower, minThrowPower, maxThrowPower);
    }

    public override void OnUseHeldLocally(NetworkObjectReference netObjRef)
    {
        if (!isChargingLocally) { return; }

    }

    public override void OnUseReleased(NetworkObjectReference netObjRef)
    {
        if (!isCharging) { return; }

        StopCharging();

        if (hook == null)
        {
            currentChargePower = 0f;
            return;
        }

        hook.ShootHook(netObjRef, currentChargePower, maxHookDistance);
        currentChargePower = 0f;
    }

    public override void OnUseReleasedLocally(NetworkObjectReference netObjRef)
    {
        StopChargingLocally();
    }

    public override void OnAltUse(NetworkObjectReference netObjRef)
    {
        if (hook == null) { return; }

        StopCharging();
        hook.ReelHook(netObjRef);
    }

    public override void OnAltUseLocally(NetworkObjectReference netObjRef)
    {
        StopChargingLocally();
    }

    public override void OnUnequipped(NetworkObjectReference netObjRef)
    {
        StopCharging();
        currentChargePower = 0f;
    }

    public override void OnUnequippedLocally(NetworkObjectReference netObjRef)
    {
        StopChargingLocally();
    }

    private void StopCharging()
    {
        isCharging = false;
    }

    private void StopChargingLocally()
    {
        isChargingLocally = false;

        if (hookAnimator != null) { hookAnimator.SetBool("IsCharging", isChargingLocally); }
    }
}