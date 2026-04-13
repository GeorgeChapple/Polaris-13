using Unity.Netcode;
using UnityEngine;

public class TestItemScript : CC_INV_UsableItems
{
    private string itemId;

    [SerializeField] private GameObject TestBullet;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float bulletSpeed;

    public override void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public override void OnUse(NetworkObjectReference netObjRef)
    {
        Debug.Log($"Player Network Object {netObjRef.NetworkObjectId}: Test Object Fired");

        GameObject testbulletFire = Instantiate(TestBullet, muzzlePoint.position, Quaternion.identity);
        NetworkObject netObj = testbulletFire.GetComponent<NetworkObject>();
        netObj.Spawn();

        Rigidbody rb = testbulletFire.GetComponent<Rigidbody>();
        rb.WakeUp();
        rb.linearVelocity = muzzlePoint.forward * bulletSpeed;
    }

    public override void OnUseHeld(NetworkObjectReference netObjRef)
    {
        Debug.Log($"Player Network Object {netObjRef.NetworkObjectId}: Test Object Held");
    }

    public override void OnUseReleased(NetworkObjectReference netObjRef)
    {
        Debug.Log($"Player Network Object {netObjRef.NetworkObjectId}: Test Object Released");
    }

    public override void OnAltUse(NetworkObjectReference netObjRef)
    {
        Debug.Log($"Player Network Object {netObjRef.NetworkObjectId}: Test Object Alt Fired");
    }
    public override void OnAltUseHeld(NetworkObjectReference netObjRef)
    {
        Debug.Log($"Player Network Object {netObjRef.NetworkObjectId}: Test Object Alt Held");
    }
    public override void OnAltUseReleased(NetworkObjectReference netObjRef)
    {
        Debug.Log($"Player Network Object {netObjRef.NetworkObjectId}: Test Object Alt Released");
    }
}