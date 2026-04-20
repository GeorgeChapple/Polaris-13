using Unity.Netcode;
using UnityEngine;

public abstract class CC_INV_UsableItems : MonoBehaviour
{
    public virtual void SendItemId(string itemId) { }

    public virtual void SendSender(NetworkObjectReference netObj) { }

    public virtual void OnUse(NetworkObjectReference netObjRef) { }
    public virtual void OnUseHeld(NetworkObjectReference netObjRef) { }
    public virtual void OnUseReleased(NetworkObjectReference netObjRef) { }

    public virtual void OnAltUse(NetworkObjectReference netObjRef) { }
    public virtual void OnAltUseHeld(NetworkObjectReference netObjRef) { }
    public virtual void OnAltUseReleased(NetworkObjectReference netObjRef) { }

    public virtual void OnUseLocally(NetworkObjectReference netObjRef) { }
    public virtual void OnUseHeldLocally(NetworkObjectReference netObjRef) { }
    public virtual void OnUseReleasedLocally(NetworkObjectReference netObjRef) { }

    public virtual void OnAltUseLocally(NetworkObjectReference netObjRef) { }
    public virtual void OnAltUseHeldLocally(NetworkObjectReference netObjRef) { }
    public virtual void OnAltUseReleasedLocally(NetworkObjectReference netObjRef) { }

    public virtual void OnEquipped(NetworkObjectReference netObjRef) { }
    public virtual void OnUnequipped(NetworkObjectReference netObjRef) { }

    public virtual void OnEquippedLocally(NetworkObjectReference netObjRef) { }
    public virtual void OnUnequippedLocally(NetworkObjectReference netObjRef) { }
}