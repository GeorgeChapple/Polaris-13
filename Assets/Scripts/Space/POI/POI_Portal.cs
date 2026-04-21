using NUnit.Framework;
using System.Collections;
using Unity.Netcode;
using Unity.Services.Relay.Models;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

// Script By : George Chapple
// Summary   : 

public class POI_Portal : NetworkBehaviour
{
    public Transform destination;
    [SerializeField] private float teleportCooldownTime = 1;
    [SerializeField] private bool refillAir = false;
    [SerializeField] private AddToSpaceJunk toggleSpaceMovement = AddToSpaceJunk.none;
    private SP_SpaceManager spaceManager;
    public UnityEvent portalEntered;


    private enum AddToSpaceJunk
    {
        none,
        toggle
    }

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
        if (!IsSpawned)
        {
            GetComponent<NetworkObject>().Spawn();
        }
    }

    private void OnTriggerEnter(Collider col)
    {
        if (!spaceManager.cannotTeleport.Contains(col))
        {
            spaceManager.cannotTeleport.Add(col);
            NetworkObject netObj = col.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                CC_Movement player = netObj.GetComponent<CC_Movement>();
                Vector3 teleportPosition = Vector3.zero;
                if (destination != null)
                {
                    teleportPosition = destination.position;
                }
                else if (player != null && destination == null)
                {
                    teleportPosition = GameObject.FindGameObjectsWithTag("SpawnPoint")[player.OwnerClientId].transform.position;
                } 
                if (player != null)
                {
                    PlayerTeleportationRpc(netObj, teleportPosition, true, refillAir);
                }
                else
                {
                    ObjectTeleportationRpc(netObj, teleportPosition, false);
                }
                ToggleSpaceMovement(toggleSpaceMovement, netObj.gameObject);
                StartCoroutine(ObjectTeleportCooldown(teleportCooldownTime, col));
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void ObjectTeleportationRpc(NetworkObjectReference targetRef, Vector3 teleportPosition, bool resetVelocity)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        {
            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (resetVelocity)
                {
                    rb.linearVelocity = Vector3.zero;
                }
                rb.position = teleportPosition;
            }
        } 
    }

    [Rpc(SendTo.Everyone)]
    private void PlayerTeleportationRpc(NetworkObjectReference targetRef, Vector3 teleportPosition, bool resetVelocity, bool resetAir)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        {
            CC_Movement player = netObj.GetComponent<CC_Movement>();
            if (player != null && player.Body != null)
            {
                if (resetVelocity)
                {
                    player.Body.linearVelocity = Vector3.zero;
                }
                if (resetAir)
                {
                    CC_CharacterValues charVals = player.GetComponent<CC_CharacterValues>();
                    charVals.SetOxygen(charVals.maxOxygen);
                }
                player.Body.position = teleportPosition;
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

    private void ToggleSpaceMovement(AddToSpaceJunk toggle, GameObject obj)
    {
        if (toggle == AddToSpaceJunk.none || obj.tag == "Space")
        {
            return;
        }
        else if (toggle == AddToSpaceJunk.toggle)
        {
            if (spaceManager.debris.ContainsKey(obj))
            {
                spaceManager.debris.Remove(obj);
            }
            else
            {
                spaceManager.debris.Add(obj, spaceManager.rocket.worldDirection);
                obj.GetComponent<CustomGravityRigidbody>().useGravity = false;
            }
        }
    }
}
