using NUnit.Framework;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

public class Boid
{
    public static void Constrain(Rigidbody self, Vector3 bounds)
    {
        Vector3 position = self.position;
        bounds /= 2f;

        if (position.x < bounds.x * -1f) { position.x = bounds.x; }
        else if (position.x > bounds.x) {  position.x = bounds.x * -1; }

        if (position.y < bounds.y * -1f) { position.y = bounds.y; }
        else if (position.y > bounds.y) { position.y = bounds.x * -1; }

        if (position.z < bounds.z * -1f) { position.z = bounds.z; }
        else if (position.z > bounds.z) { position.z = bounds.z * -1; }

        self.position = position;
    }

    public static Vector3 Seek(Rigidbody self, Vector3 target)
    {
        Vector3 newVelocity = target - self.position;
        newVelocity.Normalize();
        return newVelocity;
    }

    public static Vector3 Flee(Rigidbody self, Vector3 target)
    {
        Vector3 newVelocity = self.position - target;
        newVelocity.Normalize();
        return newVelocity;
    }

    public static Vector3 Alignment(Rigidbody self, Vector3 target, List<Rigidbody> neighbours)
    {
        Vector3 newVelocity = Vector3.zero;
        foreach (Rigidbody rb in neighbours)
        {
            newVelocity += rb.GetComponent<BD_Goober>().velocity;
        }
        newVelocity /= neighbours.Count;
        newVelocity.Normalize();
        if (target != Vector3.zero)
        {
            Vector3 targetDirection = target - self.position;
            targetDirection.Normalize();
            newVelocity += targetDirection;
            newVelocity /= 2;
            newVelocity.Normalize();
        }
        return newVelocity;
    }

    public static Vector3 Cohesion(Rigidbody self, List<Rigidbody> neighbours)
    {
        Vector3 averageLocation = Vector3.zero;
        foreach (Rigidbody rb in neighbours)
        {
            averageLocation += rb.position;
        }
        averageLocation /= neighbours.Count;
        return Seek(self, averageLocation);
    }

    public static Vector3 Separation(Rigidbody self, List<Rigidbody> neighbours)
    {
        Vector3 newVelocity = Vector3.zero;
        foreach (Rigidbody rb in neighbours)
        {
            newVelocity += Flee(self, rb.position);
        }
        newVelocity.Normalize();
        return newVelocity;
    }
}
