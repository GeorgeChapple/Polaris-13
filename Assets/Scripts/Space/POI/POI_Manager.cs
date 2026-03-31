using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

// Script By : George Chapple
// Summary   : 

public class POI_Manager : NetworkBehaviour
{
    public List<POI_Level> levels = new List<POI_Level>();
    [HideInInspector] public int levelsGenerated = 0;
    private int levelsGeneratedReset = 99;

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
}
