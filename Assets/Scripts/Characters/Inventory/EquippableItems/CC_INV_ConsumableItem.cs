using Unity.Netcode;
using UnityEngine;

public class CC_INV_ConsumableItem : MonoBehaviour, IUsableItem
{
    private string itemId;

    public void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }
    public void OnUse(NetworkObjectReference netObjRef)
    {        
        if (netObjRef.TryGet(out NetworkObject netObj))
        {
            INV_PlayerInventoryNet playerInventoryNet = netObj.GetComponent<INV_PlayerInventoryNet>();
            if (playerInventoryNet != null)
            {
                playerInventoryNet.RequestUseItemInInventory(itemId);
                Debug.Log($"{netObj.NetworkObjectId} ate item with id {itemId}.");
            }
        }
    }
}
