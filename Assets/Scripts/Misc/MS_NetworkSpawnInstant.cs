using UnityEngine;
using Unity.Netcode;

public class MS_NetworkSpawnInstant : NetworkBehaviour
{
    private void Start()
    {
        if (!NetworkObject.IsSpawned)
        {
            NetworkObject.Spawn();
        }
    }
}
