using UnityEngine;

// Made by: Jason Lodge
// Original by: CatLikeCoding Available at:https://catlikecoding.com/unity/tutorials/movement/complex-gravity/
// Summary: Gravity source that uses a box volume.
// Intended as an upgraded replacement for gravity plane.
// Uses editor set size + offset.
// Anything outside the volume gets no gravity.

public class GravityVolume : GravitySource
{
    [Tooltip("If this gravity volume should give oxygen to the player.")]
    [SerializeField] private bool provideOxygen = false;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private Vector3 volumeSize = new Vector3(1f, 1f, 1f);
    [SerializeField] private Vector3 volumeOffset = Vector3.zero;

    [SerializeField] private bool directionalGravity = true;
    [Tooltip("Local direction used when directional gravity is enabled.")]
    [SerializeField] private Vector3 gravityDirection = Vector3.down;

    public override Vector3 GetGravity(Vector3 position)
    {
        Vector3 local = GetLocalVolumePosition(position);

        // outside the volume? no gravity bro
        if (!IsInsideVolumeLocal(local))
        {
            return Vector3.zero;
        }

        // directional gravity mode
        if (directionalGravity)
        {
            Vector3 dir = gravityDirection.normalized;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector3.down;
            }

            return transform.rotation * dir * gravity;
        }

        // center pull mode
        Vector3 toCenter = -local;

        if (toCenter.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        return transform.rotation * toCenter.normalized * gravity;
    }

    public override bool ProvidesOxygen(Vector3 position)
    {
        if (!provideOxygen) { return false; }

        Vector3 local = GetLocalVolumePosition(position);

        // oxygen uses the same volume as gravity
        return IsInsideVolumeLocal(local);
    }

    private Vector3 GetLocalVolumePosition(Vector3 position)
    {
        // convert world position into the volume's local space
        Vector3 offset = position - transform.position;
        Vector3 local = Quaternion.Inverse(transform.rotation) * offset;

        // move into the volume's local offset space
        local -= volumeOffset;

        return local;
    }

    private bool IsInsideVolumeLocal(Vector3 local)
    {
        // half extents of the gravity volume
        Vector3 halfExtents = volumeSize * 0.5f;

        return
            local.x >= -halfExtents.x && local.x <= halfExtents.x &&
            local.y >= -halfExtents.y && local.y <= halfExtents.y &&
            local.z >= -halfExtents.z && local.z <= halfExtents.z;
    }

    void OnDrawGizmos()
    {
        Quaternion rot = transform.rotation;
        Vector3 center = transform.position + rot * volumeOffset;

        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(Vector3.zero, volumeSize);

        Vector3 half = volumeSize * 0.5f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new Vector3(0f, -half.y, 0f), new Vector3(volumeSize.x, 0f, volumeSize.z));

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(0f, half.y, 0f), new Vector3(volumeSize.x, 0f, volumeSize.z));

        Gizmos.color = Color.green;
        if (directionalGravity)
        {
            Vector3 dir = gravityDirection.sqrMagnitude > 0.0001f ? gravityDirection.normalized : Vector3.down;

            Gizmos.DrawLine(Vector3.zero, dir * Mathf.Min(volumeSize.magnitude * 0.25f, 1.5f));
        }
        else
        {
            Gizmos.DrawSphere(Vector3.zero, 0.1f);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}