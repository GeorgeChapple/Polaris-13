using System.Collections;
using System.Net.Sockets;
using Unity.VisualScripting;
using UnityEngine;

public class SP_Junk : MonoBehaviour
{
    [HideInInspector] public Quaternion junkRotation;
    [HideInInspector] public Vector3 junkRotationRate;
    [HideInInspector] public Vector3 objDirection;
    private SP_SpaceJunk spaceManager;
    [SerializeField] private float scaleSpeed = 1;
    private bool scaling = false;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (GetComponent<Rigidbody>() == null)
        {
            rb = this.AddComponent<Rigidbody>();
        }
        spaceManager = FindFirstObjectByType<SP_SpaceJunk>();
    }

    private void Start()
    {
        StartCoroutine(LerpScale(Vector3.zero, transform.localScale, scaleSpeed, false));
        GetObjectDirection();
        rb.AddTorque(Vector3.one * Random.Range(-100, 100));
    }

    // Update is called once per frame
    void Update()
    {
        GetObjectDirection();
        if (!spaceManager.foundObjects.Contains(this.gameObject)) {
            StartCoroutine(LerpScale(transform.localScale, Vector3.zero, scaleSpeed, true));
        }
    }

    private void GetObjectDirection()
    { 
        objDirection = (Vector3.back + spaceManager.debris[this.gameObject] - spaceManager.rocket.worldDirection).normalized * spaceManager.rocket.speed;
    }

    private IEnumerator LerpScale(Vector3 start, Vector3 end, float duration, bool destroy)
    {
        if (!scaling)
        {
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
            if (destroy)
            {
                spaceManager.debris.Remove(this.gameObject);
                Destroy(this.gameObject);
            }
            scaling = false;
        }
    }
}
