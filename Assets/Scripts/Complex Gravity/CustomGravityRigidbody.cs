using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomGravityRigidbody : MonoBehaviour
{
    [SerializeField]
    protected bool floatToSleep = false;

    protected Rigidbody body;

    protected float floatDelay;

    protected virtual void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = this.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
    }

    protected virtual void FixedUpdate()
    {
        if (HandleFloatToSleep()) { return; }

        ApplyGravity();
    }

    protected bool HandleFloatToSleep()
    {
        if (!floatToSleep) { return false; }

        if (body.IsSleeping())
        {
            floatDelay = 0f;
            return true;
        }

        if (body.linearVelocity.sqrMagnitude < 0.0001f)
        {
            floatDelay += Time.deltaTime;
            if (floatDelay >= 1f)
            {
                return true;
            }
        }
        else
        {
            floatDelay = 0f;
        }

        return false;
    }

    protected virtual void ApplyGravity()
    {
        body.AddForce(
            CustomGravity.GetGravity(body.position), ForceMode.Acceleration
        );
    }
}