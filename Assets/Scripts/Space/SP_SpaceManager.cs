using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.VisualScripting;
using UnityEngine;

// Script By : George Chapple
// Summary   : Manages space spawners and moves space objects depending on rocket direction and speed.

[RequireComponent(typeof(BoxCollider))]
public class SP_SpaceManager : NetworkBehaviour
{
    [SerializeField] private int maxDebris = 20;
    [HideInInspector] public RS_Move rocket; 
    public List<SP_SpawnSettings> spawnerSettings = new List<SP_SpawnSettings>();
    //[HideInInspector] public List<GameObject> foundObjects = new List<GameObject>();
    public Dictionary<GameObject, Vector3> debris = new Dictionary<GameObject, Vector3>();
    public Dictionary<SP_Spawner, int> spawners = new Dictionary<SP_Spawner, int>();
    [HideInInspector] public List<Collider> cannotTeleport = new List<Collider>();
    public Vector3 spaceBounds = new Vector3(20, 20, 20);
    [HideInInspector] public bool maxDebrisReached;
    [SerializeField] private List<GameObject> debrisDebugList = new List<GameObject>();

    private void Awake()
    {
        InitialiseComponents();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // only server controls junk spawning and movement
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        InitialiseComponents();
    }

    private void InitialiseComponents()
    {
        gameObject.tag = "Space";
        GetComponent<BoxCollider>().size = spaceBounds;
        rocket = FindFirstObjectByType<RS_Move>();
    }

    private void Start()
    {

        InitialiseSpawners(spawnerSettings);
    }

    void Update()
    {
        if (!IsServer) return;
        if (rocket == null) return;

        if (debris.Count >= maxDebris)
        {
            maxDebrisReached = true;
        }
        else
        {
            maxDebrisReached = false;
        }

        MoveDebris();

        //Collider[] colliders = Physics.OverlapBox(transform.position, spaceBounds / 2, transform.rotation);
        //foundObjects.Clear();

        //foreach (Collider col in colliders)
        //{
        //    foundObjects.Add(col.gameObject);
        //}

        debrisDebugList.Clear();
        foreach(GameObject obj in debris.Keys)
        {
            debrisDebugList.Add(obj);
        }
    }

    private void OnTriggerExit(Collider col)
    {
        StartCoroutine(WaitTriggerExit(col));
    }

    private IEnumerator WaitTriggerExit(Collider col)
    {
        float t = 0;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        NetworkObject netObj = col.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            if (netObj.GetComponent<CC_Movement>() && !cannotTeleport.Contains(col))
            {
                PlayerExitSpaceRpc(netObj);
            }
            else
            {
                ObjectExitSpaceRpc(netObj);
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void ObjectExitSpaceRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        { 
            SP_SpaceJunk spaceObj = netObj.GetComponent<SP_SpaceJunk>();
            if (spaceObj != null)
            {
                spaceObj.StartLerpScale(spaceObj.transform.localScale, Vector3.zero, true);
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PlayerExitSpaceRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        {
            CC_Movement player = netObj.GetComponent<CC_Movement>();
            if (player != null)
            {
                player.Body.linearVelocity = Vector3.zero;
                player.Body.position = GameObject.FindGameObjectsWithTag("SpawnPoint")[player.OwnerClientId].transform.position;
            }
        }
    }

    private void InitialiseSpawners(List<SP_SpawnSettings> spawnSettings)
    {
        foreach (SP_Spawner spawner in spawners.Keys)
        {
            // Safety Check
            if (!IsServer)
            {
                return;
            }

            // Despawn/destroy spawners
            NetworkObject netObj = spawner.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(spawner.gameObject);
            }
        }

        spawners.Clear();

        foreach (SP_SpawnSettings settings in spawnSettings)
        {
            //SortProbabilities(settings);
            GameObject newObject = new GameObject();
            newObject.AddComponent<SP_Spawner>();
            SP_Spawner newSpawner = newObject.GetComponent<SP_Spawner>();
            newSpawner.settings = settings;
            newObject.name = newSpawner.settings.spawnerName;

            NetworkObject netObj = newSpawner.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned)
            {
                netObj.Spawn();
            }
            else
            {
                if (IsServer)
                {
                    netObj = newSpawner.AddComponent<NetworkObject>();
                    netObj.Spawn();
                }
            }

            spawners.Add(newSpawner, 0);


            if (IsServer)
            {
                newSpawner.enabled = true;
            }
        }
    }

    private void MoveDebris()
    {
        List<GameObject> debrisObjects = new List<GameObject>(debris.Keys);

        foreach (GameObject obj in debrisObjects)
        {
            if (obj == null) continue;

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            NetworkTransform netTransform = obj.GetComponent<NetworkTransform>();

            Vector3 objDirection = (Vector3.back + debris[obj] - rocket.worldDirection).normalized * rocket.speed;
            if (rb != null)
            { 
                rb.MovePosition(rb.position + objDirection * Time.deltaTime);
            } 
            else
            {
                obj.transform.position = Vector3.Lerp(obj.transform.position, obj.transform.position + objDirection, Time.deltaTime);
            }
        }
    }

    public void RemoveDebris(GameObject obj)
    {
        if (obj == null) { return; }

        if (debris.ContainsKey(obj))
        {
            debris.Remove(obj);
        }
    }

    private void OnDrawGizmos()
    {
        CustomGizmos.DrawBox(Vector3.zero, transform.rotation, spaceBounds, Color.red);
        foreach (SP_SpawnSettings spawner in spawnerSettings)
        {
            if (spawner != null)
            {
                CustomGizmos.DrawBox(new Vector3(0, 0, spaceBounds.z / 2), transform.rotation, new Vector2(spawner.spawnbounds.x, spawner.spawnbounds.y), spawner.gizmoColour);
                CustomGizmos.DrawBox(new Vector3(0, 0, spaceBounds.z / 2), transform.rotation, new Vector2(spawner.spawnbounds.z, spawner.spawnbounds.w), spawner.gizmoColour);
            }
        }
    }
}