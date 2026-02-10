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
    [SerializeField] private float resetTime = 20;
    private float resetTimer;
    private bool scaling = false;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (GetComponent<Rigidbody>() == null)
        {
            rb = this.AddComponent<Rigidbody>();
        }
        resetTimer = resetTime;
        junkRotation.eulerAngles = Vector3.one * Random.value * 360;
        junkRotationRate = Vector3.one * Random.value * 5;
        spaceManager = FindFirstObjectByType<SP_SpaceJunk>();
    }

    private void Start()
    {
        StartCoroutine(LerpScale(Vector3.zero, transform.localScale, scaleSpeed, false));
    }

    // Update is called once per frame
    void Update()
    {
        resetTimer += Time.deltaTime;
        if (resetTimer > resetTime && !spaceManager.debris.ContainsKey(this.gameObject))
        {
            spaceManager.debris.Add(this.gameObject, spaceManager.rocket.worldDirection);
            rb.AddForce(objDirection * 100);
        }
        if (!spaceManager.foundObjects.Contains(this.gameObject)) {
            StartCoroutine(LerpScale(transform.localScale, Vector3.zero, scaleSpeed, true));
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        resetTimer = 0;
        rb.useGravity = false;
        Vector3 forceDirection = (Vector3.back - spaceManager.rocket.worldDirection).normalized;
        if (collision.gameObject.CompareTag("Rocket"))
        {
            forceDirection = collision.transform.position - transform.position.normalized; 
            rb.AddForce(forceDirection * 100);
        }
        rb.AddTorque(Vector3.one * Random.Range(-10, 10));
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
