using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class SP_Junk : MonoBehaviour
{
    [HideInInspector] public Quaternion junkRotation;
    [HideInInspector] public Vector3 junkRotationRate;
    private SP_SpaceJunk spaceManager;
    [SerializeField] private float scaleSpeed = 1;
    private bool scaling = false;

    private void Awake()
    {
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
        if (!spaceManager.foundObjects.Contains(this.gameObject)) {
            StartCoroutine(LerpScale(transform.localScale, Vector3.zero, scaleSpeed, true));
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Rocket")) {
            spaceManager.debris.Remove(this.gameObject);
            if (this.GetComponent<Rigidbody>() != null)
            { 
                Rigidbody rb = this.AddComponent<Rigidbody>();
                rb.useGravity = false;
            }
        }
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
