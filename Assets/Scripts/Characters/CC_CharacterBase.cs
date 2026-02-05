using UnityEngine;

// Made by: Jason Lodge
// Summary: Holds all shared logic between all characters, player included.
// That is movement, Values like health and levels, (will have more as i figure it out) etc.

[RequireComponent(typeof(Rigidbody))]
public class CC_CharacterBase : MonoBehaviour
{
    public enum LocomotionType { GroundMode, SpaceMode }

    public LocomotionType locomotionType = LocomotionType.GroundMode;

    [Header("Player Movement")]
    public float moveSpeed = 5f;
    public float accelerationRate = 12f;

    [Header("Jump / Gravity")]
    public float jumpPower = 10f;

    [Header("Surface Stabalisation")]
    public float torqueStrength = 5f;

    [Tooltip("How far to check for a surface to start ramping stabilisation.")]
    public float stabiliseRange = 10f;

    [Tooltip("Extra buffer so we start stabilising slightly before contact.")]
    public float stabilisePadding = 0.25f;

    [Tooltip("Curve mapping proximity 0-1 to stabilisation multiplier.")]
    public AnimationCurve stabiliseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Maximum multiplier applied to torqueStrength (final = torqueStrength * curve * max).")]
    public float stabiliseMaxMult = 1f;

    [Header("Space Thrusters")]
    public float thrusterAccel = 4f;
    public float spaceTurnSpeed = 30f;

    [Header("Space Rotation")]
    public float rollMaxSpeed = 30f;
    public float rollAccel = 4f;
    public float rollDamping = 2f;


    [Header("Gravity Detection")]
    public float zeroGravityThreshold = 0.25f;

    [Header("Grounded Check")]
    public bool grounded = true;
    public Transform groundedCheckObj;
    public float groundedRadius = 0.5f;
    public LayerMask groundLayers;

    public float jumpTimeout = 0.1f;
    public float fallTimeout = 0.15f;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine camera that the camera will follow")]
    public Transform cinemachineCameraTarget;

    [Tooltip("How far in degrees you can move the camera up")]
    public float topClamp = 90.0f;

    [Tooltip("How far in degrees you can move the camera down")]
    public float bottomClamp = -90.0f;

    [Header("Look Sensitivity")]
    [Tooltip("Overall horizontal multiplier (applies to mouse + stick).")]
    public float lookMultX = 1f;

    [Tooltip("Overall vertical multiplier (applies to mouse + stick).")]
    public float lookMultY = 1f;

    [Tooltip("Mouse base sensitivity")]
    public float mouseBaseX = 0.10f;
    public float mouseBaseY = 0.10f;

    [Tooltip("Stick base sensitivity")]
    public float stickBaseX = 180f;
    public float stickBaseY = 180f;

    public bool invertY = false;

    // cinemachine
    protected float cinemachineTargetPitch;

    // internals
    protected Rigidbody rb;
    protected float verticalVelocity;
    protected float yaw;
    protected float jumpTimeoutDelta;
    protected float fallTimeoutDelta;
    protected Vector2 pendingLook;
    protected float pendingRoll;
    protected float rollSpeed;


    // keep velocity as a desired and smooth towards it
    protected Vector3 desiredPlanarVelocity;

    Vector3 upAxis;
    Vector3 currentGravity;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        jumpTimeoutDelta = jumpTimeout;
        fallTimeoutDelta = fallTimeout;
    }

    // Call in FixedUpdate (physics)
    public virtual void TickMotorFixed(Vector2 moveInput, bool jumpInput, float rollInput)
    {
        // keep our up axis updated from gravity
        currentGravity = CustomGravity.GetGravity(rb.position, out upAxis);

        UpdateLocomotionMode();

        pendingRoll = -rollInput;

        // rotation
        if (locomotionType == LocomotionType.SpaceMode)
        {
            SpaceRotation();
        }
        else
        {
            ApplyYawRotation(); // do yaw in physics so it behaves properly with rigidbody + gravity up
            TorqueStabalisation();
        }

        // grounded should only matter in ground mode
        if (locomotionType == LocomotionType.GroundMode) { GroundedCheck(); }
        else { grounded = false; }

        // movement
        if (locomotionType == LocomotionType.SpaceMode) { SpaceThrusters(moveInput, jumpInput); }
        else { MoveAndGravity(moveInput); Jump(jumpInput); }

        // clear pending look after we use it in physics
        pendingLook = Vector2.zero;
    }

    // Call in LateUpdate (camera).
    public virtual void TickCameraLate(Vector2 lookInput, bool isMouse)
    {
        CameraRotation(lookInput, isMouse);
    }

    protected virtual void UpdateLocomotionMode()
    {
        // if we are not inside any sources (or all sources return near-zero), swap to space mode
        float gMag = currentGravity.magnitude;

        if (gMag <= zeroGravityThreshold)
        {
            locomotionType = LocomotionType.SpaceMode;
            return;
        }

        locomotionType = LocomotionType.GroundMode;
    }

    protected virtual void GroundedCheck()
    {
        if (jumpTimeoutDelta <= 0) { return; } // can't ground unless jump time out is over
        Vector3 spherePosition = groundedCheckObj.position;
        grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
    }

    protected virtual void CameraRotation(Vector2 look, bool isMouse)
    {
        const float threshold = 0.0001f;
        if (look.sqrMagnitude < threshold || cinemachineCameraTarget == null) { return; }

        float dt = isMouse ? 1f : Time.deltaTime;

        float baseX = isMouse ? mouseBaseX : stickBaseX;
        float baseY = isMouse ? mouseBaseY : stickBaseY;

        // overall multipliers
        float sx = baseX * lookMultX;
        float sy = baseY * lookMultY;

        float lookX = look.x * sx * dt;
        float lookY = look.y * sy * dt;

        if (invertY) { lookY = -lookY; }

        // store for fixed update
        pendingLook += new Vector2(lookX, lookY);

        // pitch always affects camera target first
        float prevPitch = cinemachineTargetPitch;
        cinemachineTargetPitch -= lookY;
        cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);
        cinemachineCameraTarget.localRotation = Quaternion.Euler(cinemachineTargetPitch, 0f, 0f);

        // ground yaw uses gravity up axis
        if (locomotionType == LocomotionType.GroundMode)
        {
            yaw += lookX;
        }
    }

    protected virtual void ApplyYawRotation()
    {
        if (Mathf.Abs(yaw) < 0.0001f) { return; }

        // rotate around the current surface up
        Quaternion delta = Quaternion.AngleAxis(yaw, upAxis);
        rb.MoveRotation(delta * rb.rotation);

        yaw = 0f;
    }

    protected virtual void TorqueStabalisation()
    {
        if (locomotionType == LocomotionType.SpaceMode) { return; }

        // check how close we are to the ground along current gravity direction
        float proximity01 = 0f;

        if (grounded) // we're close enough
        {
            proximity01 = 1f;
        }
        else // shoot ray to check how close we are
        {
            float range = Mathf.Max(0.01f, stabiliseRange);
            float castDist = range + stabilisePadding;

            Ray ray = new Ray(transform.position, -upAxis);
            Debug.DrawRay(transform.position, -upAxis * castDist, Color.red);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, castDist, groundLayers, QueryTriggerInteraction.Ignore))
            {
                // map distance to proximity
                float d = Mathf.Clamp(hit.distance - stabilisePadding, 0f, range);
                proximity01 = 1f - (d / range);
            }
        }

        // run through curve and scale to max
        float curve = stabiliseCurve != null ? stabiliseCurve.Evaluate(proximity01) : proximity01;
        float strength = torqueStrength * stabiliseMaxMult * Mathf.Clamp01(curve);

        if (strength <= 0.0001f) { return; }

        // align our current up to the gravity up axis
        Quaternion current = rb.rotation;

        // rotate our current up onto the surface up
        Quaternion toUp = Quaternion.FromToRotation(current * Vector3.up, upAxis);
        Quaternion target = toUp * current;

        // smooth it
        float t = 1f - Mathf.Exp(-strength * Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.Slerp(current, target, t));
    }
    protected virtual void MoveAndGravity(Vector2 moveInput)
    {
        // camera-based planar axes
        Vector3 camForward = Vector3.ProjectOnPlane(cinemachineCameraTarget.forward, upAxis);
        Vector3 camRight = Vector3.ProjectOnPlane(cinemachineCameraTarget.right, upAxis);

        // if camera is looking almost straight up/down, projection can get tiny, so fallback
        if (camForward.sqrMagnitude < 0.0001f) { camForward = Vector3.ProjectOnPlane(transform.forward, upAxis); }
        if (camRight.sqrMagnitude < 0.0001f) { camRight = Vector3.ProjectOnPlane(transform.right, upAxis); }

        camForward.Normalize();
        camRight.Normalize();

        // build movement direction from camera planar axes
        Vector3 moveDir = (camRight * moveInput.x + camForward * moveInput.y);
        if (moveDir.sqrMagnitude > 1f) { moveDir.Normalize(); }

        // current velocity from Rigidbody
        Vector3 current = rb.linearVelocity;

        // split into planar (along surface) and vertical (along gravity axis)
        Vector3 currentPlanarVel = Vector3.ProjectOnPlane(current, upAxis);
        Vector3 currentVerticalVel = current - currentPlanarVel;

        // if no input, dont force planar to zero unless grounded
        if (moveInput == Vector2.zero)
        {
            if (grounded)
            {
                float t = 1f - Mathf.Exp(-accelerationRate * Time.fixedDeltaTime);
                desiredPlanarVelocity = Vector3.Lerp(currentPlanarVel, Vector3.zero, t);
                rb.linearVelocity = desiredPlanarVelocity + currentVerticalVel;
                return;
            }

            rb.linearVelocity = currentPlanarVel + currentVerticalVel;
            return;
        }

        // calculate target planar velocity
        Vector3 targetPlanarVel = moveDir * moveSpeed;

        // smooth toward the target planar velocity
        desiredPlanarVelocity = Vector3.Lerp(currentPlanarVel, targetPlanarVel, accelerationRate * Time.fixedDeltaTime);

        if (grounded && jumpTimeoutDelta <= 0f)
        {
            currentVerticalVel = Vector3.zero;
        }

        // keep vertical from gravity/jump, apply smoothed planar
        rb.linearVelocity = desiredPlanarVelocity + currentVerticalVel;
    }

    protected virtual void Jump(bool jumpPressed)
    {
        if (grounded)
        {
            fallTimeoutDelta = fallTimeout;

            if (jumpPressed && jumpTimeoutDelta <= 0f)
            {
                // jump along current up axis so it works on walls/ceilings
                rb.AddForce(upAxis * jumpPower, ForceMode.Impulse);
                grounded = false;
            }

            if (jumpTimeoutDelta > 0f) { jumpTimeoutDelta -= Time.deltaTime; }
        }
        else
        {
            jumpTimeoutDelta = jumpTimeout;
            if (fallTimeoutDelta > 0f) { fallTimeoutDelta -= Time.deltaTime; }
        }
    }

    protected virtual void SpaceThrusters(Vector2 moveInput, bool jumpPressed)
    {
        // add force as thrusters would have inertia
        // movement is relative to camera orientation

        if (cinemachineCameraTarget == null) { return; }

        Vector3 forward = cinemachineCameraTarget.forward;
        Vector3 right = cinemachineCameraTarget.right;

        Vector3 accel = (right * moveInput.x + forward * moveInput.y);

        if (jumpPressed)
        {
            accel += cinemachineCameraTarget.up;
        }

        if (accel.sqrMagnitude > 1f) { accel.Normalize(); }

        rb.AddForce(accel * thrusterAccel, ForceMode.Acceleration);
    }

    protected virtual void SpaceRotation()
    {
        Quaternion current = rb.rotation;

        float dt = Time.fixedDeltaTime;

        Vector3 camUp = cinemachineCameraTarget.up;
        Vector3 camRight = cinemachineCameraTarget.right;
        Vector3 camForward = cinemachineCameraTarget.forward;
        camUp.Normalize();
        camRight.Normalize();
        camForward.Normalize();

        // yaw/pitch
        float yawDelta = pendingLook.x * (spaceTurnSpeed / 180f) * dt * 180f;
        float pitchDelta = -pendingLook.y * (spaceTurnSpeed / 180f) * dt * 180f;

        Quaternion yawQ = Quaternion.AngleAxis(yawDelta, camUp);
        Quaternion pitchQ = Quaternion.AngleAxis(pitchDelta, camRight);

        // accelerate to target roll speed
        float targetRollSpeed = pendingRoll * rollMaxSpeed;

        // accelerate toward target
        rollSpeed = Mathf.MoveTowards(rollSpeed, targetRollSpeed, rollAccel * rollMaxSpeed * dt);

        // when no input, damp the roll back down
        if (Mathf.Abs(pendingRoll) < 0.001f)
        {
            rollSpeed = Mathf.MoveTowards(rollSpeed, 0f, rollDamping * rollMaxSpeed * dt);
        }

        float rollDelta = rollSpeed * dt;
        Quaternion rollQ = Quaternion.AngleAxis(rollDelta, camForward);

        // apply
        rb.MoveRotation(rollQ * pitchQ * yawQ * current);
    }

    protected static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) { angle += 360f; }
        if (angle > 360f) { angle -= 360f; }

        return Mathf.Clamp(angle, min, max);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Color col = grounded ? new Color(0, 1, 0, 0.35f) : new Color(1, 0, 0, 0.35f);
        Gizmos.color = col;
        Gizmos.DrawSphere(groundedCheckObj.position, groundedRadius);
    }
}
