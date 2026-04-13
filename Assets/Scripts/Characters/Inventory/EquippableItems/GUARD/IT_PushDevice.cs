using Unity.Netcode;
using UnityEngine;

public class IT_PushDevice : CC_INV_UsableItems
{
    private string itemId;

    [SerializeField] private Transform muzzlePoint;
    public Vector3 pushVolumeBounds = new Vector3(5f, 5f, 10f);
    public float maxPushForce = 10f;
    public float torqueForce = 1f;
    public float chargeTime = 2f;
    public float cooldown = 0.5f;
    public float pushForce = 0f;
    public float chargingTimer = 0f;
    public float cooldownTimer = 0f;

    public override void SendItemId(string ItemId)
    {
        itemId = ItemId;
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
        Collider[] colliders = Physics.OverlapBox(muzzlePoint.transform.position + (pushVolumeBounds.z / 2) * muzzlePoint.forward, pushVolumeBounds / 2, Quaternion.LookRotation(muzzlePoint.forward));
        foreach (Collider collider in colliders)
        {
            if (collider.GetComponent<CC_Movement>())
            {
                if (collider.transform.root != transform.root)
                {
                    PushPlayerRpc(netObjRef); 
                }
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
    private void PushPlayerRpc(NetworkObjectReference netObjRef)
    {
        if (netObjRef.TryGet(out NetworkObject netObj))
        {
            CC_Movement player = netObj.GetComponent<CC_Movement>();
            if (player != null)
            {
                player.Body.AddForce(muzzlePoint.forward * pushForce);
            }
        }
    }

    private void Start()
    {
        if (!transform.root.GetComponent<CC_CharacterPlayerController>().enabled)
        {
            //this.enabled = false;
        }
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;
    }

    private void OnDrawGizmos()
    {
        CustomGizmos.DrawBox(muzzlePoint.transform.position + (pushVolumeBounds.z / 2) * muzzlePoint.forward, Quaternion.LookRotation(muzzlePoint.forward), pushVolumeBounds, Color.cyan);
    }
}
