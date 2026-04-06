using Unity.Multiplayer.Center.NetcodeForGameObjectsExample.DistributedAuthority;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

// Script By : George Chapple
// Summary   : 

public class POI_Initialise : NetworkBehaviour
{
    [SerializeField] private GameObject POI;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            Destroy(this);
        }
    }

    private void Start()
    {
        SP_SpaceManager m_Space = FindFirstObjectByType<SP_SpaceManager>();
        POI_Manager m_POI = FindFirstObjectByType<POI_Manager>();
        float spawnDistance = m_Space.spaceBounds.y;
        if (spawnDistance < 5000 )
        {
            spawnDistance = 5000;
        }
        spawnDistance += 5000 * m_POI.levelsGenerated;
        spawnDistance += POI.GetComponent<POI_Level>().bounds.y;

        GameObject newPOI = Instantiate(POI, new Vector3(0, spawnDistance, 0), transform.rotation);

        NetworkObject netObj = newPOI.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned)
        {
            netObj.Spawn();
        }

        POI_Level newLevel = newPOI.GetComponent<POI_Level>();
        POI_Portal thisPortal = GetComponent<POI_Portal>();
        thisPortal.destination = newLevel.StartPosition;
        newLevel.mainPortal = thisPortal;
        newLevel.ExitPortal.destination = thisPortal.transform;
        
        m_POI.levels.Add(newPOI.GetComponent<POI_Level>());
        m_POI.levelsGenerated++;
        m_POI.CheckGeneratedReset();
        newLevel.enabled = true;
        Destroy(this);
    }
}
