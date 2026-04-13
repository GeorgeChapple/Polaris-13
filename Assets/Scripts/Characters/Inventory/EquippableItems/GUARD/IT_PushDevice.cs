using Unity.Netcode;
using UnityEngine;

public class IT_PushDevice : CC_INV_UsableItems
{
    private string itemId;

    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Vector3 pushVolumeBounds = new Vector3(5f, 5f, 10f);
    [SerializeField] private float maxPushForce = 10f;
    [SerializeField] private float torqueForce = 1f;
    [SerializeField] private float chargeTime = 2f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private float pushForce = 0;
    [SerializeField] private float chargingTimer = 0;
    [SerializeField] private float cooldownTimer = 0;

    public override void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public override void OnUse(NetworkObjectReference netObjRef)
    {
        //Debug.Log($"Player Network Object {netObjRef.NetworkObjectId}: Test Object Fired");

        //GameObject testbulletFire = Instantiate(TestBullet, muzzlePoint.position, Quaternion.identity);
        //NetworkObject netObj = testbulletFire.GetComponent<NetworkObject>();
        //netObj.Spawn();

        //Rigidbody rb = testbulletFire.GetComponent<Rigidbody>();
        //rb.WakeUp();
        //rb.linearVelocity = muzzlePoint.forward * bulletSpeed;
    }

    public override void OnUseHeld(NetworkObjectReference netObjRef) {
        Debug.Log("HOLDING GUARD");
        if (cooldownTimer < 0f)
        {
            Debug.Log("HOLDING GUARD2");
            TickForce();
            //pushForce = Mathf.Lerp(0, maxPushForce, Mathf.Clamp(chargingTimer, float.MinValue, chargeTime) / chargeTime);
        }
    }
    public override void OnUseReleased(NetworkObjectReference netObjRef) {
        cooldownTimer = cooldown;
        Collider[] colliders = Physics.OverlapBox(muzzlePoint.transform.position + (pushVolumeBounds.z / 2) * muzzlePoint.forward, pushVolumeBounds / 2, Quaternion.LookRotation(muzzlePoint.forward));
        foreach (Collider collider in colliders)
        {
            if (collider.GetComponent<CC_Movement>())
            {
                PushPlayerRpc(collider);
            }
            else
            {
                PushObjectRpc(collider);
            }
        }
        pushForce = 0;
        chargingTimer = 0;
    }
    public override void OnAltUse(NetworkObjectReference netObjRef) { }
    public override void OnAltUseHeld(NetworkObjectReference netObjRef) { }
    public override void OnAltUseReleased(NetworkObjectReference netObjRef) { }

    private void TickForce()
    { 
        chargingTimer += Time.deltaTime;
    }

    [Rpc(SendTo.Server)]
    private void PushObjectRpc(Collider col)
    {
        Rigidbody rb = col.GetComponent<Rigidbody>();
        if (rb != null)
        { 
            rb.AddForce(muzzlePoint.forward * pushForce);
            rb.AddTorque(Vector3.one * Random.Range(-torqueForce, torqueForce));
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PushPlayerRpc(Collider col)
    {
        Rigidbody rb = col.GetComponent<Rigidbody>();
        if (rb != null)
        { 
            rb.AddForce(muzzlePoint.forward * pushForce);
            rb.AddTorque(Vector3.one * Random.Range(-torqueForce, torqueForce));
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
