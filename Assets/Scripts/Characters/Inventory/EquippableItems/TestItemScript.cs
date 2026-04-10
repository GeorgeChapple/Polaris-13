using Unity.Netcode;
using UnityEngine;

public class TestItemScript : MonoBehaviour, IUsableItem
{
    private string itemId;

    [SerializeField] private GameObject TestBullet;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float bulletSpeed;

    public void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public void OnUse(NetworkObjectReference netObjRef)
    {
        Debug.Log($"Player Network Object {netObjRef.NetworkObjectId}: Test Object Fired");

        GameObject testbulletFire = Instantiate(TestBullet, muzzlePoint.position, Quaternion.identity);
        NetworkObject netObj = testbulletFire.GetComponent<NetworkObject>();
        netObj.Spawn();

        Rigidbody rb = testbulletFire.GetComponent<Rigidbody>();
        rb.WakeUp();
        rb.linearVelocity = muzzlePoint.forward * bulletSpeed;
    }
}