using Unity.Cinemachine;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Body alignment and camera logic.
// Notes:
// - This handles Body Capsule position/rotation in world space so it doesn't inherit ball rolling.

public class CC_BodyAndCamera : MonoBehaviour
{
    [Header("References")]
    public CC_Movement movement;

    [Tooltip("Camera (Cinemachine)")]
    public CinemachineCamera cCam;

    [Header("Body")]
    [Tooltip("Visual body object.")]
    public Transform bodyTransform;

    [Tooltip("How far the body sits above the ball along up axis.")]
    public float bodyUpOffset = 1f;

    [Tooltip("How fast body position follows ball.")]
    public float bodyFollowPosSharpness = 50f;

    [Tooltip("How fast body rotation follows its target rotation (up axis).")]
    public float bodyFollowRotSharpness = 50f;

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

    [Header("Ground Mask (for stabilise ray)")]
    public LayerMask groundLayers;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine camera that the camera will follow")]
    public Transform cinemachineCameraTarget;

    [Tooltip("How far in degrees you can move the camera up")]
    public float topClamp = 90.0f;

    [Tooltip("How far in degrees you can move the camera down")]
    public float bottomClamp = -90.0f;

    [Header("Crouch")]
    [Tooltip("Local Y offset applied to the camera target when crouched.")]
    public float crouchCameraYOffset = -0.5f;

    [Tooltip("How quickly crouch transitions.")]
    public float crouchSharpness = 14f;

    [Tooltip("Capsule collider on the body. Changes size when crouched.")]
    public CapsuleCollider bodyCapsule;

    [Tooltip("Capsule height multiplier while crouched.")]
    public float crouchCapsuleHeightMult = 0.6f;

    [Header("Sprint FOV")]
    [Tooltip("FOV multiplier while sprinting (1 = normal).")]
    public float sprintFovMult = 1.15f;

    [Tooltip("How quickly FOV changes.")]
    public float fovSharpness = 8f;

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

    [Header("Space Rotation")]
    public float spaceTurnSpeed = 30f;
    public float rollMaxSpeed = 30f;
    public float rollAccel = 4f;
    public float rollDamping = 2f;

    // cinemachine
    float cinemachineTargetPitch;

    // fov state
    float baseFov;

    // crouch state
    Vector3 camTargetBaseLocalPos;
    float capsuleBaseHeight;
    Vector3 capsuleBaseCenter;

    // camera bobbing state
    float bobTime;
    Vector3 bobOffset;
    Vector3 bobOffsetVel;

    // look
    Vector2 pendingLook;
    float pendingRoll;
    float rollSpeed;

    // body orientation state
    Quaternion bodyRotation = Quaternion.identity;

    void Awake()
    {
        if (movement == null) { movement = GetComponent<CC_Movement>(); }

        // cache camera and base fov
        if (cCam != null) { baseFov = cCam.Lens.FieldOfView; }

        // cache camera target local position so crouch just offsets it
        if (cinemachineCameraTarget != null)
        {
            camTargetBaseLocalPos = cinemachineCameraTarget.localPosition;
        }

        // cache capsule defaults (body, not ball)
        if (bodyCapsule != null)
        {
            capsuleBaseHeight = bodyCapsule.height;
            capsuleBaseCenter = bodyCapsule.center;
        }

        // init body rotation
        if (bodyTransform != null)
        {
            bodyRotation = bodyTransform.rotation;
        }
        else
        {
            bodyRotation = transform.rotation;
        }
    }

    // Call in FixedUpdate.
    public void TickFixed(float rollInput)
    {
        pendingRoll = -rollInput;

        if (movement == null) { return; }

        // body rotation only applied to the body
        if (movement.locomotionType == CC_Movement.LocomotionType.SpaceMode)
        {
            SpaceBodyRotation();
        }
        else
        {
            GroundBodyRotation();
        }

        // clear pending look after we use it in physics
        pendingLook = Vector2.zero;
    }

    // Call in LateUpdate.
    public void TickLate(Vector2 lookInput, bool isMouse)
    {
        if (movement == null) { return; }

        CameraRotation(lookInput, isMouse);
        UpdateBodyFollow();
        UpdateCrouch();
        UpdateSprintFov();
        UpdateCameraBobbing();
    }

    void CameraRotation(Vector2 look, bool isMouse)
    {
        const float threshold = 0.0001f;
        if (look.sqrMagnitude < threshold || cinemachineCameraTarget == null) { return; }

        float dt = isMouse ? 1f : Time.deltaTime;

        float baseX = isMouse ? mouseBaseX : stickBaseX;
        float baseY = isMouse ? mouseBaseY : stickBaseY;

        float sx = baseX * lookMultX;
        float sy = baseY * lookMultY;

        float lookX = look.x * sx * dt;
        float lookY = look.y * sy * dt;

        if (invertY) { lookY = -lookY; }

        // store for fixed update
        pendingLook += new Vector2(lookX, lookY);

        // pitch always affects camera target first
        cinemachineTargetPitch -= lookY;
        cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);
        cinemachineCameraTarget.localRotation = Quaternion.Euler(cinemachineTargetPitch, 0f, 0f);
    }

    void UpdateBodyFollow()
    {
        if (bodyTransform == null || movement == null) { return; }

        Rigidbody rb = movement.RB;
        Vector3 upAxis = movement.UpAxis;

        // body sits above the ball along up axis, and follows smoothly
        Vector3 targetPos = rb.position + (upAxis * bodyUpOffset);
        float posT = 1f - Mathf.Exp(-bodyFollowPosSharpness * Time.deltaTime);
        bodyTransform.position = Vector3.Lerp(bodyTransform.position, targetPos, posT);

        // follow the calculated body rotation smoothly
        float rotT = 1f - Mathf.Exp(-bodyFollowRotSharpness * Time.deltaTime);
        bodyTransform.rotation = Quaternion.Slerp(bodyTransform.rotation, bodyRotation, rotT);
    }

    void UpdateCrouch()
    {
        if (movement == null) { return; }

        bool canCrouch = movement.locomotionType == CC_Movement.LocomotionType.GroundMode && movement.grounded && !movement.JumpedThisTick;
        if (!canCrouch) { return; }

        // change cam height and lower collider size
        if (cinemachineCameraTarget != null)
        {
            Vector3 target = camTargetBaseLocalPos;
            if (movement.Crouching) { target.y += crouchCameraYOffset; }

            float t = 1f - Mathf.Exp(-crouchSharpness * Time.deltaTime);
            cinemachineCameraTarget.localPosition = Vector3.Lerp(cinemachineCameraTarget.localPosition, target, t);
        }

        if (bodyCapsule != null)
        {
            float targetHeight = capsuleBaseHeight * (movement.Crouching ? crouchCapsuleHeightMult : 1f);

            // center.y shifts by half the height delta to only affect top height
            float heightDelta = targetHeight - bodyCapsule.height;
            Vector3 targetCenter = bodyCapsule.center;
            targetCenter.y += heightDelta * 0.5f;

            float t = 1f - Mathf.Exp(-crouchSharpness * Time.deltaTime);

            bodyCapsule.height = Mathf.Lerp(bodyCapsule.height, targetHeight, t);
            bodyCapsule.center = Vector3.Lerp(bodyCapsule.center, targetCenter, t);
        }
    }

    void UpdateSprintFov()
    {
        if (cCam == null || movement == null) { return; }

        float targetFov = baseFov;

        // only raise fov when sprinting
        if (movement.Sprinting) { targetFov = baseFov * sprintFovMult; }

        float t = 1f - Mathf.Exp(-fovSharpness * Time.deltaTime);
        cCam.Lens.FieldOfView = Mathf.Lerp(cCam.Lens.FieldOfView, targetFov, t);
    }

    void UpdateCameraBobbing()
    {
        if (!enableCameraBobbing || cinemachineCameraTarget == null || movement == null) { return; }

        // bob only in ground mode and grounded
        bool allowBob = movement.locomotionType == CC_Movement.LocomotionType.GroundMode && movement.grounded;

        Rigidbody rb = movement.RB;
        Vector3 upAxis = movement.UpAxis;

        // use planar speed for intensity
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, upAxis);
        float speed01 = Mathf.Clamp01(planarVel.magnitude / Mathf.Max(0.01f, movement.moveSpeed));

        // no input movement or not allowed, fade out bob
        Vector3 targetBob = Vector3.zero;

        if (allowBob && speed01 > 0.01f)
        {
            float mult = speed01;

            // sprint/crouch affects bob intensity
            if (movement.Sprinting) { mult *= sprintBobMult; }
            if (movement.Crouching) { mult *= crouchBobMult; }

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
        float smoothTime = (bobSharpness <= 0f) ? 0.01f : (1f / bobSharpness);
        bobOffset = Vector3.SmoothDamp(bobOffset, targetBob, ref bobOffsetVel, smoothTime);

        // apply bob on top of base local pos + crouch offset
        cinemachineCameraTarget.localPosition += bobOffset;
    }

    void GroundBodyRotation()
    {
        if (bodyTransform == null || movement == null) { return; }

        Vector3 upAxis = movement.UpAxis;

        // align body up to gravity up axis with curve ramp
        float strength = GetStabiliseStrength();
        if (strength > 0.0001f)
        {
            Quaternion current = bodyRotation;

            Quaternion toUp = Quaternion.FromToRotation(current * Vector3.up, upAxis);
            Quaternion target = toUp * current;

            float t = 1f - Mathf.Exp(-strength * Time.fixedDeltaTime);
            bodyRotation = Quaternion.Slerp(current, target, t);
        }

        // rotate body around gravity up axis
        float yawDelta = pendingLook.x;
        if (Mathf.Abs(yawDelta) > 0.0001f)
        {
            Quaternion yawQ = Quaternion.AngleAxis(yawDelta, upAxis);
            bodyRotation = yawQ * bodyRotation;
        }
    }

    float GetStabiliseStrength()
    {
        if (movement == null) { return 0f; }
        if (movement.locomotionType == CC_Movement.LocomotionType.SpaceMode) { return 0f; }

        Rigidbody rb = movement.RB;
        Vector3 upAxis = movement.UpAxis;

        float proximity01 = 0f;

        if (movement.grounded)
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
            if (Physics.Raycast(ray, out hit, castDist, groundLayers, QueryTriggerInteraction.Ignore))
            {
                float d = Mathf.Clamp(hit.distance - stabilisePadding, 0f, range);
                proximity01 = 1f - (d / range);
            }
        }

        float curve = stabiliseCurve != null ? stabiliseCurve.Evaluate(proximity01) : proximity01;
        return torqueStrength * stabiliseMaxMult * Mathf.Clamp01(curve);
    }

    void SpaceBodyRotation()
    {
        if (bodyTransform == null || cinemachineCameraTarget == null) { return; }

        Quaternion current = bodyRotation;
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
    }

    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) { angle += 360f; }
        if (angle > 360f) { angle -= 360f; }

        return Mathf.Clamp(angle, min, max);
    }
}