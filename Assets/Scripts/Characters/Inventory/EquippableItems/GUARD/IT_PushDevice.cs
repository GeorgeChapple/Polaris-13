using Unity.Netcode;
using UnityEngine;

public class IT_PushDevice : CC_INV_UsableItems
{
    private string itemId;

    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Vector3 pushVolumeBounds = new Vector3(5f, 5f, 10f);
    [SerializeField] private float pushVolumeForwardOffset = 0f;
    [SerializeField] private float maxPushForce = 10f;
    [SerializeField] private float torqueForce = 1f;
    [SerializeField] private float chargeTime = 2f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private bool ignoreSelf = true;
    private float pushForce = 0f;
    private float chargingTimer = 0f;
    private float cooldownTimer = 0f;

    private NetworkObject playerWhoSent;

    
    public override void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public override void SendSender(NetworkObjectReference netObj)
    {
        playerWhoSent = netObj;
    }

    public override void OnUse(NetworkObjectReference netObjRef) { }

    public override void OnUseHeld(NetworkObjectReference netObjRef)
    {
        if (muzzlePoint == null)
        {
            return;
        }

        if (cooldownTimer <= 0f)
        {
            chargingTimer += Time.deltaTime;
            chargingTimer = Mathf.Clamp(chargingTimer, 0f, Mathf.Max(0.001f, chargeTime));

            float safeChargeTime = Mathf.Max(0.001f, chargeTime);
            pushForce = Mathf.Lerp(0f, maxPushForce, Mathf.Clamp(chargingTimer, 0f, safeChargeTime) / safeChargeTime);
        }
    }

    public override void OnUseReleased(NetworkObjectReference netObjRef) {
        cooldownTimer = cooldown;
        Collider[] colliders = Physics.OverlapBox(muzzlePoint.transform.position + (pushVolumeForwardOffset + pushVolumeBounds.z / 2) * muzzlePoint.forward, pushVolumeBounds / 2, Quaternion.LookRotation(muzzlePoint.forward));
        foreach (Collider collider in colliders)
        {
            if (collider.GetComponent<CC_Movement>())
            {
                PushPlayerRpc(collider, playerWhoSent); 
            }
            else
            {
                PushObject(collider);
            }
        }
        pushForce = 0;
        chargingTimer = 0;
    }
    public override void OnAltUse(NetworkObjectReference netObjRef) { }
    public override void OnAltUseHeld(NetworkObjectReference netObjRef) { }
    public override void OnAltUseReleased(NetworkObjectReference netObjRef) { }

    private void PushObject(Collider col)
    {
        Rigidbody rb = col.GetComponent<Rigidbody>();
        if (rb != null)
        { 
            rb.AddForce(muzzlePoint.forward * pushForce);
            rb.AddTorque(Vector3.one * Random.Range(-torqueForce, torqueForce));
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PushPlayerRpc(Collider col, NetworkObjectReference us)
    {
        if (us.TryGet(out NetworkObject usNetObj) && (col.GetComponent<CC_Movement>() != usNetObj.GetComponent<CC_Movement>() || !ignoreSelf))
        { 
            CC_Movement player = col.GetComponent<CC_Movement>();
            if (player != null)
            {
                player.PushSelf(muzzlePoint.forward * pushForce);
            }
        }
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;
    }

    private void OnDrawGizmos()
    {
        CustomGizmos.DrawBox(muzzlePoint.transform.position + (pushVolumeForwardOffset + pushVolumeBounds.z / 2) * muzzlePoint.forward, Quaternion.LookRotation(muzzlePoint.forward), pushVolumeBounds, Color.cyan);
    }
}
