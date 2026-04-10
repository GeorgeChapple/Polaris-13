using System.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

// Script By : George Chapple
// Summary   : Handles spawning and despawning of space objects

public class SP_SpaceJunk : NetworkBehaviour
{
    [HideInInspector] public Quaternion junkRotation;
    [HideInInspector] public Vector3 junkRotationRate;
    [HideInInspector] public SP_Spawner spawner;
    [HideInInspector] public bool canTeleport = true;
    [SerializeField] private float scaleSpeed = 1;
    [SerializeField] private GameObject destroyVFX;
    private SP_SpaceManager spaceManager;
    private bool scaling = false;
    private bool destroyRequested = false;
    private Rigidbody rb;

    private void Awake()
    {
        InitialiseComponents();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only server decides if junk gets destroyed
        if (!IsServer)
        {
            return;
        }

        if (spaceManager != null && spaceManager.debris.ContainsKey(gameObject) && collision.gameObject.CompareTag("Rocket"))
        {
            StartCoroutine(LerpScale(transform.localScale, Vector3.zero, scaleSpeed, true));
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        InitialiseComponents();
    }

    private void InitialiseComponents()
    {
        rb = GetComponent<Rigidbody>();
        spaceManager = FindFirstObjectByType<SP_SpaceManager>();
    }

    private void Start()
    {
        StartLerpScale(Vector3.zero, transform.localScale, false);
        if (rb != null)
        {
            rb.AddTorque(Vector3.one * Random.Range(-10, 10));
        } 
        if (IsServer && spawner != null)
        { 
            spaceManager.spawners[spawner]++;
        }
    }

    public void StartLerpScale(Vector3 start, Vector3 end, bool destroy)
    {
        StartCoroutine(LerpScale(start, end, scaleSpeed, destroy));
    }

    private IEnumerator LerpScale(Vector3 start, Vector3 end, float duration, bool destroy)
    {
        if (!scaling)
        {
            if (destroy && spaceManager != null && spaceManager.debris.ContainsKey(this.gameObject))
            {
                spaceManager.debris.Remove(this.gameObject);
            }

            scaling = true;
            transform.localScale = start;
            float t = 0;

            while (t < 1)
            {
                t += Time.deltaTime / duration;
                transform.localScale = Vector3.Lerp(start, end, Easing.Sine.Out(t));
                yield return null;
            }

            transform.localScale = end;

            if (destroy && !destroyRequested)
            {
                destroyRequested = true;
                DestroyJunk();
            }

            scaling = false;
        }
    }

    private void DestroyJunk()
    {
        // safety check
        if (!IsServer)
        {
            return;
        }

        // spawn destroy VFX on server
        if (destroyVFX != null)
        {
            GameObject vfxInstance = Instantiate(destroyVFX, transform.position, transform.rotation);
            NetworkObject vfxNetObj = vfxInstance.GetComponent<NetworkObject>();

            if (vfxNetObj != null && !vfxNetObj.IsSpawned)
            {
                vfxNetObj.Spawn();
            }
        }

        if (spawner != null)
        { 
            spaceManager.spawners[spawner]--;
        }

        // despawn junk over network
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (IsServer && netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }

        Destroy(this.gameObject);
    }
    public void RemoveFromSpaceManager()
    {
        if (spaceManager == null)
        {
            spaceManager = FindFirstObjectByType<SP_SpaceManager>();
        }

        if (spaceManager == null)
        {
            Debug.LogWarning("SP_SpaceJunk could not find SP_SpaceManager.", this);
            return;
        }

        spaceManager.RemoveDebris(gameObject);
    }
}