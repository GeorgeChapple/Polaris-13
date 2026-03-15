using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

// Made by: Jason Lodge
// Summary: Handles locomotion, gravity, jumping, body rotation and stamina / thruster usage.

[RequireComponent(typeof(Rigidbody))]
public class CC_Movement : NetworkBehaviour
{
    public enum LocomotionType { GroundMode, SpaceMode }

    public LocomotionType locomotionType = LocomotionType.GroundMode;

    [Header("References")]
    public CC_CharacterValues values;
    public CC_CameraController cameraController;

    [Header("Gravity")]
    [Tooltip("Custom gravity component on the same object as this.")]
    public CustomGravityRigidbodyForEntities customGravityBody;

    [Header("Player Movement")]
    public float moveSpeed = 5f;
    public float accelerationRate = 12f;

    [Header("Sprint")]
    public float sprintSpeedMult = 1.5f;

    [Header("Crouch")]
    public float crouchSpeedMult = 0.5f;

    [Tooltip("Capsule collider on the body. Changes size when crouched.")]
    public CapsuleCollider bodyCapsule;

    [Tooltip("Capsule height multiplier while crouched.")]
    public float crouchCapsuleHeightMult = 0.6f;

    [Header("Jump / Gravity")]
    public float jumpPower = 10f;

    [Header("Ground Thrusters")]
    [Tooltip("Acceleration applied upward while holding jump in ground mode and airborne.")]
    public float groundThrusterAccel = 8f;

    [Tooltip("Maximum upward speed the ground thrusters will push towards.")]
    public float groundThrusterUpSpeedCap = 8f;

    [Tooltip("Thruster drain per second while using airborne ground thrusters.")]
    public float groundThrusterDrainPerSecond = 15f;

    [Header("Ground Air Control")]
    [Tooltip("Allow horizontal airborne movement in ground mode using the space thruster settings.")]
    [Range(0f, 1f)] public float airborneGroundControl = 1f;

    [Header("Surface Stabalisation")]
    public float torqueStrength = 5f;

    [Tooltip("How much stronger stabilisation is while grounded.")]
    public float groundedTorqueStrength = 20f;

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

    [Tooltip("Thruster drain per second while using space movement thrusters.")]
    public float spaceThrusterDrainPerSecond = 8f;

    [Header("Space Stabilisation")]
    [Tooltip("Acceleration applied opposite to current velocity while stabilising in space.")]
    public float spaceStabiliseAccel = 8f;

    [Tooltip("Thruster drain per second while using space stabilisation.")]
    public float spaceStabiliseDrainPerSecond = 4f;

    [Header("Space Rotation")]
    public float spaceTurnSpeed = 30f;
    public float rollMaxSpeed = 30f;
    public float rollAccel = 4f;
    public float rollDamping = 2f;

    [Header("Gravity Detection")]
    public float zeroGravityThreshold = 0.25f;

    [Header("Grounded")]
    public bool grounded = true;
    public float jumpTimeout = 0.1f;
    public float fallTimeout = 0.15f;

    [Header("Network Ownership")]
    [Tooltip("If True, will work without network manager/ownership interference.")]
    [SerializeField] private bool singlePlayer = false;

    [SerializeField] private TextMeshProUGUI playerText;

    // internals
    protected Rigidbody rb;
    protected float jumpTimeoutDelta;
    protected float fallTimeoutDelta;

    // sprint/crouch state
    protected bool sprinting;
    protected bool crouching;
    protected bool jumpedThisTick;

    // look
    protected Vector2 pendingLook;
    protected float pendingRoll;
    protected float rollSpeed;

    // body orientation state
    protected Quaternion bodyRotation = Quaternion.identity;
    protected Vector3 groundedForward = Vector3.forward;

    // gravity
    protected Vector3 upAxis = Vector3.up;
    protected Vector3 currentGravity;

    // crouch state
    float capsuleBaseHeight;
    Vector3 capsuleBaseCenter;

    // thrusters
    bool usingGroundThrusters;
    bool usingSpaceMoveThrusters;
    bool usingSpaceStabiliseThrusters;
    float thrusterDrainPerSecondThisTick;

    bool initialised;

    // public read for other components like camera/interaction
    public Rigidbody Body => rb;
    public Vector3 UpAxis => upAxis;
    public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
    public bool IsGrounded => grounded;
    public bool IsSprinting => sprinting;
    public bool IsCrouching => crouching;
    public bool JumpedThisTick => jumpedThisTick;
    public bool SinglePlayer => singlePlayer;
    public Transform CameraTarget => cameraController != null ? cameraController.cinemachineCameraTarget : null;

    protected virtual void Awake()
    {
        InitialiseComponents();

        // only do player text immediately in single player mode.
        if (singlePlayer && playerText != null)
        {
            playerText.text = "Player";
        }

        StartCoroutine(WaitSpawn(true));
    }

    void InitialiseComponents()
    {
        if (initialised) { return; }
        initialised = true;

        if (values == null) { values = GetComponent<CC_CharacterValues>(); }
        if (customGravityBody == null) { customGravityBody = GetComponent<CustomGravityRigidbodyForEntities>(); }
        if (bodyCapsule == null) { bodyCapsule = GetComponent<CapsuleCollider>(); }
        if (cameraController == null) { cameraController = GetComponent<CC_CameraController>(); }

        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        jumpTimeoutDelta = jumpTimeout;
        fallTimeoutDelta = fallTimeout;

        groundedForward = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, upAxis);
        if (groundedForward.sqrMagnitude < 0.0001f)
        {
            groundedForward = Vector3.ProjectOnPlane(transform.forward, upAxis);
        }
        if (groundedForward.sqrMagnitude < 0.0001f)
        {
            groundedForward = Vector3.forward;
        }
        groundedForward.Normalize();

        // cache capsule defaults
        if (bodyCapsule != null)
        {
            capsuleBaseHeight = bodyCapsule.height;
            capsuleBaseCenter = bodyCapsule.center;
        }
    }

    public bool IsLocallyControlled()
    {
        return singlePlayer || (IsSpawned && IsOwner);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        InitialiseComponents();

        if (playerText != null && !singlePlayer)
        {
            playerText.text = (OwnerClientId + 1).ToString();
        }

        StartCoroutine(WaitSpawn(true));
    }

    private IEnumerator WaitSpawn(bool networked)
    {
        GameObject[] spawnPoints;
        ulong id = 0;
        while (true)
        {
            spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
            if (spawnPoints.Length > 0)
            {
                if (networked)
                {
                    id = OwnerClientId;
                }
                Transform spawnPoint = spawnPoints[id].transform;
                rb.position = spawnPoint.position;
                break;
            }
            yield return null;
        }
    }

    // Call in FixedUpdate.
    public virtual void TickFixed(Vector2 moveInput, bool jumpInput, float rollInput, bool sprintInput, bool crouchInput, bool stabiliseInput)
    {
        if (!IsLocallyControlled() || rb == null) { return; }

        // keep our up axis updated from gravity
        UpdateGravity();
        UpdateLocomotionMode();

        pendingRoll = -rollInput;

        // grounded should only matter in ground mode
        grounded = locomotionType == LocomotionType.GroundMode && customGravityBody != null && customGravityBody.Grounded;

        // clear each tick
        jumpedThisTick = false;
        usingGroundThrusters = false;
        usingSpaceMoveThrusters = false;
        usingSpaceStabiliseThrusters = false;
        thrusterDrainPerSecondThisTick = 0f;

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

        if (locomotionType == LocomotionType.SpaceMode)
        {
            SpaceThrusters(moveInput, jumpInput, crouchInput, stabiliseInput);
            SpaceBodyRotation();
        }
        else
        {
            GroundMove(moveInput);
            Jump(jumpInput);
            GroundThrusters(jumpInput);
            GroundBodyRotation();
        }

        // clear pending look after we use it in physics
        pendingLook = Vector2.zero;
    }

    // Call in LateUpdate.
    public virtual void TickLateState()
    {
        if (!IsLocallyControlled() || rb == null || values == null) { return; }

        // only drain stamina when sprinting in ground mode and grounded
        bool shouldDrain = sprinting && locomotionType == LocomotionType.GroundMode && grounded;

        // only regen stamina when grounded in ground mode
        bool allowRegen = locomotionType == LocomotionType.GroundMode && grounded;

        values.TickStamina(shouldDrain, allowRegen);

        // thrusters regen when not being used
        bool usingThrusters = usingGroundThrusters || usingSpaceMoveThrusters || usingSpaceStabiliseThrusters;
        values.TickThruster(usingThrusters, true, thrusterDrainPerSecondThisTick);

        // if we run out of stamina, force sprint off
        if (sprinting && !values.HasStamina())
        {
            sprinting = false;
        }
    }

    void UpdateGravity()
    {
        if (customGravityBody != null)
        {
            currentGravity = customGravityBody.CurrentGravity;
            upAxis = customGravityBody.UpAxis;
        }
        else
        {
            currentGravity = CustomGravity.GetGravity(rb.position, out upAxis);
        }
    }

    protected virtual void UpdateLocomotionMode()
    {
        locomotionType = currentGravity.magnitude <= zeroGravityThreshold
            ? LocomotionType.SpaceMode
            : LocomotionType.GroundMode;
    }

    protected virtual void GroundMove(Vector2 moveInput)
    {
        if (CameraTarget == null || rb == null) { return; }

        // current velocity split
        Vector3 vel = rb.linearVelocity;
        Vector3 verticalVel = Vector3.Project(vel, -upAxis);
        Vector3 planarVel = vel - verticalVel;

        // when in air in ground mode, allow limited thruster movement
        if (!grounded)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(CameraTarget.forward, upAxis);
            Vector3 camRight = Vector3.ProjectOnPlane(CameraTarget.right, upAxis);

            // if camera is looking almost straight up/down, projection can get tiny, so fallback
            if (camForward.sqrMagnitude < 0.0001f) { camForward = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, upAxis); }
            if (camRight.sqrMagnitude < 0.0001f) { camRight = Vector3.ProjectOnPlane(rb.rotation * Vector3.right, upAxis); }

            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = camRight * moveInput.x + camForward * moveInput.y;
            if (moveDir.sqrMagnitude > 1f) { moveDir.Normalize(); }

            // airborne movement uses thruster acceleration and thruster value
            if (moveDir.sqrMagnitude > 0.0001f && values != null && values.HasThruster())
            {
                rb.AddForce(moveDir * (thrusterAccel * airborneGroundControl), ForceMode.Acceleration);
                usingSpaceMoveThrusters = true;
                thrusterDrainPerSecondThisTick += spaceThrusterDrainPerSecond;
            }

            rb.linearVelocity = planarVel + verticalVel;
            return;
        }

        // build desired direction relative to camera, projected on gravity plane
        Vector3 camForwardGrounded = Vector3.ProjectOnPlane(CameraTarget.forward, upAxis);
        Vector3 camRightGrounded = Vector3.ProjectOnPlane(CameraTarget.right, upAxis);

        if (camForwardGrounded.sqrMagnitude < 0.0001f) { camForwardGrounded = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, upAxis); }
        if (camRightGrounded.sqrMagnitude < 0.0001f) { camRightGrounded = Vector3.ProjectOnPlane(rb.rotation * Vector3.right, upAxis); }

        camForwardGrounded.Normalize();
        camRightGrounded.Normalize();

        Vector3 moveDirGrounded = camRightGrounded * moveInput.x + camForwardGrounded * moveInput.y;
        if (moveDirGrounded.sqrMagnitude > 1f) { moveDirGrounded.Normalize(); }

        planarVel = Vector3.ProjectOnPlane(planarVel, upAxis);

        // grounded movement + friction
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

        Vector3 targetPlanarVel = moveDirGrounded * (moveSpeed * speedMult);
        float accelT = 1f - Mathf.Exp(-accelerationRate * Time.fixedDeltaTime);
        Vector3 newPlanarVel = Vector3.Lerp(planarVel, targetPlanarVel, accelT);

        rb.linearVelocity = newPlanarVel + verticalVel;
    }

    protected virtual void Jump(bool jumpPressed)
    {
        if (rb == null) { return; }

        if (grounded)
        {
            fallTimeoutDelta = fallTimeout;

            if (jumpPressed && jumpTimeoutDelta <= 0f)
            {
                // jump along current up axis so it works on walls/ceilings
                rb.AddForce(upAxis * jumpPower, ForceMode.Impulse);

                grounded = false;
                jumpTimeoutDelta = jumpTimeout;

                if (customGravityBody != null)
                {
                    customGravityBody.IgnoreGrounding(jumpTimeout);
                }

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

    protected virtual void GroundThrusters(bool jumpHeld)
    {
        if (rb == null || values == null) { return; }
        if (locomotionType != LocomotionType.GroundMode || grounded || !jumpHeld || !values.HasThruster()) { return; }

        float upSpeed = Vector3.Dot(rb.linearVelocity, upAxis);
        if (upSpeed >= groundThrusterUpSpeedCap) { return; }

        rb.AddForce(upAxis * groundThrusterAccel, ForceMode.Acceleration);

        usingGroundThrusters = true;
        thrusterDrainPerSecondThisTick += groundThrusterDrainPerSecond;
    }

    protected virtual void GroundBodyRotation()
    {
        if (rb == null) { return; }

        // keep our forward projected onto the gravity plane so yaw is preserved
        groundedForward = Vector3.ProjectOnPlane(groundedForward, upAxis);

        if (groundedForward.sqrMagnitude < 0.0001f)
        {
            groundedForward = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, upAxis);
        }
        if (groundedForward.sqrMagnitude < 0.0001f)
        {
            groundedForward = Vector3.ProjectOnPlane(transform.forward, upAxis);
        }
        if (groundedForward.sqrMagnitude < 0.0001f)
        {
            groundedForward = Vector3.forward;
        }

        groundedForward.Normalize();

        Quaternion current = rb.rotation;

        // yaw should not be smoothed, so pass through immediately
        Quaternion yawTarget = Quaternion.LookRotation(groundedForward, current * Vector3.up);

        // only stabilisation should be smoothed
        float strength = GetStabiliseStrength();
        if (strength <= 0.0001f)
        {
            bodyRotation = yawTarget;
            rb.MoveRotation(bodyRotation);
            return;
        }

        // align body up to gravity up axis with curve ramp, while preserving yaw
        Quaternion toUp = Quaternion.FromToRotation(yawTarget * Vector3.up, upAxis);
        Quaternion target = toUp * yawTarget;

        float alignT = 1f - Mathf.Exp(-Mathf.Max(0f, strength) * Time.fixedDeltaTime);
        bodyRotation = Quaternion.Slerp(yawTarget, target, alignT);

        rb.MoveRotation(bodyRotation);
    }

    protected virtual float GetStabiliseStrength()
    {
        if (locomotionType == LocomotionType.SpaceMode || rb == null) { return 0f; }

        float proximity01 = 0f;

        if (grounded)
        {
            proximity01 = 1f;
        }
        else
        {
            float range = Mathf.Max(0.01f, stabiliseRange);
            float castDist = range + stabilisePadding;

            Ray ray = new Ray(rb.position, -upAxis);
            Debug.DrawRay(rb.position, -upAxis * castDist, Color.red);

            RaycastHit hit;
            if (customGravityBody != null &&
                Physics.Raycast(ray, out hit, castDist, customGravityBody.GroundLayers, QueryTriggerInteraction.Ignore))
            {
                float d = Mathf.Clamp(hit.distance - stabilisePadding, 0f, range);
                proximity01 = 1f - (d / range);
            }
        }

        float baseStrength = grounded ? groundedTorqueStrength : torqueStrength;
        float curve = stabiliseCurve != null ? stabiliseCurve.Evaluate(proximity01) : proximity01;
        return baseStrength * stabiliseMaxMult * Mathf.Clamp01(curve);
    }

    protected virtual void SpaceThrusters(Vector2 moveInput, bool jumpPressed, bool crouchPressed, bool stabilisePressed)
    {
        // add force as thrusters would have inertia

        if (CameraTarget == null || rb == null || values == null) { return; }

        Vector3 forward = CameraTarget.forward;
        Vector3 right = CameraTarget.right;
        Vector3 up = CameraTarget.up;

        Vector3 accel = right * moveInput.x + forward * moveInput.y;

        if (jumpPressed) { accel += up; }
        if (crouchPressed) { accel -= up; }

        if (accel.sqrMagnitude > 1f) { accel.Normalize(); }

        float speedMult = 1f;
        if (sprinting) { speedMult *= sprintSpeedMult; }

        if (accel.sqrMagnitude > 0.0001f && values.HasThruster())
        {
            rb.AddForce(accel * (thrusterAccel * speedMult), ForceMode.Acceleration);

            usingSpaceMoveThrusters = true;
            thrusterDrainPerSecondThisTick += spaceThrusterDrainPerSecond;
        }

        if (stabilisePressed)
        {
            SpaceStabilisation();
        }
    }

    protected virtual void SpaceStabilisation()
    {
        if (rb == null || values == null || !values.HasThruster()) { return; }

        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        if (speed <= 0.01f) { return; }

        Vector3 accel = -velocity.normalized * spaceStabiliseAccel;

        // stop overshooting when nearly stopped
        float maxDeltaV = spaceStabiliseAccel * Time.fixedDeltaTime;
        if (speed < maxDeltaV)
        {
            accel = -velocity / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        }

        rb.AddForce(accel, ForceMode.Acceleration);

        // stabilising should also settle roll faster
        rollSpeed = Mathf.MoveTowards(rollSpeed, 0f, rollDamping * rollMaxSpeed * Time.fixedDeltaTime);

        usingSpaceStabiliseThrusters = true;
        thrusterDrainPerSecondThisTick += spaceStabiliseDrainPerSecond;
    }

    protected virtual void SpaceBodyRotation()
    {
        if (rb == null || CameraTarget == null) { return; }

        Quaternion current = rb.rotation;
        float dt = Time.fixedDeltaTime;

        Vector2 look = pendingLook;

        Vector3 camUp = CameraTarget.up.normalized;
        Vector3 camRight = CameraTarget.right.normalized;
        Vector3 camForward = CameraTarget.forward.normalized;

        // yaw/pitch
        float yawDelta = look.x * spaceTurnSpeed * dt;
        float pitchDelta = -look.y * spaceTurnSpeed * dt;

        Quaternion yawQ = Quaternion.AngleAxis(yawDelta, camUp);
        Quaternion pitchQ = Quaternion.AngleAxis(pitchDelta, camRight);

        // acceleration oriented roll
        float targetRollSpeed = pendingRoll * rollMaxSpeed;
        rollSpeed = Mathf.MoveTowards(rollSpeed, targetRollSpeed, rollAccel * rollMaxSpeed * dt);

        if (Mathf.Abs(pendingRoll) < 0.001f)
        {
            rollSpeed = Mathf.MoveTowards(rollSpeed, 0f, rollDamping * rollMaxSpeed * dt);
        }

        float rollDelta = rollSpeed * dt;
        Quaternion rollQ = Quaternion.AngleAxis(rollDelta, camForward);

        bodyRotation = rollQ * pitchQ * yawQ * current;
        rb.MoveRotation(bodyRotation);
    }

    public void AddGroundYaw(float lookX)
    {
        if (Mathf.Abs(lookX) <= 0.0001f || rb == null) { return; }

        Quaternion yawQ = Quaternion.AngleAxis(lookX, upAxis);
        groundedForward = yawQ * groundedForward;
        groundedForward = Vector3.ProjectOnPlane(groundedForward, upAxis);

        if (groundedForward.sqrMagnitude < 0.0001f)
        {
            groundedForward = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, upAxis);
        }

        groundedForward.Normalize();
    }

    public void SetPendingLook(Vector2 look)
    {
        // camera writes space look here, physics uses it in fixed
        pendingLook = look;
    }

    public void UpdateCapsuleCrouch(float crouchSharpness)
    {
        bool canCrouch = locomotionType == LocomotionType.GroundMode && grounded && !jumpedThisTick;
        if (!canCrouch) { crouching = false; }

        if (bodyCapsule == null) { return; }

        float targetHeight = capsuleBaseHeight * (crouching ? crouchCapsuleHeightMult : 1f);

        // target center is always based from the original capsule values
        Vector3 targetCenter = capsuleBaseCenter;
        if (crouching)
        {
            targetCenter.y -= (capsuleBaseHeight - targetHeight) * 0.5f;
        }

        float t = 1f - Mathf.Exp(-crouchSharpness * Time.deltaTime);

        bodyCapsule.height = Mathf.Lerp(bodyCapsule.height, targetHeight, t);
        bodyCapsule.center = Vector3.Lerp(bodyCapsule.center, targetCenter, t);
    }
}