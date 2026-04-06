using Unity.Netcode;
using UnityEngine;

public interface IUsableItem
{
    void OnUse();
    void OnUseWithUser(NetworkObjectReference netObjRef);
    void WireUp(GameObject player, string itemId);
}
