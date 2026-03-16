using UnityEngine;

public class GravityToggler : MonoBehaviour
{
    [SerializeField] private bool useGravity;

    private void OnTriggerEnter(Collider col)
    {
        CustomGravityRigidbodyForEntities gravityBody = col.GetComponent<CustomGravityRigidbodyForEntities>();
        if (gravityBody != null && !gravityBody.triggered)
        {
            gravityBody.triggered = true;
            gravityBody.useGravity = useGravity;
        }
    }

    private void OnTriggerExit(Collider col)
    {
        CustomGravityRigidbodyForEntities gravityBody = col.GetComponent<CustomGravityRigidbodyForEntities>();
        if (gravityBody != null)
        {
            gravityBody.triggered = false;
        }
    }
}
