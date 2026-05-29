using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomGravityRigidbody : NetworkBehaviour
{
    [SerializeField]
    protected bool floatToSleep = false;

    public bool useGravity;
    [HideInInspector] public bool triggered = false;

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

        if (useGravity) { triggered = true; }
    }

    protected virtual void FixedUpdate()
    {
        if (!IsOwner) { return; } // realistically this should only ever even try to run on the server host cus it'll just get replicated for its positions
        if (useGravity)
        {
            if (HandleFloatToSleep()) { return; }

            ApplyGravity();
        }
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
            floatDelay += Time.fixedDeltaTime;
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

    // apply custom gravity as acceleration.
    protected virtual void ApplyGravity()
    {
        body.AddForce(
            CustomGravity.GetGravity(body.position), ForceMode.Acceleration
        );
    }
}