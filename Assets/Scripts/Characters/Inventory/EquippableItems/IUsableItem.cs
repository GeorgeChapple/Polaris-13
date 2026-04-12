using Unity.Netcode;
using UnityEngine;

public interface IUsableItem
{
    void OnUse(NetworkObjectReference netObjRef);
    void OnUseHeld(NetworkObjectReference netObjRef);
    void OnUseReleased(NetworkObjectReference netObjRef);

    void OnAltUse(NetworkObjectReference netObjRef);
    void OnAltUseHeld(NetworkObjectReference netObjRef);
    void OnAltUseReleased(NetworkObjectReference netObjRef);

    void SendItemId(string itemId);
}