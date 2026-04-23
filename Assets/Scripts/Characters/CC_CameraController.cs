using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Handles camera ownership, look rotation, crouch camera offset, FOV, bobbing and replicated camera direction.

public class CC_CameraController : NetworkBehaviour
{
    [Header("References")]
    public CC_Movement movement;

    [Tooltip("Camera (Cinemachine)")]
    public CinemachineCamera cCam;

    [Header("Network Ownership")]
    [Tooltip("Camera Root GameObject that contains the Unity Camera + CinemachineBrain in It's Children.")]
    public GameObject cameraRoot;

    [Tooltip("Canvas Object")]
    public GameObject canvasObj;

    [Tooltip("Canvas Component")]
    public Canvas canvas;

    [Tooltip("Canvas Overlay Object")]
    public GameObject canvasOverlayObj;

    [Tooltip("If true, non owners will disable their cameraRoot. If false, does nothing for now.")]
    public bool destroyNonOwnerCameraRoot = true;

    [Header("Replicated Camera Direction")]
    [Tooltip("Always-active transform used as a replicated version of the local camera direction.")]
    [SerializeField] private Transform replicatedCameraDirectionRoot;

    [Tooltip("Source transform to copy for replicated camera direction. If null, uses camera target first, then cameraRoot transform.")]
    [SerializeField] private Transform replicatedCameraSource;

    [Tooltip("If true, owner updates the replicated camera direction root from the real camera target.")]
    [SerializeField] private bool replicateCameraDirection = true;

    [Header("Sprint")]
    [Tooltip("FOV multiplier while sprinting (1 = normal).")]
    public float sprintFovMult = 1.15f;

    [Tooltip("How quickly FOV changes.")]
    public float fovSharpness = 8f;

    [Header("Crouch")]
    [Tooltip("Local Y offset applied to the camera target when crouched.")]
    public float crouchCameraYOffset = -0.5f;

    [Tooltip("How quickly crouch transitions.")]
    public float crouchSharpness = 14f;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine camera that the camera will follow")]
    public Transform cinemachineCameraTarget;

    [Tooltip("How far in degrees you can move the camera up")]
    public float topClamp = 90.0f;

    [Tooltip("How far in degrees you can move the camera down")]
    public float bottomClamp = -90.0f;

    [Header("Gravity Camera Recovery")]
    [Tooltip("How quickly leftover space tilt/roll recovers once gravity takes over.")]
    public float gravityRecoverySharpness = 8f;

    [Tooltip("Maximum recovery speed when close enough to a gravity surface that body alignment snaps.")]
    public float gravityRecoverySnapSharpness = 20f;

    [Tooltip("When recovery is smaller than this angle, snap it fully to identity.")]
    public float gravityRecoverySnapAngle = 0.5f;

    [Header("Camera Bobbing")]
    [Tooltip("If true, camera bobbing is enabled while grounded in usable gravity.")]
    public bool enableCameraBobbing = true;

    [Tooltip("Bobbing frequency at full speed.")]
    public float bobFrequency = 1.8f;

    [Tooltip("Vertical bob amplitude at full speed.")]
    public float bobAmplitude = 0.01f;

    [Tooltip("Side-to-side bob amplitude at full speed.")]
    public float bobSideAmplitude = 0.005f;

    [Tooltip("How quickly bob blends in/out.")]
    public float bobSharpness = 6f;

    [Tooltip("Extra bob multiplier while sprinting.")]
    public float sprintBobMult = 1.25f;

    [Tooltip("Bob multiplier while crouching.")]
    public float crouchBobMult = 0.5f;

    [Header("Space Roll")]
    [Tooltip("Maximum roll speed in degrees per second.")]
    public float rollMaxSpeed = 120f;

    [Tooltip("How quickly roll accelerates toward target speed.")]
    public float rollAccel = 4f;

    [Tooltip("How quickly roll slows back to zero when no input is held.")]
    public float rollDamping = 2f;

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

    // space roll state
    protected float rollSpeed;

    // cinemachine
    protected float baseFov;

    // free camera rotation state in world space
    protected Quaternion cameraWorldRotation = Quaternion.identity;
    protected bool cameraWorldRotationInitialised;

    // grounded look state
    protected Vector3 gravityAlignedForward = Vector3.forward;
    protected float gravityAlignedPitch;

    // smooth recovery state while gravity is active
    protected Quaternion gravityRecoveryRotation = Quaternion.identity;
    protected bool wasUsingGravityLastFrame;

    // crouch state
    Vector3 camTargetBaseLocalPos;

    // camera bobbing state
    float bobTime;
    Vector3 bobOffset;
    Vector3 bobOffsetVel;

    bool initialised;

    // replicated camera direction
    private NetworkVariable<Vector3> replicatedCameraLocalPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<Quaternion> replicatedCameraLocalRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public Transform ReplicatedCameraDirectionRoot => replicatedCameraDirectionRoot;

    public Quaternion CameraWorldRotation => cameraWorldRotation;

    protected virtual void Awake()
    {
        InitialiseComponents();

        // only do ownership immediately in single player mode.
        if (movement != null && movement.SinglePlayer)
        {
            ApplyOwnership(true);
        }
    }

    void InitialiseComponents()
    {
        if (initialised) { return; }
        initialised = true;

        if (movement == null) { movement = GetComponent<CC_Movement>(); }

        // cache camera and base fov
        if (cCam != null) { baseFov = cCam.Lens.FieldOfView; }

        // cache camera target local position so crouch just offsets it
        if (cinemachineCameraTarget != null)
        {
            camTargetBaseLocalPos = cinemachineCameraTarget.localPosition;
        }

        // prefer the actual look source first so replicated aim includes pitch / space look properly
        if (replicatedCameraSource == null)
        {
            if (cinemachineCameraTarget != null)
            {
                replicatedCameraSource = cinemachineCameraTarget;
            }
            else if (cameraRoot != null)
            {
                replicatedCameraSource = cameraRoot.transform;
            }
        }

        InitialiseCameraWorldRotation();
        SyncGravityStateFromWorldRotation();
        gravityRecoveryRotation = Quaternion.identity;
        wasUsingGravityLastFrame = movement != null && movement.HasUsableGravity();
        ApplyCameraWorldRotation();
    }

    void InitialiseCameraWorldRotation()
    {
        if (cameraWorldRotationInitialised) { return; }

        if (cinemachineCameraTarget != null)
        {
            cameraWorldRotation = cinemachineCameraTarget.rotation;
        }
        else if (movement != null && movement.Body != null)
        {
            cameraWorldRotation = movement.Body.rotation;
        }
        else
        {
            cameraWorldRotation = transform.rotation;
        }

        cameraWorldRotationInitialised = true;
    }

    void SyncGravityStateFromWorldRotation()
    {
        if (movement == null) { return; }

        Vector3 upAxis = movement.UpAxis;
        Vector3 forward = cameraWorldRotation * Vector3.forward;
        Vector3 right = cameraWorldRotation * Vector3.right;

        gravityAlignedForward = Vector3.ProjectOnPlane(forward, upAxis);

        if (gravityAlignedForward.sqrMagnitude < 0.0001f)
        {
            gravityAlignedForward = Vector3.ProjectOnPlane(transform.forward, upAxis);
        }

        if (gravityAlignedForward.sqrMagnitude < 0.0001f && movement.Body != null)
        {
            gravityAlignedForward = Vector3.ProjectOnPlane(movement.Body.rotation * Vector3.forward, upAxis);
        }

        if (gravityAlignedForward.sqrMagnitude < 0.0001f)
        {
            gravityAlignedForward = Vector3.forward;
        }

        gravityAlignedForward.Normalize();

        gravityAlignedPitch = Vector3.SignedAngle(gravityAlignedForward, forward, right);
        gravityAlignedPitch = Mathf.Clamp(gravityAlignedPitch, bottomClamp, topClamp);
    }

    void ApplyCameraWorldRotation()
    {
        if (cinemachineCameraTarget == null) { return; }

        Transform parent = cinemachineCameraTarget.parent;

        if (parent != null)
        {
            cinemachineCameraTarget.localRotation = Quaternion.Inverse(parent.rotation) * cameraWorldRotation;
        }
        else
        {
            cinemachineCameraTarget.rotation = cameraWorldRotation;
        }
    }

    bool IsLocallyControlled()
    {
        return movement != null && movement.IsLocallyControlled();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        InitialiseComponents();
        ApplyOwnership(IsOwner);

        replicatedCameraLocalPosition.OnValueChanged += OnReplicatedCameraLocalPositionChanged;
        replicatedCameraLocalRotation.OnValueChanged += OnReplicatedCameraLocalRotationChanged;

        ApplyReplicatedCameraDirection();
    }

    public override void OnNetworkDespawn()
    {
        replicatedCameraLocalPosition.OnValueChanged -= OnReplicatedCameraLocalPositionChanged;
        replicatedCameraLocalRotation.OnValueChanged -= OnReplicatedCameraLocalRotationChanged;

        base.OnNetworkDespawn();
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();

        if (movement != null && !movement.SinglePlayer)
        {
            ApplyOwnership(true);
        }
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();

        if (movement != null && !movement.SinglePlayer)
        {
            ApplyOwnership(false);
        }
    }

    void ApplyOwnership(bool isOwnerNow)
    {
        if (cameraRoot == null)
        {
            Debug.LogError("Camera Root not set in editor!", this);
            return;
        }

        // In single player, always act like a local owner
        if (movement != null && movement.SinglePlayer)
        {
            isOwnerNow = true;
        }

        if (isOwnerNow)
        {
            if (!cameraRoot.activeSelf) { cameraRoot.SetActive(true); }

            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;

                Camera cam = cameraRoot.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    canvas.worldCamera = cam;
                }
            }

            if (canvasObj != null)
            {
                canvasObj.SetActive(true);
            }
        }
        else
        {
            // disable cam for non owners.
            if (destroyNonOwnerCameraRoot && cameraRoot != null)
            {
                cameraRoot.SetActive(false);
            }

            if (canvasObj != null && canvasOverlayObj != null)
            {
                canvasObj.SetActive(false);
                canvasOverlayObj.SetActive(false);
            }
        }
    }

    // Call in LateUpdate.
    public virtual void TickLate(Vector2 lookInput, float rollInput, bool isMouse)
    {
        if (!IsLocallyControlled()) { return; }

        CameraRotation(lookInput, rollInput, isMouse);
        UpdateCrouch();
        UpdateSprintFov();
        UpdateCameraBobbing();
        UpdateReplicatedCameraDirection();

        // body capsule crouch is handled by movement, camera crouch offset is handled here
        if (movement != null)
        {
            movement.UpdateCapsuleCrouch(crouchSharpness);
        }
    }

    protected virtual void CameraRotation(Vector2 look, float rollInput, bool isMouse)
    {
        if (cinemachineCameraTarget == null || movement == null) { return; }

        InitialiseCameraWorldRotation();

        float sx = isMouse ? mouseBaseX * lookMultX : stickBaseX * lookMultX;
        float sy = isMouse ? mouseBaseY * lookMultY : stickBaseY * lookMultY;
        float dt = isMouse ? 1f : Time.deltaTime;

        float yawDelta = look.x * sx * dt;
        float pitchDelta = look.y * sy * dt;

        if (invertY) { pitchDelta = -pitchDelta; }

        bool hasUsableGravity = movement.HasUsableGravity();

        if (!hasUsableGravity)
        {
            // fully free camera in space
            Vector3 currentUp = cameraWorldRotation * Vector3.up;
            Vector3 currentRight = cameraWorldRotation * Vector3.right;
            Vector3 currentForward = cameraWorldRotation * Vector3.forward;

            Quaternion yawQ = Quaternion.AngleAxis(yawDelta, currentUp);
            Quaternion pitchQ = Quaternion.AngleAxis(-pitchDelta, currentRight);

            // thruster-style roll acceleration
            float targetRollSpeed = -rollInput * rollMaxSpeed;
            rollSpeed = Mathf.MoveTowards(rollSpeed, targetRollSpeed, rollAccel * rollMaxSpeed * Time.deltaTime);

            if (Mathf.Abs(rollInput) < 0.001f)
            {
                rollSpeed = Mathf.MoveTowards(rollSpeed, 0f, rollDamping * rollMaxSpeed * Time.deltaTime);
            }

            float rollDelta = rollSpeed * Time.deltaTime;
            Quaternion rollQ = Quaternion.AngleAxis(rollDelta, currentForward);

            cameraWorldRotation = rollQ * pitchQ * yawQ * cameraWorldRotation;
            cameraWorldRotation = Quaternion.Normalize(cameraWorldRotation);

            wasUsingGravityLastFrame = false;
            ApplyCameraWorldRotation();
            return;
        }

        // entering gravity: stop space roll momentum
        if (!wasUsingGravityLastFrame)
        {
            rollSpeed = 0f;
        }

        wasUsingGravityLastFrame = true;

        // gravity mode should keep camera forward from player look
        // gravity only recovers roll toward the up axis
        Vector3 upAxis = movement.UpAxis;

        Vector3 lookForward = cameraWorldRotation * Vector3.forward;
        Vector3 lookRight = cameraWorldRotation * Vector3.right;
        Vector3 lookUp = cameraWorldRotation * Vector3.up;

        // yaw around gravity up
        if (Mathf.Abs(yawDelta) > 0.0001f)
        {
            Quaternion yawQ = Quaternion.AngleAxis(yawDelta, upAxis);
            lookForward = yawQ * lookForward;
            lookRight = yawQ * lookRight;
            lookUp = yawQ * lookUp;
        }

        // pitch around current camera right
        if (Mathf.Abs(pitchDelta) > 0.0001f)
        {
            Quaternion pitchQ = Quaternion.AngleAxis(-pitchDelta, lookRight);
            lookForward = pitchQ * lookForward;
            lookUp = pitchQ * lookUp;
        }

        lookForward.Normalize();

        // recover only roll so camera up moves toward gravity up around current forward
        Vector3 desiredUp = Vector3.ProjectOnPlane(upAxis, lookForward);
        if (desiredUp.sqrMagnitude < 0.0001f)
        {
            desiredUp = Vector3.ProjectOnPlane(lookUp, lookForward);
        }
        if (desiredUp.sqrMagnitude < 0.0001f)
        {
            desiredUp = lookUp;
        }
        desiredUp.Normalize();

        Vector3 currentUpOnPlane = Vector3.ProjectOnPlane(lookUp, lookForward);
        if (currentUpOnPlane.sqrMagnitude < 0.0001f)
        {
            currentUpOnPlane = desiredUp;
        }
        currentUpOnPlane.Normalize();

        float signedRollError = Vector3.SignedAngle(currentUpOnPlane, desiredUp, lookForward);

        float alignStrength = movement.GetGravityAlignmentSharpness();
        float recoverySharpness = Mathf.Max(gravityRecoverySharpness, alignStrength > 0f ? alignStrength : 0f);

        float t = 1f - Mathf.Exp(-recoverySharpness * Time.deltaTime);
        float recoveredRollStep = signedRollError * t;

        Quaternion rollRecoveryQ = Quaternion.AngleAxis(recoveredRollStep, lookForward);

        Vector3 finalUp = rollRecoveryQ * lookUp;

        cameraWorldRotation = Quaternion.LookRotation(lookForward, finalUp);
        cameraWorldRotation = Quaternion.Normalize(cameraWorldRotation);

        // keep body yaw state roughly synced from planar forward when gravity exists
        Vector3 planarForward = Vector3.ProjectOnPlane(lookForward, upAxis);
        if (planarForward.sqrMagnitude > 0.0001f)
        {
            movement.SetGroundForward(planarForward.normalized);
        }

        ApplyCameraWorldRotation();
    }

    protected virtual void UpdateCrouch()
    {
        if (movement == null || cinemachineCameraTarget == null) { return; }

        bool canCrouch = movement.HasUsableGravity() && movement.IsGrounded && !movement.JumpedThisTick;

        // change cam height
        Vector3 target = camTargetBaseLocalPos;
        if (canCrouch && movement.IsCrouching)
        {
            target.y += crouchCameraYOffset;
        }

        float t = 1f - Mathf.Exp(-crouchSharpness * Time.deltaTime);
        cinemachineCameraTarget.localPosition = Vector3.Lerp(cinemachineCameraTarget.localPosition, target, t);
    }

    protected virtual void UpdateSprintFov()
    {
        if (cCam == null || movement == null) { return; }

        float targetFov = baseFov;

        // only raise fov when sprinting
        if (movement.IsSprinting) { targetFov = baseFov * sprintFovMult; }

        float t = 1f - Mathf.Exp(-fovSharpness * Time.deltaTime);
        cCam.Lens.FieldOfView = Mathf.Lerp(cCam.Lens.FieldOfView, targetFov, t);
    }

    protected virtual void UpdateCameraBobbing()
    {
        if (!enableCameraBobbing || cinemachineCameraTarget == null || movement == null) { return; }

        // bob only with usable gravity and grounded
        bool allowBob = movement.HasUsableGravity() && movement.IsGrounded;

        // use planar speed for intensity
        Vector3 planarVel = Vector3.ProjectOnPlane(movement.Velocity, movement.UpAxis);
        float speed01 = Mathf.Clamp01(planarVel.magnitude / Mathf.Max(0.01f, movement.moveSpeed));

        // no input movement or not allowed, fade out bob
        Vector3 targetBob = Vector3.zero;

        if (allowBob && speed01 > 0.01f)
        {
            float mult = speed01;

            // sprint/crouch affects bob intensity
            if (movement.IsSprinting) { mult *= sprintBobMult; }
            if (movement.IsCrouching) { mult *= crouchBobMult; }

            bobTime += Time.deltaTime * bobFrequency * (0.5f + mult);

            float y = Mathf.Sin(bobTime * Mathf.PI * 2f) * bobAmplitude * mult;
            float x = Mathf.Cos(bobTime * Mathf.PI * 2f * 0.5f) * bobSideAmplitude * mult;

            targetBob = new Vector3(x, y, 0f);
        }
        else
        {
            // reset time slowly so coming to stop feels clean
            bobTime = Mathf.MoveTowards(bobTime, 0f, Time.deltaTime * bobFrequency);
        }

        // smooth bob offset
        float smoothTime = bobSharpness <= 0f ? 0.01f : (1f / bobSharpness);
        bobOffset = Vector3.SmoothDamp(bobOffset, targetBob, ref bobOffsetVel, smoothTime);

        // apply bob on top of base local pos + crouch offset
        cinemachineCameraTarget.localPosition += bobOffset;
    }

    void UpdateReplicatedCameraDirection()
    {
        if (!replicateCameraDirection || !IsLocallyControlled() || replicatedCameraDirectionRoot == null) { return; }

        Transform source = replicatedCameraSource != null
            ? replicatedCameraSource
            : (cinemachineCameraTarget != null
                ? cinemachineCameraTarget
                : (cameraRoot != null ? cameraRoot.transform : null));

        if (source == null) { return; }

        Transform parent = replicatedCameraDirectionRoot.parent;

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

        replicatedCameraLocalPosition.Value = localPos;
        replicatedCameraLocalRotation.Value = localRot;

        // also apply locally so owner sees the same replicated transform
        ApplyReplicatedCameraDirection();
    }

    void OnReplicatedCameraLocalPositionChanged(Vector3 oldValue, Vector3 newValue)
    {
        ApplyReplicatedCameraDirection();
    }

    void OnReplicatedCameraLocalRotationChanged(Quaternion oldValue, Quaternion newValue)
    {
        ApplyReplicatedCameraDirection();
    }

    void ApplyReplicatedCameraDirection()
    {
        if (replicatedCameraDirectionRoot == null) { return; }

        Transform parent = replicatedCameraDirectionRoot.parent;

        if (parent != null)
        {
            replicatedCameraDirectionRoot.localPosition = replicatedCameraLocalPosition.Value;
            replicatedCameraDirectionRoot.localRotation = replicatedCameraLocalRotation.Value;
        }
        else
        {
            replicatedCameraDirectionRoot.position = replicatedCameraLocalPosition.Value;
            replicatedCameraDirectionRoot.rotation = replicatedCameraLocalRotation.Value;
        }
    }

    protected static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) { angle += 360f; }
        if (angle > 360f) { angle -= 360f; }

        return Mathf.Clamp(angle, min, max);
    }
}