using Unity.Netcode;
using UnityEngine;

public class TestItemScript : MonoBehaviour, IUsableItem
{
    private GameObject ownerPlayer;
    private string itemId;

    [SerializeField] private GameObject TestBullet;
    [SerializeField] private Transform muzzlePoint;

    public void WireUp(GameObject player, string ItemId)
    {
        ownerPlayer = player;
        itemId = ItemId;
    }

    public void OnUse()
    {
        Debug.Log($"{ownerPlayer.name}: Test Object Fired");

        GameObject testbulletFire = Instantiate(TestBullet, muzzlePoint.position, Quaternion.identity);
        NetworkObject netObj = testbulletFire.GetComponent<NetworkObject>();
        netObj.Spawn();

        netObj.GetComponent<Rigidbody>().AddForce(muzzlePoint.forward);
    }
    public void OnUseWithUser(NetworkObjectReference netObjRef)
    {

    }
}