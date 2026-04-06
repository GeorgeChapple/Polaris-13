using Unity.Netcode;
using UnityEngine;

public class CC_INV_ConsumableItem : MonoBehaviour, IUsableItem
{
    private GameObject ownerPlayer;
    private string itemId;

    public void WireUp(GameObject player, string ItemId)
    {
        ownerPlayer = player;
        itemId = ItemId;
    }

    public void OnUse()
    {
        //use item
        INV_PlayerInventoryNet playerInventoryNet = ownerPlayer.GetComponent<INV_PlayerInventoryNet>();
        if (playerInventoryNet != null)
        {
            playerInventoryNet.RequestUseItemInInventory(itemId);
        }
    }
    public void OnUseWithUser(NetworkObjectReference netObjRef)
    {
        INV_PlayerInventoryNet playerInventoryNet = ownerPlayer.GetComponent<INV_PlayerInventoryNet>();
        if (netObjRef.TryGet(out NetworkObject netObj))
        {
            Debug.Log($"{netObj.NetworkObjectId} ate item with id {itemId}.");
        }
    }
}
