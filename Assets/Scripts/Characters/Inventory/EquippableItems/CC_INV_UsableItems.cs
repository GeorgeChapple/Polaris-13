using Unity.Netcode;
using UnityEngine;

public abstract class CC_INV_UsableItems : MonoBehaviour
{
    public virtual void SendItemId(string itemId) { }

    public virtual void OnUse(NetworkObjectReference netObjRef) { }
    public virtual void OnUseHeld(NetworkObjectReference netObjRef) { }
    public virtual void OnUseReleased(NetworkObjectReference netObjRef) { }

    public virtual void OnAltUse(NetworkObjectReference netObjRef) { }
    public virtual void OnAltUseHeld(NetworkObjectReference netObjRef) { }
    public virtual void OnAltUseReleased(NetworkObjectReference netObjRef) { }
}
