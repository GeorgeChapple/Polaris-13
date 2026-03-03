using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

// Made By: Jason Lodge
// Summary: Simple interactable object, can be wired up in editor using Unity Events.
//          Can also mirror current onInteract listeners into an object-based runtime event,
//          allowing the interactor to be passed through on network spawn.
public class InteractableObject : NetworkBehaviour
{
    [Header("Interact")]
    public UnityEvent onInteract;

    [Tooltip("Runtime event that passes the interactor through as an Object.")]
    public ObjectEvent onInteractWithInteractor;

    [Tooltip("If true, current onInteract listeners will be mirrored into onInteractWithInteractor on network spawn.")]
    public bool rewireInteractListenersOnNetworkSpawn = true;

    [Tooltip("If true, the original onInteract event will still be invoked as well. Disable this once fully migrated to avoid double-calls.")]
    public bool invokeLegacyInteractEvent = true;

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

    [System.Serializable]
    public class ObjectEvent : UnityEvent<Object> { }

    [Tooltip("Progress from 0-1 while holding.")]
    public FloatEvent onHoldProgress;

    bool used;

    // runtime bindings created from the current onInteract persistent listeners
    readonly List<RuntimeObjectBinding> runtimeInteractBindings = new List<RuntimeObjectBinding>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (rewireInteractListenersOnNetworkSpawn)
        {
            RewireInteractListeners();
        }
    }

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

        if (invokeLegacyInteractEvent)
        {
            onInteract?.Invoke();
        }

        onInteractWithInteractor?.Invoke(interactor);
    }

    public void RewireInteractListeners()
    {
        onInteractWithInteractor.RemoveAllListeners();
        runtimeInteractBindings.Clear();

        if (onInteract == null) { return; }

        int eventCount = onInteract.GetPersistentEventCount();

        for (int i = 0; i < eventCount; i++)
        {
            Object target = onInteract.GetPersistentTarget(i);
            string methodName = onInteract.GetPersistentMethodName(i);

            if (target == null) { continue; }
            if (string.IsNullOrEmpty(methodName)) { continue; }

            MethodInfo method = FindObjectCompatibleMethod(target, methodName);

            if (method == null)
            {
                Debug.LogWarning($"{name}: Could not rewire '{methodName}' on '{target.name}'. Expected a method with the same name that takes one Object/GameObject compatible parameter.", this);
                continue;
            }

            RuntimeObjectBinding binding = new RuntimeObjectBinding(target, method);
            runtimeInteractBindings.Add(binding);
            onInteractWithInteractor.AddListener(binding.Invoke);
        }
    }

    MethodInfo FindObjectCompatibleMethod(Object target, string methodName)
    {
        System.Type targetType = target.GetType();

        MethodInfo[] methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];

            if (method.Name != methodName) { continue; }

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != 1) { continue; }

            System.Type parameterType = parameters[0].ParameterType;

            // supports object, gameObject, or any base type gameObject can be passed to
            if (parameterType.IsAssignableFrom(typeof(GameObject)))
            {
                return method;
            }
        }

        return null;
    }

    class RuntimeObjectBinding
    {
        readonly Object target;
        readonly MethodInfo method;
        readonly object[] args = new object[1];

        public RuntimeObjectBinding(Object target, MethodInfo method)
        {
            this.target = target;
            this.method = method;
        }

        public void Invoke(Object interactor)
        {
            if (target == null) { return; }
            if (method == null) { return; }

            args[0] = interactor;
            method.Invoke(target, args);
        }
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