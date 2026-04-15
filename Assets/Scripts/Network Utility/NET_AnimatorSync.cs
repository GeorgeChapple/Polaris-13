using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Network animator sync script.
// Turns out network animator is harder that I thought. So I made this RPC sender.
// Network animator doesnt magically sync animator states or variables,
// so server needs to receive a request to set them in which net animator will sync them to clients.
// Will be adding the other things like bool, float, etc, so we can use it for every animator to sync.

public class NET_AnimatorSync : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private NetworkAnimator networkAnimator;

    public override void OnNetworkSpawn()
    {
        if (networkAnimator == null)
        {
            networkAnimator = GetComponent<NetworkAnimator>();
        }
    }

    public void SetTrigger(string triggerName)
    {
        if (networkAnimator == null) { return; }

        if (IsServer)
        {
            SetTrigger_Server(triggerName);
            return;
        }

        SetTrigger_ServerRpc(triggerName);
    }

    public void ResetTrigger(string triggerName)
    {
        if (networkAnimator == null) { return; }

        if (IsServer)
        {
            ResetTrigger_Server(triggerName);
            return;
        }

        ResetTrigger_ServerRpc(triggerName);
    }

    [Rpc(SendTo.Server)]
    private void SetTrigger_ServerRpc(string triggerName)
    {
        SetTrigger_Server(triggerName);
    }

    [Rpc(SendTo.Server)]
    private void ResetTrigger_ServerRpc(string triggerName)
    {
        ResetTrigger_Server(triggerName);
    }

    private void SetTrigger_Server(string triggerName)
    {
        if (networkAnimator == null) { return; }

        networkAnimator.SetTrigger(triggerName);
    }

    private void ResetTrigger_Server(string triggerName)
    {
        if (networkAnimator == null) { return; }

        networkAnimator.ResetTrigger(triggerName);
    }
}