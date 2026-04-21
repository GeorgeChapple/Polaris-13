using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Script By : George Chapple
// Summary   : 

[RequireComponent(typeof(BoxCollider))]
public class POI_Level : NetworkBehaviour
{
    public Vector3 bounds = new Vector3(100, 100, 100); 
    [HideInInspector] public POI_Portal mainPortal;
    [SerializeField] private Transform startPosition;
    [SerializeField] private POI_Portal exitPortal;
    private int players = 0;
    private SP_SpaceManager spaceManager;

    // Getters
    public int Players => players;
    public Transform StartPosition => startPosition;
    public POI_Portal ExitPortal => exitPortal;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        spaceManager = FindFirstObjectByType<SP_SpaceManager>();
        GetComponent<BoxCollider>().size = bounds;
        if (IsServer)
        {
            StartCoroutine(DeleteChack());
        }
    }

    private IEnumerator DeleteChack()
    {
        while (true)
        {
            bool found = false;
            if (mainPortal == null)
            {
                CC_Movement[] players = FindObjectsByType<CC_Movement>(FindObjectsSortMode.None);
                Collider[] foundObjects = Physics.OverlapBox(transform.position, bounds * 0.5f, transform.rotation);
                foreach (CC_Movement player in players)
                {
                    if (foundObjects.Contains(player.GetComponent<Collider>()))
                    {
                        found = true;
                    }
                }
                if (!found)
                {
                    break;
                }
            }
            yield return new WaitForSeconds(5f);
        }
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
        Destroy(this.gameObject);
    }

    private void OnTriggerEnter(Collider col)
    {
        if (IsServer && col.GetComponent<CC_Movement>())
        {
            players++;
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
            if (netObj.GetComponent<CC_Movement>() && !spaceManager.cannotTeleport.Contains(col))
            {
                if (IsServer)
                {
                    players--;
                }
                PlayerExitSpacePOIRpc(netObj);
            }
            else
            {
                ObjectExitSpacePOIRpc(netObj);
            }
        } 
        if (!spaceManager.cannotTeleport.Contains(col))
        {
            spaceManager.cannotTeleport.Add(col);
            StartCoroutine(ObjectTeleportCooldown(0.5f, col));
        }
    }

    [Rpc(SendTo.Server)]
    private void ObjectExitSpacePOIRpc(NetworkObjectReference targetRef)
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
    private void PlayerExitSpacePOIRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        {
            CC_Movement player = netObj.GetComponent<CC_Movement>();
            if (player != null)
            {
                player.Body.linearVelocity = Vector3.zero;
                if (exitPortal.destination != null)
                {
                    player.Body.position = exitPortal.destination.position;
                    if (!spaceManager.debris.ContainsKey(netObj.gameObject))
                    {
                        spaceManager.debris.Add(netObj.gameObject, spaceManager.rocket.worldDirection);
                    }
                }
                else
                { 
                    player.Body.position = GameObject.FindGameObjectsWithTag("SpawnPoint")[player.OwnerClientId].transform.position;
                }
            }
        }
    }

    private IEnumerator ObjectTeleportCooldown(float duration, Collider col)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (col != null)
        {
            spaceManager.cannotTeleport.Remove(col);
        }
    }

    private void OnDrawGizmos()
    {
        CustomGizmos.DrawBox(transform.position, transform.rotation, bounds, Color.red);
    }
}
