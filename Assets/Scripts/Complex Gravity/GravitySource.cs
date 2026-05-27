using UnityEngine;

public class GravitySource : MonoBehaviour
{

    public virtual Vector3 GetGravity(Vector3 position)
    {
        return Physics.gravity;
    }

    public virtual bool ProvidesOxygen(Vector3 position)
    {
        return false;
    }

    public virtual Vector3 GetGravityAndOxygen(Vector3 position, out bool providesOxygen)
    {
        providesOxygen = ProvidesOxygen(position);
        return GetGravity(position);
    }

    void OnEnable()
    {
        CustomGravity.Register(this);
    }

    void OnDisable()
    {
        CustomGravity.Unregister(this);
    }
}