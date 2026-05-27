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

    private Mesh cachedMesh;
    private Vector3[] cachedVertices;
    private int[] cachedTriangles;
    private Bounds cachedBounds;
    private Vector3 cachedGravityDirection;

    private static readonly Vector3 insideCheckDirection = new Vector3(1f, 0.131f, 0.253f).normalized;

    private void Awake()
    {
        CacheMeshData();
    }

    private void OnValidate()
    {
        CacheMeshData();
    }

    public override Vector3 GetGravity(Vector3 position)
    {
        if (!IsInsideMesh(position))
        {
            return Vector3.zero;
        }

        return GetGravityFromInsidePosition(position);
    }

    public override bool ProvidesOxygen(Vector3 position)
    {
        if (!provideOxygen) { return false; }
        return IsInsideMesh(position);
    }

    public override Vector3 GetGravityAndOxygen(Vector3 position, out bool providesOxygen)
    {
        bool inside = IsInsideMesh(position);

        providesOxygen = provideOxygen && inside;

        if (!inside)
        {
            return Vector3.zero;
        }

        return GetGravityFromInsidePosition(position);
    }

    private Vector3 GetGravityFromInsidePosition(Vector3 position)
    {
        // directional gravity mode
        if (directionalGravity)
        {
            return transform.rotation * cachedGravityDirection * gravity;
        }

        // center pull mode
        Vector3 local = transform.InverseTransformPoint(position);
        Vector3 toCenter = cachedBounds.center - local;

        if (toCenter.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        return transform.TransformDirection(toCenter.normalized) * gravity;
    }

    private bool IsInsideMesh(Vector3 position)
    {
        if (cachedMesh == null) { return false; }
        if (cachedVertices == null) { return false; }
        if (cachedTriangles == null) { return false; }

        Vector3 localPoint = transform.InverseTransformPoint(position);

        // quick bounds check first because triangle checks are more expensive
        if (!cachedBounds.Contains(localPoint))
        {
            return false;
        }

        // just need to choose a direction, doesnt really matter but may hit triangle edges so just slightly off should do
        Vector3 rayDirection = insideCheckDirection;

        // small offset so we don't start on a surface
        Vector3 rayOrigin = localPoint + (rayDirection * insideCheckOffset);

        if (drawRay) { Debug.DrawRay(transform.TransformPoint(rayOrigin), transform.TransformDirection(rayDirection)); }

        int hitCount = 0;

        for (int i = 0; i < cachedTriangles.Length; i += 3)
        {
            Vector3 a = cachedVertices[cachedTriangles[i]];
            Vector3 b = cachedVertices[cachedTriangles[i + 1]];
            Vector3 c = cachedVertices[cachedTriangles[i + 2]];

            if (RayIntersectsTriangle(rayOrigin, rayDirection, a, b, c))
            {
                hitCount++;
            }
        }

        // odd number of hits means inside
        return (hitCount & 1) == 1;
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

    private void CacheMeshData()
    {
        cachedMesh = GetVolumeMesh();

        if (cachedMesh == null)
        {
            cachedVertices = null;
            cachedTriangles = null;
            cachedBounds = default;
            cachedGravityDirection = Vector3.down;
            return;
        }

        cachedVertices = cachedMesh.vertices;
        cachedTriangles = cachedMesh.triangles;
        cachedBounds = cachedMesh.bounds;

        cachedGravityDirection = gravityDirection.sqrMagnitude > 0.0001f ? gravityDirection.normalized : Vector3.down;
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