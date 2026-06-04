using Unity.Netcode;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Quest trigger.
// Lives on trigger collider and completes the current quest step when any player enters.
public class TUT_TutorialTrigger : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private TUT_TutorialManager tutorialManager;

    [Header("Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneShot = true;

    [SerializeField] private string currentStepName;
    [SerializeField] private bool completeOnlyCurrentStep = true;

    private NetworkVariable<bool> used = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) { return; }

        if (!string.IsNullOrWhiteSpace(playerTag) && !other.CompareTag(playerTag))
        {
            return;
        }

        if (tutorialManager == null)
        {
            tutorialManager = FindFirstObjectByType<TUT_TutorialManager>();
        }

        if (tutorialManager == null) { return; }
        if (oneShot && used.Value) { return; }

        // if this trigger is networked, ask the server to use it.
        if (IsSpawned)
        {
            if (IsServer)
            {
                UseTriggerServer();
            }
            else
            {
                UseTriggerServerRpc();
            }

            return;
        }
        if (tutorialManager.CurrentStep.title == currentStepName && completeOnlyCurrentStep)
        {
            tutorialManager.CompleteCurrentStep();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UseTriggerServerRpc()
    {
        UseTriggerServer();
    }

    // complete the shared quest step from the server.
    private void UseTriggerServer()
    {
        if (oneShot && used.Value) { return; }

        if (oneShot)
        {
            used.Value = true;
        }

        if (tutorialManager == null)
        {
            tutorialManager = FindFirstObjectByType<TUT_TutorialManager>();
        }

        if (tutorialManager == null) { return; }

        if (tutorialManager.CurrentStep.title == currentStepName && completeOnlyCurrentStep)
        {
            tutorialManager.CompleteCurrentStep();
        }
    }
}