using Unity.VisualScripting;
using UnityEngine;

// Made by: Jason Lodge
// Original by: CatLikeCoding Available at:https://catlikecoding.com/unity/tutorials/movement/complex-gravity/
// Summary: Gravity source that uses a mesh volume.
// Anything outside the mesh gets no gravity.
// Mesh needs to be closed for inside checks to work.

public class GravityMesh : GravitySource
{
    [Tooltip("If this gravity mesh should give oxygen to the player.")]
    [SerializeField] private bool provideOxygen = false;

    [SerializeField] private float gravity = 9.81f;

    [Tooltip("Mesh used as the gravity volume.")]
    [SerializeField] private Mesh volumeMesh;

    [SerializeField] private bool directionalGravity = true;

    [Tooltip("Local direction used when directional gravity is enabled.")]
    [SerializeField] private Vector3 gravityDirection = Vector3.down;

    [Header("Inside Check")]
    [Tooltip("Small offset used to avoid ray hits starting on triangle edges.")]
    [SerializeField] private float insideCheckOffset = 0.0001f;

    [Header("Debug")]
    [SerializeField] private bool drawWireMesh = false;
    [SerializeField] private bool drawRay = false;

    public override Vector3 GetGravity(Vector3 position)
    {
        if (!IsInsideMesh(position))
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
        Vector3 local = transform.InverseTransformPoint(position);
        Vector3 toCenter = volumeMesh.bounds.center - local;

        if (toCenter.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        return transform.TransformDirection(toCenter.normalized) * gravity;
    }

    public override bool ProvidesOxygen(Vector3 position)
    {
        if (!provideOxygen) { return false; }

        // oxygen uses the same mesh volume as gravity
        return IsInsideMesh(position);
    }

    private bool IsInsideMesh(Vector3 position)
    {
        Mesh mesh = GetVolumeMesh();
        if (mesh == null) { return false; }

        Vector3 localPoint = transform.InverseTransformPoint(position);

        // quick bounds check first because triangle checks are more expensive
        if (!mesh.bounds.Contains(localPoint))
        {
            return false;
        }


        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        // just need to choose a direction, doesnt really matter but may hit triangle edges so just slightly off should do
        Vector3 rayDirection = new Vector3(1f, 0.131f, 0.253f).normalized;

        // small offset so we don't start on a surface
        Vector3 rayOrigin = localPoint + (rayDirection * insideCheckOffset);

        if (drawRay) { Debug.DrawRay(rayOrigin, rayDirection); }

        int hitCount = 0;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];

            if (RayIntersectsTriangle(rayOrigin, rayDirection, a, b, c))
            {
                hitCount++;
            }
        }

        // odd number of hits means inside
        return (hitCount % 2) == 1;
    }

    private bool RayIntersectsTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;

        Vector3 h = Vector3.Cross(direction, edge2);
        float det = Vector3.Dot(edge1, h);

        if (det > -insideCheckOffset && det < insideCheckOffset)
        {
            return false;
        }

        float invDet = 1f / det;

        Vector3 s = origin - a;
        float u = invDet * Vector3.Dot(s, h);

        if (u < 0f || u > 1f)
        {
            return false;
        }

        Vector3 q = Vector3.Cross(s, edge1);
        float v = invDet * Vector3.Dot(direction, q);

        if (v < 0f || u + v > 1f)
        {
            return false;
        }

        float t = invDet * Vector3.Dot(edge2, q);

        return t > insideCheckOffset;
    }

    private Mesh GetVolumeMesh()
    {
        if (volumeMesh != null)
        {
            return volumeMesh;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            return meshFilter.sharedMesh;
        }

        return null;
    }

    void OnDrawGizmos()
    {
        if (!drawWireMesh) { return; }
        Mesh mesh = GetVolumeMesh();
        if (mesh == null) { return; }

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = Color.white;
        Gizmos.DrawWireMesh(mesh);

        Gizmos.color = Color.green;
        if (directionalGravity)
        {
            Vector3 dir = gravityDirection.sqrMagnitude > 0.0001f ? gravityDirection.normalized : Vector3.down;

            Gizmos.DrawLine(Vector3.zero, dir * Mathf.Min(mesh.bounds.size.magnitude * 0.25f, 1.5f));
        }
        else
        {
            Gizmos.DrawSphere(mesh.bounds.center, 0.1f);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}