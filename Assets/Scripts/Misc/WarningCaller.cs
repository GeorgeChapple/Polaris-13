using UnityEngine;
using Unity.Netcode;

// Made By: Jason Lodge
// Summary: Calls warnings on network spawn.
public class WarningCaller : NetworkBehaviour
{
    [SerializeField] private string nameOfWarningToCall;

    private RS_WarningSystem ship;
    private UI_WarningSystem[] players;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Init();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        End();
    }

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