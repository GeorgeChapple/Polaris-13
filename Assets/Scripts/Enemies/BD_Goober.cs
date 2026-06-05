using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class BD_Goober : NetworkBehaviour
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
    [SerializeField] private Rigidbody rb;
    public Vector3 velocity = Vector3.forward;

    void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    void Update()
    {
        if (!IsServer)
        {
            return;
        }

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
        Boid.Constrain(rb, bounds);
    }

    private void FlockUpdate(Vector3 target)
    {
        Vector3 targetVelocity = Vector3.zero;

        if (neighbours.Count > 0)
        {
            targetVelocity += Boid.Separation(rb, neighbours) * separation;
            if (targetVelocity.magnitude < alignmentThreshold)
            {
                targetVelocity += Boid.Alignment(rb, target, neighbours) * alignment;
            }
            if (targetVelocity.magnitude < cohesionThreshold)
            {
                targetVelocity += Boid.Cohesion(rb, neighbours) * cohesion;
            }
        }

        if (targetVelocity == Vector3.zero)
        {
            targetVelocity = Boid.Seek(rb, target);
        }

        targetVelocity.Normalize();
        velocity = targetVelocity;
        MoveBoid();
    }

    private void MoveBoid()
    {
        if (velocity != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        }

        rb.MovePosition(Vector3.Lerp(rb.position, rb.position + velocity * speed, Time.deltaTime));
    }
}