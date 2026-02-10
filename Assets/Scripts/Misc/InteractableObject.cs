using UnityEngine;
using UnityEngine.Events;

// Made By: Jason Lodge
// Summary: Simple interactable object, can be wired up in editor using Unity Events.
public class InteractableObject : MonoBehaviour
{
    [Header("Interact")]
    public UnityEvent onInteract;

    [Tooltip("Optional, If true, object can only be interacted with once.")]
    public bool oneShot;

    [Header("Hold Interaction")]
    [Tooltip("If true, this interactable requires holding the interact key.")]
    public bool requiresHold = false;

    [Tooltip("How long to hold for interact.")]
    public float holdTime = 1f;

    [Tooltip("Fired when the player starts holding interact on this object.")]
    public UnityEvent onStartHold;

    [Tooltip("Fired when holding is cancelled (look away / release / out of range).")]
    public UnityEvent onCancelHold;

    [System.Serializable]
    public class FloatEvent : UnityEvent<float> { }

    [Tooltip("Progress from 0-1 while holding.")]
    public FloatEvent onHoldProgress;

    bool used;

    public bool CanInteract()
    {
        return !(oneShot && used);
    }

    public float GetHoldTime()
    {
        return Mathf.Max(0f, holdTime);
    }

    // called by the character when hold begins
    public void BeginHold(GameObject interactor)
    {
        if (!CanInteract()) { return; }
        onStartHold?.Invoke();
    }

    // called by the character during hold (progress 0-1), for ui or other
    public void HoldProgress(GameObject interactor, float progress01)
    {
        if (!CanInteract()) { return; }
        onHoldProgress?.Invoke(Mathf.Clamp01(progress01));
    }

    // called by the character when hold is cancelled
    public void CancelHold(GameObject interactor)
    {
        if (!CanInteract()) { return; }
        onCancelHold?.Invoke();
    }

    // called by the character when interaction completes (tap or hold finished)
    public void Interact(GameObject interactor)
    {
        if (!CanInteract()) { return; }

        used = true;
        onInteract?.Invoke();
    }

    public void ResetOneShot()
    {
        used = false;
    }
    public void Test()
    {
        Debug.Log($"{name} has been interacted with. Calling Event {onInteract.GetPersistentMethodName(0)}");
    }
}
