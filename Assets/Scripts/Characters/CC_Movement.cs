using UnityEngine;

// Made by: Jason Lodge
// Summary: Movement and physics handler.
// Notes:
// - This owns locomotion mode, grounded, sprint/crouch state, and applies velocity/forces for movement.

[RequireComponent(typeof(Rigidbody))]
public class CC_Movement : MonoBehaviour
{
    public enum LocomotionType { GroundMode, SpaceMode }

    public LocomotionType locomotionType = LocomotionType.GroundMode;

    [Header("References")]
    public CC_CharacterValues values;

    [Tooltip("Camera target / view reference used for movement direction.")]
    public Transform viewTransform;

    [Header("Player Movement")]
    public float moveSpeed = 5f;
    public float accelerationRate = 12f;

    [Header("Sprint")]
    public float sprintSpeedMult = 1.5f;

    [Header("Crouch")]
    public float crouchSpeedMult = 0.5f;

    [Header("Jump / Gravity")]
    public float jumpPower = 10f;

    [Header("Space Thrusters")]
    public float thrusterAccel = 4f;

    [Header("Gravity Detection")]
    public float zeroGravityThreshold = 0.25f;

    [Header("Grounded Check")]
    public bool grounded = true;
    public Transform groundedCheckObj;
    public float groundedRadius = 0.5f;
    public LayerMask groundLayers;

    public float jumpTimeout = 0.1f;
    public float fallTimeout = 0.15f;

    // state exposed for other scripts
    public Rigidbody RB => rb;
    public Vector3 UpAxis => upAxis;
    public Vector3 CurrentGravity => currentGravity;
    public bool JumpedThisTick => jumpedThisTick;
    public bool Sprinting => sprinting;
    public bool Crouching => crouching;

    // runtime binding (body is spawned after awake)
    public void BindValues(CC_CharacterValues v) { values = v; }
    public void BindGroundedCheck(Transform t) { groundedCheckObj = t; }
    public void BindViewTransform(Transform t) { viewTransform = t; }

    // internals
    Rigidbody rb;

    Vector3 upAxis;
    Vector3 currentGravity;

    float jumpTimeoutDelta;
    float fallTimeoutDelta;
    bool jumpedThisTick;

    bool sprinting;
    bool crouching;

    protected virtual void Awake()
    {
        if (values == null) { values = GetComponent<CC_CharacterValues>(); }

        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        jumpTimeoutDelta = jumpTimeout;
        fallTimeoutDelta = fallTimeout;
    }

    // Call in FixedUpdate.
    public virtual void TickFixed(Vector2 moveInput, bool jumpInput, bool sprintInput, bool crouchInput)
    {
        // keep our up axis updated from gravity
        currentGravity = CustomGravity.GetGravity(rb.position, out upAxis);

        UpdateLocomotionMode();

        // grounded only matters in ground mode
        if (locomotionType == LocomotionType.GroundMode) { GroundedCheck(); }
        else { grounded = false; }

        // clear each tick
        jumpedThisTick = false;

        // crouch only in ground mode + grounded
        bool canCrouch = locomotionType == LocomotionType.GroundMode && grounded;

        // crouch wins over sprint
        crouching = crouchInput && canCrouch;
        sprinting = sprintInput && !crouching;

        // if no stamina, no sprint
        if (values != null && sprinting && !values.HasStamina())
        {
            sprinting = false;
        }

        // movement is always applied to the ball
        if (locomotionType == LocomotionType.SpaceMode)
        {
            SpaceThrusters(moveInput, jumpInput, crouchInput);
        }
        else
        {
            GroundMove(moveInput);
            Jump(jumpInput);
        }
    }

    // Call in LateUpdate.
    public virtual void TickLate()
    {
        if (values == null) { return; }

        // only drain stamina when sprinting in ground mode and grounded
        bool shouldDrain = sprinting && locomotionType == LocomotionType.GroundMode && grounded;

        // only regen stamina when grounded in ground mode
        bool allowRegen = locomotionType == LocomotionType.GroundMode && grounded;

        values.TickStamina(shouldDrain, allowRegen);

        // if we run out of stamina, force sprint off
        if (sprinting && !values.HasStamina())
        {
            sprinting = false;
        }
    }

    protected virtual void UpdateLocomotionMode()
    {
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
        // can't ground unless jump time out is over
        if (jumpTimeoutDelta > 0f) { grounded = false; return; }

        if (groundedCheckObj == null) { grounded = false; return; }

        Vector3 spherePosition = groundedCheckObj.position;
        grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
    }

    protected virtual void GroundMove(Vector2 moveInput)
    {
        if (viewTransform == null) { return; }

        // current velocity split
        Vector3 vel = rb.linearVelocity;
        Vector3 planarVel = Vector3.ProjectOnPlane(vel, upAxis);
        Vector3 verticalVel = vel - planarVel;

        if (grounded)
        {
            verticalVel = Vector3.zero;
        }

        // when in air in ground mode, dont allow air movement
        if (!grounded)
        {
            rb.linearVelocity = planarVel + verticalVel;
            return;
        }

        // build desired direction relative to view, projected on surface plane
        Vector3 camForward = Vector3.ProjectOnPlane(viewTransform.forward, upAxis);
        Vector3 camRight = Vector3.ProjectOnPlane(viewTransform.right, upAxis);

        // if camera is looking almost straight up/down, projection can get tiny, so fallback
        if (camForward.sqrMagnitude < 0.0001f) { camForward = Vector3.ProjectOnPlane(transform.forward, upAxis); }
        if (camRight.sqrMagnitude < 0.0001f) { camRight = Vector3.ProjectOnPlane(transform.right, upAxis); }

        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camRight * moveInput.x + camForward * moveInput.y);
        if (moveDir.sqrMagnitude > 1f) { moveDir.Normalize(); }

        // grounded movement and friction
        if (moveInput == Vector2.zero)
        {
            float t = 1f - Mathf.Exp(-accelerationRate * Time.fixedDeltaTime);
            planarVel = Vector3.Lerp(planarVel, Vector3.zero, t);
            rb.linearVelocity = planarVel + verticalVel;
            return;
        }

        float speedMult = 1f;
        if (sprinting) { speedMult *= sprintSpeedMult; }
        if (crouching) { speedMult *= crouchSpeedMult; }

        Vector3 targetPlanarVel = moveDir * (moveSpeed * speedMult);
        float accelT = 1f - Mathf.Exp(-accelerationRate * Time.fixedDeltaTime);
        Vector3 newPlanarVel = Vector3.Lerp(planarVel, targetPlanarVel, accelT);

        rb.linearVelocity = newPlanarVel + verticalVel;
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
                jumpTimeoutDelta = jumpTimeout;

                // when jumping stop crouching
                crouching = false;
                jumpedThisTick = true;
            }
        }

        // timers
        if (grounded)
        {
            if (jumpTimeoutDelta > 0f) { jumpTimeoutDelta -= Time.deltaTime; }
        }
        else
        {
            jumpTimeoutDelta = Mathf.Max(0f, jumpTimeoutDelta - Time.deltaTime);
            if (fallTimeoutDelta > 0f) { fallTimeoutDelta -= Time.deltaTime; }
        }
    }

    protected virtual void SpaceThrusters(Vector2 moveInput, bool jumpPressed, bool crouchPressed)
    {
        // add force as thrusters would have inertia

        if (viewTransform == null) { return; }

        Vector3 forward = viewTransform.forward;
        Vector3 right = viewTransform.right;
        Vector3 up = viewTransform.up;

        Vector3 accel = (right * moveInput.x + forward * moveInput.y);

        if (jumpPressed) { accel += up; }
        if (crouchPressed) { accel -= up; }

        if (accel.sqrMagnitude > 1f) { accel.Normalize(); }

        float speedMult = 1f;
        if (sprinting) { speedMult *= sprintSpeedMult; }

        rb.AddForce(accel * (thrusterAccel * speedMult), ForceMode.Acceleration);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Color col = grounded ? new Color(0, 1, 0, 0.35f) : new Color(1, 0, 0, 0.35f);
        Gizmos.color = col;

        if (groundedCheckObj != null)
        {
            Gizmos.DrawSphere(groundedCheckObj.position, groundedRadius);
        }
    }
}
