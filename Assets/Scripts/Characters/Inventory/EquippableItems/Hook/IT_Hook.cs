using Unity.Netcode;
using UnityEngine;

public class IT_Hook : MonoBehaviour, IUsableItem
{
    private string itemId;

    [SerializeField] private HookLineRenderer hook;
    [SerializeField] private Transform muzzlePoint;
    public float shootSpeed = 5;
    public int segmentCount = 20;
    public float length = 10f;

    public void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public void OnUse(NetworkObjectReference netObjRef)
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

    public void OnUseHeld(NetworkObjectReference netObjRef) { }
    public void OnUseReleased(NetworkObjectReference netObjRef) { }
    public void OnAltUse(NetworkObjectReference netObjRef) { }
    public void OnAltUseHeld(NetworkObjectReference netObjRef) { }
    public void OnAltUseReleased(NetworkObjectReference netObjRef) { }
}