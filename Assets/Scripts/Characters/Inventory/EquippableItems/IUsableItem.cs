using Unity.Netcode;
using UnityEngine;

public interface IUsableItem
{
    void OnUse(NetworkObjectReference netObjRef);
    void SendItemId(string itemId);
}
