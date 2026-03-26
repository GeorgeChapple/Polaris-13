using Unity.Netcode;
using UnityEngine;

public class GravityToggler : NetworkBehaviour
{
    [SerializeField] private bool useGravity;
    [SerializeField] private bool planet;

    private void OnTriggerEnter(Collider col)
    {
        CustomGravityRigidbody gravityBody = col.GetComponent<CustomGravityRigidbody>();
        if (gravityBody != null && !gravityBody.triggered)
        {
            gravityBody.triggered = true;
            gravityBody.useGravity = useGravity;
            if (planet) 
            { 
                col.GetComponent<NetworkObject>().TrySetParent(transform.parent, true);
            }
            
        }
    }

    private void OnTriggerExit(Collider col)
    {
        CustomGravityRigidbody gravityBody = col.GetComponent<CustomGravityRigidbody>();
        if (gravityBody != null)
        {
            gravityBody.triggered = false;
            if (planet)
            {
                gravityBody.useGravity = !useGravity;
                col.GetComponent<NetworkObject>().TryRemoveParent(true);
            }
        }
    }
}
