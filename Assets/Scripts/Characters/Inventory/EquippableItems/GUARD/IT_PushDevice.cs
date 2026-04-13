using Unity.Netcode;
using UnityEngine;

public class IT_PushDevice : CC_INV_UsableItems
{
    private string itemId;

    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private PushValues pushValues;

    public override void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public override void OnUse(NetworkObjectReference netObjRef) { }

    public override void OnUseHeld(NetworkObjectReference netObjRef) {
        Debug.Log("HOLDING GUARD");
        if (pushValues.cooldownTimer < 0f)
        {
            Debug.Log("HOLDING GUARD2");
            if (pushValues.chargingTimer < pushValues.chargeTime)
            {
                pushValues.chargingTimer += Time.deltaTime;
            }
            pushValues.pushForce = Mathf.Lerp(0, pushValues.maxPushForce, pushValues.chargingTimer / pushValues.chargeTime);
        }
    }
    public override void OnUseReleased(NetworkObjectReference netObjRef) {
        pushValues.cooldownTimer = pushValues.cooldown;
        Collider[] colliders = Physics.OverlapBox(muzzlePoint.transform.position + (pushValues.pushVolumeBounds.z / 2) * muzzlePoint.forward, pushValues.pushVolumeBounds / 2, Quaternion.LookRotation(muzzlePoint.forward));
        foreach (Collider collider in colliders)
        {
            if (collider.GetComponent<CC_Movement>())
            {
                if (collider.GetComponent<CC_Movement>().Body.isKinematic)
                {
                    PushPlayerRpc(collider); 
                }
            }
            else
            {
                PushObjectRpc(collider);
            }
        }
        pushValues.pushForce= 0;
        pushValues.chargingTimer = 0;
    }
    public override void OnAltUse(NetworkObjectReference netObjRef) { }
    public override void OnAltUseHeld(NetworkObjectReference netObjRef) { }
    public override void OnAltUseReleased(NetworkObjectReference netObjRef) { }

    [Rpc(SendTo.Server)]
    private void PushObjectRpc(Collider col)
    {
        Rigidbody rb = col.GetComponent<Rigidbody>();
        if (rb != null)
        { 
            rb.AddForce(muzzlePoint.forward * pushValues.pushForce);
            rb.AddTorque(Vector3.one * Random.Range(-pushValues.torqueForce, pushValues.torqueForce));
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PushPlayerRpc(Collider col)
    {
        Rigidbody rb = col.GetComponent<Rigidbody>();
        if (rb != null)
        { 
            rb.AddForce(muzzlePoint.forward * pushValues.pushForce);
            rb.AddTorque(Vector3.one * Random.Range(-pushValues.torqueForce, pushValues.torqueForce));
        }
    }

    private void Update()
    {
        pushValues.cooldownTimer -= Time.deltaTime;
    }

    private void OnDrawGizmos()
    {
        CustomGizmos.DrawBox(muzzlePoint.transform.position + (pushValues.pushVolumeBounds.z / 2) * muzzlePoint.forward, Quaternion.LookRotation(muzzlePoint.forward), pushValues.pushVolumeBounds, Color.cyan);
    }
}
