using UnityEngine;

public class UI_Horizon : MonoBehaviour
{
    [SerializeField] private Transform t;

    private void LateUpdate()
    {
        if (t == null) { return; }
        transform.localRotation = Quaternion.Inverse(t.rotation);
    }
}