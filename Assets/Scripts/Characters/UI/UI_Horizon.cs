using UnityEngine;

public class UI_Horizon : MonoBehaviour
{
    [SerializeField] private Transform t;

    private void FixedUpdate()
    {
        Vector3 BLEH = Vector3.ProjectOnPlane(t.rotation.eulerAngles, Vector3.up);
        transform.rotation = Quaternion.Euler(BLEH);
    }
}
