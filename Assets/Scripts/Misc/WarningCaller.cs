using UnityEngine;
using Unity.Netcode;

// Made By: Jason Lodge
// Summary: Calls warnings on network spawn.
public class WarningCaller : NetworkBehaviour
{
    [SerializeField] private string nameOfWarningToCall;

    private RS_WarningSystem ship;
    private UI_WarningSystem[] players;

    // setup network state once the object has spawned.
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Init();
    }

    // clean up network subscriptions when despawned.
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        End();
    }

    // setup this UI element with its target data.
    private void Init()
    {
        ship = FindFirstObjectByType<RS_WarningSystem>();

        players = FindObjectsByType<UI_WarningSystem>(FindObjectsSortMode.None);

        if (ship != null) { ship.CallWarningByName(nameOfWarningToCall); }
        if (players.Length != 0)
        {
            foreach (UI_WarningSystem player in players)
            {
                player.CallWarningByName(nameOfWarningToCall);
            }
        }
    }

    private void End()
    {
        if (ship != null) { ship.EndWarningByName(nameOfWarningToCall); }
        if (players.Length != 0)
        {
            foreach (UI_WarningSystem player in players)
            {
                player.EndWarningByName(nameOfWarningToCall);
            }
        }
    }
}