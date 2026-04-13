using Unity.Netcode;
using UnityEngine;

public class CC_INV_ConsumableItem : CC_INV_UsableItems
{
    private string itemId;

    public override void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public override void OnUse(NetworkObjectReference netObjRef)
    {
        if (netObjRef.TryGet(out NetworkObject netObj))
        {
            Debug.Log($"{netObj.NetworkObjectId} used consumable with id {itemId}.");
        }
    }
}