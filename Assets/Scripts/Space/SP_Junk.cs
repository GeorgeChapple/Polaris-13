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
        GetObjectDirection();
        rb.AddForce(objDirection * 100);
        rb.AddTorque(Vector3.one * Random.Range(-10, 10));
    }

    // Update is called once per frame
    void Update()
    {
        GetObjectDirection();
        resetTimer += Time.deltaTime;
        if (resetTimer > resetTime && !spaceManager.debris.ContainsKey(this.gameObject))
        {
            rb.AddForce(objDirection * 100, ForceMode.Acceleration);
        }
        if (!spaceManager.foundObjects.Contains(this.gameObject)) {
            StartCoroutine(LerpScale(transform.localScale, Vector3.zero, scaleSpeed, true));
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        resetTimer = 0;
        rb.useGravity = false;
        Vector3 forceDirection;
        //forceDirection = (Vector3.back - spaceManager.rocket.worldDirection).normalized;
        if (collision.gameObject.CompareTag("Rocket"))
        {
            resetTimer = resetTime - 1;
            if (resetTimer < 0)
            {
                resetTimer = 0;
            }
            //forceDirection = (collision.transform.position - transform.position).normalized;
            forceDirection = Vector3.zero;
            float xPos = transform.position.x;
            if (xPos < 0)
            {
                forceDirection += Vector3.left;
            }
            else
            {
                forceDirection += Vector3.right;
            }
            rb.AddForce(forceDirection * 100);
            rb.AddTorque(Vector3.one * Random.Range(-10, 10));
        }
        rb.AddTorque(Vector3.one * Random.Range(-10, 10));
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
