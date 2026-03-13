using UnityEngine;

public class CustomGravityRigidbodyForEntities : CustomGravityRigidbody
{
    [Header("Grounded Check")]
    [SerializeField]
    private bool useGroundedCheck = true;

    [SerializeField]
    private Transform groundedCheckObj;

    [SerializeField]
    private float groundedRayLength = 0.75f;

    [SerializeField]
    private float groundedRayRadius = 0.2f;

    [SerializeField]
    private LayerMask groundLayers;

    [SerializeField]
    private float zeroGravityThreshold = 0.25f;

    [Header("Ground Snap")]
    [Tooltip("How far above the ground point the grounded check should rest.")]
    [SerializeField]
    private float groundedHoverHeight = 1f;

    [Tooltip("Grace time before we fully lose grounded state.")]
    [SerializeField]
    private float groundedLoseGraceTime = 0.08f;

    [Tooltip("If true, while grounded we remove any velocity going towards ground.")]
    [SerializeField]
    private bool stopDownwardVelocityWhenGrounded = true;

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
        currentGravity = CustomGravity.GetGravity(body.position, out upAxis);

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

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, upAxis);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.up, upAxis);
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

        // split velocity into up axis and planar parts
        Vector3 axisVel = Vector3.Project(vel, upAxis);
        Vector3 planarVel = vel - axisVel;

        // if moving opposite up axis, we are moving into the ground
        float axisSpeed = Vector3.Dot(axisVel, upAxis);
        if (axisSpeed < 0f)
        {
            body.linearVelocity = planarVel;
        }
    }

    private void SnapBodyToGround()
    {
        if (!hasGroundHit) { return; }
        if (groundedCheckObj == null) { return; }

        // grounded check should rest this far above the surface
        Vector3 targetCheckPos = groundHit.point + (upAxis * groundedHoverHeight);

        // move body so grounded check reaches target exactly
        Vector3 deltaToTarget = targetCheckPos - groundedCheckObj.position;

        // only move along up axis
        float alongUp = Vector3.Dot(deltaToTarget, upAxis);
        Vector3 moveDelta = upAxis * alongUp;

        if (moveDelta.sqrMagnitude <= 0.00000001f) { return; }

        body.position += moveDelta;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundedCheckObj == null) { return; }

        Vector3 origin = groundedCheckObj.position;
        Vector3 up = transform.up;
        Vector3 down = -up;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(up, forward).normalized;

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
            Gizmos.DrawSphere(groundHit.point + (upAxis * groundedHoverHeight), 0.03f);
        }
    }
}