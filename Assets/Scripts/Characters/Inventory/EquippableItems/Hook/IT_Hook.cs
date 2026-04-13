using Unity.Netcode;
using UnityEngine;

public class IT_Hook : CC_INV_UsableItems
{
    private string itemId;

    [SerializeField] private HookLineRenderer hook;
    [SerializeField] private Transform muzzlePoint;
    public float shootSpeed = 5;
    public int segmentCount = 20;
    public float length = 10f;

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

        hook.TriggerHook();
    }

    public override void OnUseHeld(NetworkObjectReference netObjRef) { }
    public override void OnUseReleased(NetworkObjectReference netObjRef) { }
    public override void OnAltUse(NetworkObjectReference netObjRef) { }
    public override void OnAltUseHeld(NetworkObjectReference netObjRef) { }
    public override void OnAltUseReleased(NetworkObjectReference netObjRef) { }
}