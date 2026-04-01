using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

// Script By : George Chapple
// Summary   : 

[RequireComponent(typeof(SphereCollider))]
public class POI_Portal : NetworkBehaviour
{
    public Transform destination;
    [SerializeField] private float teleportCooldownTime = 1;
    [SerializeField] private AddToSpaceJunk toggleSpaceMovement = AddToSpaceJunk.none;
    private SP_SpaceManager spaceManager;
    public UnityEvent portalEntered;


    private enum AddToSpaceJunk
    {
        none,
        toggle
    }


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
        spaceManager = FindFirstObjectByType<SP_SpaceManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.GetComponent<Rigidbody>();
        CC_Movement player = other.GetComponent<CC_Movement>();
        SP_SpaceJunk spaceObj = other.GetComponent<SP_SpaceJunk>();
        if (rb != null)
        {
            Vector3 teleportPoint = Vector3.zero;
            if (destination != null)
            { 
                teleportPoint = destination.position;  
            } 
            else if (player != null) 
            {
                teleportPoint = GameObject.FindGameObjectsWithTag("SpawnPoint")[player.OwnerClientId].transform.position;
            }
            else if (spaceObj != null)
            {
                teleportPoint = Vector3.zero;
            }
            if (player != null && player.canTeleport)
            {
                StartCoroutine(ObjectTeleportCooldown(teleportCooldownTime, player, rb, teleportPoint));
            }
            else if (spaceObj != null && spaceObj.canTeleport)
            {
                StartCoroutine(ObjectTeleportCooldown(teleportCooldownTime, spaceObj, rb, teleportPoint));
            }
        }
    }

    private IEnumerator ObjectTeleportCooldown(float duration, NetworkBehaviour component, Rigidbody rb, Vector3 teleportPoint)
    {
        ToggleSpaceMovement(toggleSpaceMovement, rb.gameObject);
        component.GetType().GetProperty("canTeleport").SetValue(component, false);
        rb.position = teleportPoint;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }
        component.GetType().GetProperty("canTeleport").SetValue(component, true);
    }

    private void ToggleSpaceMovement(AddToSpaceJunk toggle, GameObject obj)
    {
        if (toggle == AddToSpaceJunk.none)
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
            }
        }
    }
}
