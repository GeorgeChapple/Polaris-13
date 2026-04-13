using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(fileName = "PushValues", menuName = "PushValues")]
public class PushValues : ScriptableObject
{
    public Vector3 pushVolumeBounds = new Vector3(5f, 5f, 10f);
    public float maxPushForce = 10f;
    public float torqueForce = 1f;
    public float chargeTime = 2f;
    public float cooldown = 0.5f;
    public float pushForce = 0f;
    public float chargingTimer = 0f;
    public float cooldownTimer = 0f;
}
