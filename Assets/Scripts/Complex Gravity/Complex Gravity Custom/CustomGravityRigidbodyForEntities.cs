using UnityEngine;

// Made by: Jason Lodge
// Original by: CatLikeCoding Available at:https://catlikecoding.com/unity/tutorials/movement/complex-gravity/
// Summary: Script that adds new functionality to CustomGravityRigidbody.
// Adds a Grounded functionality using multiple raycasts (for slopes).
// For use by characters/entities.

public class CustomGravityRigidbodyForEntities : CustomGravityRigidbody
{
    [Header("Gravity Reference")]
    [Tooltip("Transform to use for gravity sampling instead of the rigidbody position.")]
    [SerializeField] private Transform gravitySampleObj;

    [Header("Grounded Check")]
    [SerializeField] private bool useGroundedCheck = true;

    [Tooltip("Object used as the grounded check origin.")]
    [SerializeField] private Transform groundedCheckObj;

    [Tooltip("Transform used for forward/right raycast orientation. If null, uses groundedCheckObj, then this transform.")]
    [SerializeField] private Transform groundedOrientationObj;

    [SerializeField] private float groundedRayLength = 0.75f;
    [SerializeField] private float groundedRayRadius = 0.2f;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float zeroGravityThreshold = 0.25f;

    [Header("Ground Snap")]
    [Tooltip("How far above the ground point the grounded check should rest.")]
    [SerializeField] private float groundedHoverHeight = 1f;

    [Tooltip("Grace time before we fully lose grounded state.")]
    [SerializeField] private float groundedLoseGraceTime = 0.08f;

    [Tooltip("If true, while grounded we remove any velocity going towards ground.")]
    [SerializeField] private bool stopDownwardVelocityWhenGrounded = true;

    [Tooltip("How quickly we correct hover height while grounded.")]
    [SerializeField] private float groundedSnapSharpness = 18f;

    [Tooltip("Maximum snap speed while correcting hover height.")]
    [SerializeField] private float groundedSnapMaxSpeed = 6f;

    [Tooltip("Small dead zone around hover height to stop tiny corrections / pogoing.")]
    [SerializeField] private float groundedSnapDeadZone = 0.02f;

    public bool Grounded => grounded;
    public Vector3 UpAxis => upAxis;
    public Vector3 CurrentGravity => currentGravity;
    public bool HasGravity => currentGravity.magnitude > zeroGravityThreshold;
    public LayerMask GroundLayers => groundLayers;
    public Vector3 GroundNormal => hasGroundHit ? groundHit.normal : upAxis;
    public float GroundDistance => hasGroundHit ? groundHit.distance : float.PositiveInfinity;

    private float groundedIgnoreTimer;
    private float groundedLoseTimer;

    private bool grounded;
    private Vector3 upAxis = Vector3.up;
    private Vector3 currentGravity;

    private RaycastHit groundHit;
    private bool hasGroundHit;

    public void IgnoreGrounding(float duration)
    {
        groundedIgnoreTimer = Mathf.Max(groundedIgnoreTimer, duration);
        grounded = false;
        hasGroundHit = false;
        groundedLoseTimer = 0f;
    }

    protected override void FixedUpdate()
    {
        if (useGravity)
        {
            Vector3 gravitySamplePosition = GetGravitySamplePosition();
            currentGravity = CustomGravity.GetGravity(gravitySamplePosition, out upAxis);

            if (groundedIgnoreTimer > 0f)
            {
                groundedIgnoreTimer -= Time.fixedDeltaTime;
            }

            UpdateGroundedState();

            if (grounded)
            {
                if (stopDownwardVelocityWhenGrounded)
                {
                    StopGroundPushThroughVelocity();
                }

                SnapBodyToGround();
            }

            if (HandleFloatToSleep()) { return; }

            // only apply gravity if we actually have usable gravity and are not grounded
            if (!grounded && currentGravity.magnitude > 0.0001f)
            {
                body.AddForce(currentGravity, ForceMode.Acceleration);
            }
        }
    }

    private Vector3 GetGravitySamplePosition()
    {
        if (gravitySampleObj != null)
        {
            return gravitySampleObj.position;
        }

        if (groundedCheckObj != null)
        {
            return groundedCheckObj.position;
        }

        return body.position;
    }

    private Transform GetGroundOrientationTransform()
    {
        if (groundedOrientationObj != null)
        {
            return groundedOrientationObj;
        }

        if (groundedCheckObj != null)
        {
            return groundedCheckObj;
        }

        return transform;
    }

    private void UpdateGroundedState()
    {
        hasGroundHit = false;

        if (!useGroundedCheck)
        {
            grounded = false;
            groundedLoseTimer = 0f;
            return;
        }

        if (groundedCheckObj == null)
        {
            grounded = false;
            groundedLoseTimer = 0f;
            return;
        }

        if (groundedIgnoreTimer > 0f)
        {
            grounded = false;
            groundedLoseTimer = 0f;
            return;
        }

        if (currentGravity.magnitude <= zeroGravityThreshold)
        {
            grounded = false;
            groundedLoseTimer = 0f;
            return;
        }

        bool foundHit = FindBestGroundHit(groundedRayLength, out groundHit);

        if (foundHit)
        {
            hasGroundHit = true;
            grounded = true;
            groundedLoseTimer = groundedLoseGraceTime;
            return;
        }

        if (groundedLoseTimer > 0f)
        {
            groundedLoseTimer -= Time.fixedDeltaTime;
            grounded = true;
            return;
        }

        grounded = false;
    }

    private bool FindBestGroundHit(float rayLength, out RaycastHit bestHit)
    {
        bestHit = default;

        Vector3 origin = groundedCheckObj.position;
        Vector3 down = -upAxis;

        Transform orientation = GetGroundOrientationTransform();

        Vector3 forward = Vector3.ProjectOnPlane(orientation.forward, upAxis);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(orientation.up, upAxis);
        }
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, upAxis);
        }
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(upAxis, forward);
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        right.Normalize();

        float offsetDist = groundedRayRadius;

        // using multiple ray casts because I had a lot of trouble getting a sphere check to accurately check ground hit for slopes
        Vector3[] origins = new Vector3[5];
        origins[0] = origin;
        origins[1] = origin + (forward * offsetDist);
        origins[2] = origin - (forward * offsetDist);
        origins[3] = origin + (right * offsetDist);
        origins[4] = origin - (right * offsetDist);

        bool foundAny = false;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < origins.Length; i++)
        {
            RaycastHit hit;
            if (Physics.Raycast(
                origins[i],
                down,
                out hit,
                rayLength,
                groundLayers,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    foundAny = true;
                }
            }
        }

        return foundAny;
    }

    private void StopGroundPushThroughVelocity()
    {
        Vector3 vel = body.linearVelocity;
        Vector3 supportNormal = hasGroundHit ? groundHit.normal : upAxis;

        // split velocity into support normal and planar parts
        Vector3 normalVel = Vector3.Project(vel, supportNormal);
        Vector3 planarVel = vel - normalVel;

        // if moving into the ground, remove that part
        float normalSpeed = Vector3.Dot(normalVel, supportNormal);
        if (normalSpeed < 0f)
        {
            body.linearVelocity = planarVel;
        }
    }

    private void SnapBodyToGround()
    {
        if (!hasGroundHit) { return; }
        if (groundedCheckObj == null) { return; }

        Vector3 supportNormal = groundHit.normal;

        // grounded check should rest this far above the surface along the actual ground normal
        Vector3 targetCheckPos = groundHit.point + (supportNormal * groundedHoverHeight);

        // move body so grounded check reaches target
        Vector3 deltaToTarget = targetCheckPos - groundedCheckObj.position;

        // only correct along the support normal so curved surfaces stay stable
        float alongNormal = Vector3.Dot(deltaToTarget, supportNormal);

        // dead zone stops tiny constant corrections / pogoing
        if (Mathf.Abs(alongNormal) <= groundedSnapDeadZone) { return; }

        float t = 1f - Mathf.Exp(-groundedSnapSharpness * Time.fixedDeltaTime);
        float snapDelta = alongNormal * t;

        float maxStep = groundedSnapMaxSpeed * Time.fixedDeltaTime;
        snapDelta = Mathf.Clamp(snapDelta, -maxStep, maxStep);

        Vector3 moveDelta = supportNormal * snapDelta;
        body.position += moveDelta;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundedCheckObj == null) { return; }

        Vector3 origin = groundedCheckObj.position;
        Vector3 gizmoUp = upAxis.sqrMagnitude > 0.0001f ? upAxis.normalized : transform.up;
        Vector3 down = -gizmoUp;

        Transform orientation = groundedOrientationObj != null ? groundedOrientationObj : groundedCheckObj;

        Vector3 forward = Vector3.ProjectOnPlane(orientation.forward, gizmoUp);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(orientation.up, gizmoUp);
        }
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(gizmoUp, forward);
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        right.Normalize();

        Vector3[] origins = new Vector3[5];
        origins[0] = origin;
        origins[1] = origin + (forward * groundedRayRadius);
        origins[2] = origin - (forward * groundedRayRadius);
        origins[3] = origin + (right * groundedRayRadius);
        origins[4] = origin - (right * groundedRayRadius);

        Gizmos.color = grounded ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
        for (int i = 0; i < origins.Length; i++)
        {
            Gizmos.DrawSphere(origins[i], 0.025f);
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < origins.Length; i++)
        {
            Gizmos.DrawLine(origins[i], origins[i] + (down * groundedRayLength));
        }

        if (hasGroundHit)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(groundHit.point, 0.03f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(groundHit.point + (groundHit.normal * groundedHoverHeight), 0.03f);
        }
    }
}