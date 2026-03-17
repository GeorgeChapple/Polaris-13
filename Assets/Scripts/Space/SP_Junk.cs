using System.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class SP_Junk : NetworkBehaviour
{
    [HideInInspector] public Quaternion junkRotation;
    [HideInInspector] public Vector3 junkRotationRate;
    [HideInInspector] public Vector3 objDirection;
    [SerializeField] private float scaleSpeed = 1;
    [SerializeField] private Vector2 sizeSpread = new Vector2(0.7f, 1.3f);
    [SerializeField] private GameObject destroyVFX;
    private SP_SpaceJunk spaceManager;
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
        if (rb == null)
        {
            rb = this.AddComponent<Rigidbody>();
        }

        transform.localScale = Vector3.one * Random.Range(sizeSpread.x, sizeSpread.y);
        spaceManager = FindFirstObjectByType<SP_SpaceJunk>();
    }

    private void Start()
    {
        StartCoroutine(LerpScale(Vector3.zero, transform.localScale, scaleSpeed, false));
        GetObjectDirection();
        rb.AddTorque(Vector3.one * Random.Range(-10, 10));
    }

    // Update is called once per frame
    void Update()
    {
        GetObjectDirection();

        // only server decides if junk should despawn
        if (!IsServer)
        {
            return;
        }

        if (spaceManager != null && !spaceManager.foundObjects.Contains(this.gameObject))
        {
            StartCoroutine(LerpScale(transform.localScale, Vector3.zero, scaleSpeed, true));
        }
    }

    private void GetObjectDirection()
    {
        if (spaceManager != null && spaceManager.debris.ContainsKey(this.gameObject))
        {
            objDirection = (Vector3.back + spaceManager.debris[this.gameObject] - spaceManager.rocket.worldDirection).normalized * spaceManager.rocket.speed;
        }
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

        // despawn junk over network
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
}