using Unity.Cinemachine;
using UnityEngine;
using Unity.Netcode;

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

    [Header("Camera Bobbing")]
    [Tooltip("If true, camera bobbing is enabled (ground mode only).")]
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
    protected float baseFov;

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
    public virtual void TickLate(Vector2 lookInput, bool isMouse)
    {
        if (!IsLocallyControlled()) { return; }

        CameraRotation(lookInput, isMouse);
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

    protected virtual void CameraRotation(Vector2 look, bool isMouse)
    {
        const float threshold = 0.0001f;
        if (look.sqrMagnitude < threshold || cinemachineCameraTarget == null || movement == null) { return; }

        float dt = isMouse ? 1f : Time.deltaTime;

        float baseX = isMouse ? mouseBaseX : stickBaseX;
        float baseY = isMouse ? mouseBaseY : stickBaseY;

        float sx = baseX * lookMultX;
        float sy = baseY * lookMultY;

        float lookX = look.x * sx * dt;
        float lookY = look.y * sy * dt;

        if (invertY) { lookY = -lookY; }

        // pitch always affects camera target first
        cinemachineTargetPitch -= lookY;
        cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);
        cinemachineCameraTarget.localRotation = Quaternion.Euler(cinemachineTargetPitch, 0f, 0f);

        // in ground mode, apply yaw immediately
        if (movement.locomotionType == CC_Movement.LocomotionType.GroundMode)
        {
            movement.AddGroundYaw(lookX);
            return;
        }

        // in space mode cache look for fixed update rotation
        movement.SetPendingLook(new Vector2(lookX, lookY));
    }

    protected virtual void UpdateCrouch()
    {
        if (movement == null || cinemachineCameraTarget == null) { return; }

        bool canCrouch = movement.locomotionType == CC_Movement.LocomotionType.GroundMode && movement.IsGrounded && !movement.JumpedThisTick;

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

        // bob only in ground mode and grounded
        bool allowBob = movement.locomotionType == CC_Movement.LocomotionType.GroundMode && movement.IsGrounded;

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