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
            Debug.Log($"{netObj.NetworkObjectId} used consumable with id {itemId}.");
        }
    }

    public void OnUseHeld(NetworkObjectReference netObjRef) { }
    public void OnUseReleased(NetworkObjectReference netObjRef) { }
    public void OnAltUse(NetworkObjectReference netObjRef) { }
    public void OnAltUseHeld(NetworkObjectReference netObjRef) { }
    public void OnAltUseReleased(NetworkObjectReference netObjRef) { }
}