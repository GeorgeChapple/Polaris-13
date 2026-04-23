using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Handles locomotion, gravity, jumping, body rotation and stamina / oxygen usage.

[RequireComponent(typeof(Rigidbody))]
public class CC_Movement : NetworkBehaviour
{
    [Header("References")]
    public CC_CharacterValues values;
    public CC_CameraController cameraController;

    [Header("Gravity")]
    [Tooltip("Custom gravity component on the same object as this.")]
    public CustomGravityRigidbodyForEntities customGravityBody;

    [Header("Player Movement")]
    public float moveSpeed = 5f;
    public float accelerationRate = 12f;
    public bool canTeleport = true;

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

    [Header("Ground Air Control")]
    [Tooltip("Allow horizontal airborne movement in gravity using the space thruster settings.")]
    [Range(0f, 1f)] public float airborneGroundControl = 1f;

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

    [Header("Space Stabilisation")]
    [Tooltip("Acceleration applied opposite to current velocity while stabilising in space.")]
    public float spaceStabiliseAccel = 8f;

    [Header("Gravity Detection")]
    public float zeroGravityThreshold = 0.25f;

    [Header("Grounded")]
    public bool grounded = true;
    public float jumpTimeout = 0.1f;
    public float fallTimeout = 0.15f;

    [Header("Replicated Visual Body")]
    [Tooltip("Transform used as the visual body.")]
    [SerializeField] private Transform visualBodyRoot;

    [Tooltip("If true, owner updates the replicated visual body root from the local visual body source.")]
    [SerializeField] private bool replicateVisualBody = true;

    [Header("Animator")]
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private NetworkAnimator networkAnimator;
    [SerializeField] private float groundAnimatorMaxSpeed = 6;    

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
    float oxygenDrainMultiplierThisTick;

    // jump / thruster input state
    bool jumpHeldLastTick;
    bool groundThrustersActivatedThisAirborne;
    bool groundThrustersRequiresRelease;

    bool initialised;
    bool deathRespawnRunning;

    // hook movement
    private bool hookMoveActive;
    private Vector3 hookAnchorPoint;
    private float hookMaxDistance;
    private float hookPullLerpStrength;
    private float hookMaxPullSpeed;
    private float hookOutwardVelocityDamping;

    // replicated visual body
    private NetworkVariable<Vector3> replicatedVisualBodyLocalPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<Quaternion> replicatedVisualBodyLocalRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    //animator
    private Vector3 currentSpeed;

    // public read for other components like camera/interaction
    public Rigidbody Body => rb;
    public Vector3 UpAxis => upAxis;
    public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
    public Vector3 CurrentGravity => currentGravity;
    public float GravityMagnitude => currentGravity.magnitude;
    public bool IsGrounded => grounded;
    public bool IsSprinting => sprinting;
    public bool IsCrouching => crouching;
    public bool JumpedThisTick => jumpedThisTick;
    public bool SinglePlayer => singlePlayer;
    public Transform CameraTarget => cameraController != null ? cameraController.cinemachineCameraTarget : null;
    public Transform ReplicatedVisualBodyRoot => visualBodyRoot;

    public Vector3 CurrentSpeed => currentSpeed;

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

        replicatedVisualBodyLocalPosition.OnValueChanged += OnReplicatedVisualBodyLocalPositionChanged;
        replicatedVisualBodyLocalRotation.OnValueChanged += OnReplicatedVisualBodyLocalRotationChanged;

        ApplyReplicatedVisualBody();

        StartCoroutine(WaitSpawn(true));
    }

    public override void OnNetworkDespawn()
    {
        replicatedVisualBodyLocalPosition.OnValueChanged -= OnReplicatedVisualBodyLocalPositionChanged;
        replicatedVisualBodyLocalRotation.OnValueChanged -= OnReplicatedVisualBodyLocalRotationChanged;

        base.OnNetworkDespawn();
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();
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
                    if (id >= (ulong)spawnPoints.Length)
                    {
                        id = 0;
                    }
                }

                Transform spawnPoint = spawnPoints[id].transform;
                rb.position = spawnPoint.position;
                break;
            }
            yield return null;
        }
    }

    private void Update()
    {
        currentSpeed = Vector3.Project(rb.linearVelocity / groundAnimatorMaxSpeed, Vector3.ProjectOnPlane(CameraTarget.forward, upAxis));
    }

    // Call in FixedUpdate.
    public virtual void TickFixed(Vector2 moveInput, bool jumpInput, float rollInput, bool sprintInput, bool crouchInput, bool stabiliseInput)
    {
        if (!IsLocallyControlled() || rb == null) { return; }

        // keep our up axis updated from gravity
        UpdateGravity();

        bool hasUsableGravity = HasUsableGravity();

        // dead = no movement/input handling
        if (values != null && values.isDead)
        {
            jumpedThisTick = false;
            usingGroundThrusters = false;
            usingSpaceMoveThrusters = false;
            usingSpaceStabiliseThrusters = false;
            oxygenDrainMultiplierThisTick = 0f;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (!deathRespawnRunning)
            {
                StartCoroutine(DeathRespawnRoutine());
            }

            return;
        }

        // grounded should only matter while gravity is strong enough to support grounded movement
        grounded = hasUsableGravity && customGravityBody != null && customGravityBody.Grounded;

        // detect jump press this tick so ground jump and airborne thrusters act like separate presses
        bool jumpPressedThisTick = jumpInput && !jumpHeldLastTick;

        // clear each tick
        jumpedThisTick = false;
        networkAnimator.ResetTrigger("Jumped");
        usingGroundThrusters = false;
        usingSpaceMoveThrusters = false;
        usingSpaceStabiliseThrusters = false;
        oxygenDrainMultiplierThisTick = 0f;

        // landing resets the airborne thruster activation
        if (grounded)
        {
            groundThrustersActivatedThisAirborne = false;
            groundThrustersRequiresRelease = false;
        }

        // crouch only with usable gravity + grounded
        bool canCrouch = hasUsableGravity && grounded;

        // crouch wins over sprint
        crouching = crouchInput && canCrouch;
        sprinting = sprintInput && !crouching;

        // if no stamina, no sprint
        if (values != null && sprinting && !values.HasStamina())
        {
            sprinting = false;
        }

        if (hasUsableGravity)
        {
            GroundMove(moveInput);
            Jump(jumpPressedThisTick);
            GroundThrusters(jumpInput, jumpPressedThisTick);
        }
        else
        {
            SpaceThrusters(moveInput, jumpInput, crouchInput, stabiliseInput);
        }

        ApplyHookMovement();
        UpdateBodyRotation();

        // cache jump held for edge detection next tick
        jumpHeldLastTick = jumpInput;
    }

    // Call in LateUpdate.
    public virtual void TickLateState()
    {
        if (!IsLocallyControlled() || rb == null || values == null)
        {
            ApplyReplicatedVisualBody();
            return;
        }

        if (values.isDead)
        {
            UpdateReplicatedVisualBody();
            UpdateAnimator();
            return;
        }

        // only drain stamina when sprinting with usable gravity and grounded
        bool shouldDrainStamina = sprinting && HasUsableGravity() && grounded;
        values.TickStamina(shouldDrainStamina, true);

        // oxygen regens when not being used for thrusters
        bool usingOxygenForThrusters = usingGroundThrusters || usingSpaceMoveThrusters || usingSpaceStabiliseThrusters;
        values.TickOxygenThrusterUsage(usingOxygenForThrusters, true, oxygenDrainMultiplierThisTick);

        // passive oxygen drain while not in usable gravity
        values.TickPassiveOxygenDrainInSpace(!HasUsableGravity());

        // if we run out of stamina, force sprint off
        if (sprinting && !values.HasStamina())
        {
            sprinting = false;
        }

        UpdateReplicatedVisualBody();
        UpdateAnimator();
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

    public bool HasUsableGravity()
    {
        if (customGravityBody != null && !customGravityBody.useGravity)
        {
            return false;
        }

        return currentGravity.magnitude >= zeroGravityThreshold;
    }

    public float GetGravityAlignment01()
    {
        float gravity01 = Mathf.Clamp01(currentGravity.magnitude / Mathf.Max(0.0001f, zeroGravityThreshold));

        float proximity01 = 0f;

        if (customGravityBody != null)
        {
            if (customGravityBody.Grounded)
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
                if (Physics.Raycast(ray, out hit, castDist, customGravityBody.GroundLayers, QueryTriggerInteraction.Ignore))
                {
                    float d = Mathf.Clamp(hit.distance - stabilisePadding, 0f, range);
                    proximity01 = 1f - (d / range);
                }
            }
        }

        float curve = stabiliseCurve != null ? stabiliseCurve.Evaluate(proximity01) : proximity01;
        return Mathf.Clamp01(gravity01 * Mathf.Clamp01(curve));
    }

    public float GetGravityAlignmentSharpness()
    {
        float align01 = GetGravityAlignment01();

        if (align01 >= 0.9999f) { return -1f; }

        return torqueStrength * stabiliseMaxMult * align01;
    }

    protected virtual void GroundMove(Vector2 moveInput)
    {
        if (CameraTarget == null || rb == null) { return; }

        // current velocity split
        Vector3 vel = rb.linearVelocity;
        Vector3 verticalVel = Vector3.Project(vel, -upAxis);
        Vector3 planarVel = vel - verticalVel;

        // when in air with gravity, allow limited thruster movement
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

            // airborne movement uses thruster acceleration and oxygen
            if (moveDir.sqrMagnitude > 0.0001f && values != null && values.HasOxygen())
            {
                rb.AddForce(moveDir * (thrusterAccel * airborneGroundControl), ForceMode.Acceleration);
                usingSpaceMoveThrusters = true;
                oxygenDrainMultiplierThisTick += values.spaceMoveOxygenDrainMult;
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

    protected virtual void Jump(bool jumpPressedThisTick)
    {
        if (rb == null) { return; }

        if (grounded)
        {
            fallTimeoutDelta = fallTimeout;

            if (jumpPressedThisTick && jumpTimeoutDelta <= 0f)
            {
                // jump along current up axis so it works on walls/ceilings
                rb.AddForce(upAxis * jumpPower, ForceMode.Impulse);

                grounded = false;
                jumpTimeoutDelta = jumpTimeout;
                groundThrustersRequiresRelease = true;

                if (customGravityBody != null)
                {
                    customGravityBody.IgnoreGrounding(jumpTimeout);
                }

                // when jumping stop crouching
                crouching = false;
                jumpedThisTick = true;
                networkAnimator.SetTrigger("Jumped");
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

    protected virtual void GroundThrusters(bool jumpHeld, bool jumpPressedThisTick)
    {
        if (rb == null || values == null) { return; }
        if (!HasUsableGravity() || grounded || !values.HasOxygen()) { return; }

        // holding jump from the initial ground jump shouldn't activate thrusters
        // player must release jump after jumping, then press again while airborne to activate thrusters
        if (groundThrustersRequiresRelease)
        {
            if (!jumpHeld)
            {
                groundThrustersRequiresRelease = false;
            }

            return;
        }

        // airborne thrusters activate like a second jump
        if (!groundThrustersActivatedThisAirborne)
        {
            if (!jumpPressedThisTick) { return; }
            groundThrustersActivatedThisAirborne = true;
        }

        // once activated, keep thrusting only while jump is held
        if (!jumpHeld) { return; }

        float upSpeed = Vector3.Dot(rb.linearVelocity, upAxis);
        if (upSpeed >= groundThrusterUpSpeedCap) { return; }

        rb.AddForce(upAxis * groundThrusterAccel, ForceMode.Acceleration);

        usingGroundThrusters = true;
        oxygenDrainMultiplierThisTick += values.groundThrusterOxygenDrainMult;
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

        if (accel.sqrMagnitude > 0.0001f && values.HasOxygen())
        {
            rb.AddForce(accel * (thrusterAccel * speedMult), ForceMode.Acceleration);

            usingSpaceMoveThrusters = true;
            oxygenDrainMultiplierThisTick += values.spaceMoveOxygenDrainMult;
        }

        if (stabilisePressed)
        {
            SpaceStabilisation();
        }
    }

    protected virtual void SpaceStabilisation()
    {
        if (rb == null || values == null || !values.HasOxygen()) { return; }

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

        usingSpaceStabiliseThrusters = true;
        oxygenDrainMultiplierThisTick += values.spaceStabiliseOxygenDrainMult;
    }

    private void ApplyHookMovement()
    {
        if (!IsLocallyControlled() || !hookMoveActive || rb == null)
        {
            return;
        }

        Vector3 fromAnchorToShooter = rb.position - hookAnchorPoint;
        float distance = fromAnchorToShooter.magnitude;

        if (distance <= 0.001f)
        {
            return;
        }

        Vector3 ropeDirection = fromAnchorToShooter / distance;
        float overshoot = distance - hookMaxDistance;

        // inside rope length, let normal movement happen
        if (overshoot <= 0f)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;

        // split into rope direction and sideways movement
        float radialSpeed = Vector3.Dot(velocity, ropeDirection);
        Vector3 radialVelocity = ropeDirection * radialSpeed;
        Vector3 tangentialVelocity = velocity - radialVelocity;

        // remove outward movement
        if (radialSpeed > 0f)
        {
            radialVelocity -= ropeDirection * (radialSpeed * hookOutwardVelocityDamping);
        }

        // pull inward based on overshoot
        float pullSpeed = Mathf.Min(overshoot * hookPullLerpStrength, hookMaxPullSpeed);
        Vector3 inwardVelocity = -ropeDirection * pullSpeed;

        rb.linearVelocity = tangentialVelocity + radialVelocity + inwardVelocity;
    }

    private void SetHookMoveLocal(Vector3 anchorPoint, float maxDistance, float pullLerpStrength, float maxPullSpeed, float outwardVelocityDamping)
    {
        hookAnchorPoint = anchorPoint;
        hookMaxDistance = maxDistance;
        hookPullLerpStrength = pullLerpStrength;
        hookMaxPullSpeed = maxPullSpeed;
        hookOutwardVelocityDamping = outwardVelocityDamping;
        hookMoveActive = true;
    }

    private void ClearHookMoveLocal()
    {
        hookMoveActive = false;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SetHookMoveLocalRpc(Vector3 anchorPoint, float maxDistance, float pullLerpStrength, float maxPullSpeed, float outwardVelocityDamping, RpcParams rpcParams = default)
    {
        SetHookMoveLocal(anchorPoint, maxDistance, pullLerpStrength, maxPullSpeed, outwardVelocityDamping);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ClearHookMoveLocalRpc(RpcParams rpcParams = default)
    {
        ClearHookMoveLocal();
    }

    public void SetHookMoveForOwner(Vector3 anchorPoint, float maxDistance, float pullLerpStrength, float maxPullSpeed, float outwardVelocityDamping)
    {
        if (!IsSpawned)
        {
            return;
        }

        if (IsOwner)
        {
            SetHookMoveLocal(anchorPoint, maxDistance, pullLerpStrength, maxPullSpeed, outwardVelocityDamping);
        }

        SetHookMoveLocalRpc
        (
            anchorPoint,
            maxDistance,
            pullLerpStrength,
            maxPullSpeed,
            outwardVelocityDamping,
            RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp)
        );
    }

    public void ClearHookMoveForOwner()
    {
        if (!IsSpawned)
        {
            return;
        }

        if (IsOwner)
        {
            ClearHookMoveLocal();
        }

        ClearHookMoveLocalRpc(RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
    }

    protected virtual void UpdateBodyRotation()
    {
        if (rb == null || CameraTarget == null) { return; }

        float alignStrength = GetGravityAlignmentSharpness();

        if (!HasUsableGravity() || alignStrength <= 0.0001f)
        {
            // in free space the body should always face exactly where the camera is facing
            bodyRotation = CameraTarget.rotation;
            rb.MoveRotation(bodyRotation);
            return;
        }

        // when gravity is influencing us, unwind back toward the gravity up axis
        // using the camera forward projected onto the gravity plane.
        Vector3 targetForward = Vector3.ProjectOnPlane(CameraTarget.forward, upAxis);

        if (targetForward.sqrMagnitude < 0.0001f)
        {
            targetForward = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, upAxis);
        }

        if (targetForward.sqrMagnitude < 0.0001f)
        {
            targetForward = groundedForward;
        }

        if (targetForward.sqrMagnitude < 0.0001f)
        {
            targetForward = Vector3.ProjectOnPlane(transform.forward, upAxis);
        }

        if (targetForward.sqrMagnitude < 0.0001f)
        {
            targetForward = Vector3.forward;
        }

        targetForward.Normalize();
        groundedForward = targetForward;

        Quaternion targetRotation = Quaternion.LookRotation(targetForward, upAxis);

        // inside snap zone = no smoothing
        if (alignStrength < 0f)
        {
            bodyRotation = targetRotation;
            rb.MoveRotation(bodyRotation);
            return;
        }

        float t = 1f - Mathf.Exp(-alignStrength * Time.fixedDeltaTime);
        bodyRotation = Quaternion.Slerp(rb.rotation, targetRotation, t);

        rb.MoveRotation(bodyRotation);
    }

    public void SetGroundForward(Vector3 forward)
    {
        if (rb == null) { return; }

        groundedForward = Vector3.ProjectOnPlane(forward, upAxis);

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
    }

    public void UpdateCapsuleCrouch(float crouchSharpness)
    {
        bool canCrouch = HasUsableGravity() && grounded && !jumpedThisTick;
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

    void UpdateReplicatedVisualBody()
    {
        if (!replicateVisualBody || !IsLocallyControlled() || visualBodyRoot == null) { return; }

        Transform source = visualBodyRoot;
        Transform parent = visualBodyRoot.parent;

        Vector3 localPos;
        Quaternion localRot;

        // convert source world pose into the replicated roots parent space
        if (parent != null)
        {
            localPos = parent.InverseTransformPoint(source.position);
            localRot = Quaternion.Inverse(parent.rotation) * source.rotation;
        }
        else
        {
            localPos = source.position;
            localRot = source.rotation;
        }

        replicatedVisualBodyLocalPosition.Value = localPos;
        replicatedVisualBodyLocalRotation.Value = localRot;

        // also apply locally so owner sees the same replicated transform
        ApplyReplicatedVisualBody();
    }

    void OnReplicatedVisualBodyLocalPositionChanged(Vector3 oldValue, Vector3 newValue)
    {
        ApplyReplicatedVisualBody();
    }

    void OnReplicatedVisualBodyLocalRotationChanged(Quaternion oldValue, Quaternion newValue)
    {
        ApplyReplicatedVisualBody();
    }

    void ApplyReplicatedVisualBody()
    {
        if (visualBodyRoot == null) { return; }

        Transform parent = visualBodyRoot.parent;

        if (parent != null)
        {
            visualBodyRoot.localPosition = replicatedVisualBodyLocalPosition.Value;
            visualBodyRoot.localRotation = replicatedVisualBodyLocalRotation.Value;
        }
        else
        {
            visualBodyRoot.position = replicatedVisualBodyLocalPosition.Value;
            visualBodyRoot.rotation = replicatedVisualBodyLocalRotation.Value;
        }
    }

    void UpdateAnimator()
    {
        bodyAnimator.SetFloat("X", currentSpeed.x);
        bodyAnimator.SetFloat("Y", currentSpeed.z);
        bodyAnimator.SetBool("Grounded", grounded);
    }

    private IEnumerator DeathRespawnRoutine()
    {
        deathRespawnRunning = true;

        float delay = values != null ? values.DeathRespawnDelay : 3f;
        yield return new WaitForSeconds(delay);

        if (rb != null)
        {
            RespawnAtSpawnPoint();
        }

        if (values != null)
        {
            values.RespawnReset();
        }

        grounded = false;
        crouching = false;
        sprinting = false;
        jumpedThisTick = false;

        groundThrustersActivatedThisAirborne = false;
        groundThrustersRequiresRelease = false;
        jumpHeldLastTick = false;

        ClearHookMoveLocal();

        deathRespawnRunning = false;
    }

    private void RespawnAtSpawnPoint()
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (spawnPoints == null || spawnPoints.Length == 0 || rb == null)
        {
            return;
        }

        int spawnIndex = 0;

        if (!singlePlayer)
        {
            spawnIndex = Mathf.Clamp((int)OwnerClientId, 0, spawnPoints.Length - 1);
        }

        Transform spawnPoint = spawnPoints[spawnIndex].transform;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        ClearHookMoveLocal();

        rb.position = spawnPoint.position;
        rb.rotation = spawnPoint.rotation;

        bodyRotation = rb.rotation;

        groundedForward = Vector3.ProjectOnPlane(spawnPoint.forward, upAxis);
        if (groundedForward.sqrMagnitude < 0.0001f)
        {
            groundedForward = Vector3.ProjectOnPlane(transform.forward, upAxis);
        }
        if (groundedForward.sqrMagnitude < 0.0001f)
        {
            groundedForward = Vector3.forward;
        }

        groundedForward.Normalize();
    }

    protected void AddPushForce(Vector3 force)
    {
        rb.AddForce(force);
    }

    [Rpc(SendTo.Everyone)]
    protected void AddPushForceRpc(Vector3 force)
    {
        AddPushForce(force);
    }

    public void PushSelf(Vector3 force)
    {
        AddPushForceRpc(force);
    }
}