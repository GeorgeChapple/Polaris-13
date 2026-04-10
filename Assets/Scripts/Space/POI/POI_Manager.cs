using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

// Script By : George Chapple
// Summary   : 

public class POI_Manager : NetworkBehaviour
{
    [HideInInspector] private const int levelsGeneratedReset = 99;
    [HideInInspector] public int levelsGenerated = 0;
    public List<POI_Level> levels = new List<POI_Level>();

    private void Awake()
    {
        InitialiseComponents();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            enabled = false;
            return;
        }

        InitialiseComponents();
    }

    private void InitialiseComponents()
    {

    }

    public void CheckGeneratedReset()
    {
        if (levelsGenerated > levelsGeneratedReset)
        {
            levelsGenerated = 0;
        }
    }
}
