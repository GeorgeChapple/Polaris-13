using UnityEngine;

public class GravityToggler : MonoBehaviour
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
            }
        }
    }
}
