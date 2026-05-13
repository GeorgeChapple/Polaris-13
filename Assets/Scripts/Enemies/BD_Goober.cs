using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class BD_Goober : MonoBehaviour
{
    private List<Rigidbody> neighbours = new List<Rigidbody>();
    public Transform target;
    [SerializeField] private float speed = 10f;
    [SerializeField] private float separation = 1f;
    [SerializeField] private float alignment = 1f;
    [SerializeField] private float alignmentThreshold = 1f;
    [SerializeField] private float cohesion = 1f;
    [SerializeField] private float cohesionThreshold = 1f;
    [SerializeField] private Vector3 bounds = new Vector3(20, 20, 20);
    private Rigidbody rb;
    public Vector3 velocity = Vector3.forward;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        neighbours.Clear();
        foreach (BD_Goober gb in FindObjectsByType<BD_Goober>(FindObjectsSortMode.None))
        {
            if (gb != this)
            { 
                neighbours.Add(gb.GetComponent<Rigidbody>());
            }
        }
        if (target != null)
        {
            FlockUpdate(target.position);
        }
        else
        {
            FlockUpdate(Vector3.zero);
        }
        //Boid.Constrain(rb, bounds);
    }

    private void FlockUpdate(Vector3 target)
    {
        Vector3 targetVelocity = Vector3.zero;
        targetVelocity += Boid.Separation(rb, neighbours) * separation;
        if (targetVelocity.magnitude < alignmentThreshold)
        {
            targetVelocity += Boid.Alignment(rb, target, neighbours) * alignment;
        }
        if (targetVelocity.magnitude < cohesionThreshold)
        {
            targetVelocity += Boid.Cohesion(rb, neighbours) * cohesion;
        }
        targetVelocity.Normalize();
        velocity = targetVelocity;
        MoveBoid();
    }

    private void MoveBoid()
    {
        transform.rotation = Quaternion.LookRotation((rb.position + velocity).normalized, Vector3.up);
        rb.MovePosition(Vector3.Lerp(rb.position, rb.position + velocity * speed, Time.deltaTime));
    }
}
