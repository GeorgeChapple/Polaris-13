using Unity.Netcode;
using UnityEngine;

// Script By : George Chapple
// Summary   : 

public class POI_Level : NetworkBehaviour
{
    public Vector3 bounds = new Vector3(100, 100, 100);

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
