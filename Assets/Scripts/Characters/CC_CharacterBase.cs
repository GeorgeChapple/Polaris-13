using Unity.Cinemachine;
using UnityEngine;
using Unity.Netcode;

// Made by: Jason Lodge
// Summary: Holds all shared logic between all characters, player included.

[RequireComponent(typeof(Rigidbody))]
public class CC_CharacterBase : NetworkBehaviour
{
    public enum LocomotionType { GroundMode, SpaceMode }

    public LocomotionType locomotionType = LocomotionType.GroundMode;

    [Header("References")]
    public CC_CharacterValues values;

    [Header("Player Movement")]
    public float moveSpeed = 5f;
    public float accelerationRate = 12f;

    [Header("Sprint")]
    public float sprintSpeedMult = 1.5f;

    [Tooltip("FOV multiplier while sprinting (1 = normal).")]
    public float sprintFovMult = 1.15f;

    [Tooltip("How quickly FOV changes.")]
    public float fovSharpness = 8f;

    [Tooltip("Camera (Cinemachine)")]
    public CinemachineCamera cCam;

    [Header("Ownership")]
    [Tooltip("Camera Root GameObject that contains the Unity Camera + CinemachineBrain in It's Children.")]
    public GameObject cameraRoot;

    [Tooltip("Canvas Object")]
    public GameObject canvasObj;

    [Tooltip("Canvas Component")]
    public Canvas canvas;

    [Tooltip("If true, non owners will destroy their cameraRoot. If false, does nothing for now.")]
    public bool destroyNonOwnerCameraRoot = true;

    [Header("Crouch")]
    public float crouchSpeedMult = 0.5f;

    [Tooltip("Local Y offset applied to the camera target when crouched.")]
    public float crouchCameraYOffset = -0.5f;

    [Tooltip("How quickly crouch transitions.")]
    public float crouchSharpness = 14f;

    [Tooltip("Capsule collider on the body. Changes size when crouched.")]
    public CapsuleCollider bodyCapsule;

    [Tooltip("Capsule height multiplier while crouched.")]
    public float crouchCapsuleHeightMult = 0.6f;

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

    [Header("Space Rotation")]
    public float spaceTurnSpeed = 30f;
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

    [Header("Interaction")]
    public float interactRange = 2.5f;

    [Tooltip("Layers that contain interactables.")]
    public LayerMask interactLayers;

    [Tooltip("Origin used for interaction ray. If null, will use transform.")]
    public Transform interactOrigin;

    [Header("Body")]
    [Tooltip("Visual body object.")]
    public Transform bodyTransform;

    [Tooltip("How far the body sits above the ball along up axis.")]
    public float bodyUpOffset = 1f;

    [Tooltip("How fast body position follows ball.")]
    public float bodyFollowPosSharpness = 50f;

    [Tooltip("How fast body rotation follows its target rotation (up axis).")]
    public float bodyFollowRotSharpness = 50f;

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

    // internals
    protected Rigidbody rb;
    protected float jumpTimeoutDelta;
    protected float fallTimeoutDelta;
    bool jumpedThisTick;

    // sprint/crouch state
    protected bool sprinting;
    protected bool crouching;

    // fov state
    protected float baseFov;

    // crouch state
    Vector3 camTargetBaseLocalPos;
    float capsuleBaseHeight;
    Vector3 capsuleBaseCenter;

    // camera bobbing state
    float bobTime;
    Vector3 bobOffset;
    Vector3 bobOffsetVel;

    // look
    protected Vector2 pendingLook;
    protected float pendingRoll;
    protected float rollSpeed;

    // body orientation state
    protected Quaternion bodyRotation = Quaternion.identity;

    // gravity
    Vector3 upAxis;
    Vector3 currentGravity;

    // interact
    InteractableObject currentInteractable;
    float holdTimer;
    bool holding;
    bool interactWasHeld;
    bool interactUsedUntilRelease;

    bool ownershipApplied;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (values == null) { values = GetComponent<CC_CharacterValues>(); }

        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        jumpTimeoutDelta = jumpTimeout;
        fallTimeoutDelta = fallTimeout;

        if (bodyTransform != null)
        {
            bodyRotation = bodyTransform.rotation;
        }
        else
        {
            bodyRotation = transform.rotation;
        }

        // ownership (nuke cameras, set refereneces)
        ApplyOwnership(IsOwner);

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


    }

    void ApplyOwnership(bool isOwnerNow)
    {
        if (cameraRoot == null)
        {
            Debug.LogError("Camera Root not set in editor!", this);
            ownershipApplied = true;
            return;
        }

        if (isOwnerNow)
        {
            // make sure it's enabled for the local owner
            if (!cameraRoot) { ownershipApplied = true; return; }

            if (!cameraRoot.activeSelf) { cameraRoot.SetActive(true); }
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            if (cameraRoot != null)
            {
                canvas.worldCamera = cameraRoot.GetComponentInChildren<Camera>();
            }
        }
        else
        {
            // nuke cam for non owners only
            if (destroyNonOwnerCameraRoot)
            {
                Destroy(cameraRoot);
            }
            
            canvasObj.SetActive(false);
        }

        

        ownershipApplied = true;
    }

    // Call in FixedUpdate.
    public virtual void TickFixed(Vector2 moveInput, bool jumpInput, float rollInput, bool sprintInput, bool crouchInput)
    {
        if (!IsOwner) { return; }
        // keep our up axis updated from gravity
        currentGravity = CustomGravity.GetGravity(rb.position, out upAxis);

        UpdateLocomotionMode();

        pendingRoll = -rollInput;

        // grounded should only matter in ground mode
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

        // body rotation only applied to the body
        if (locomotionType == LocomotionType.SpaceMode)
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
    public virtual void TickLate(Vector2 lookInput, bool isMouse)
    {
        if (!IsOwner) { return; }
        CameraRotation(lookInput, isMouse);
        UpdateBodyFollow();
        UpdateCrouch();
        UpdateSprintFov();
        UpdateCameraBobbing();

        if (values != null)
        {
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

    protected virtual void UpdateBodyFollow()
    {
        if (bodyTransform == null) { return; }

        // body sits above the ball along up axis, and follows smoothly
        Vector3 targetPos = rb.position + (upAxis * bodyUpOffset);
        float posT = 1f - Mathf.Exp(-bodyFollowPosSharpness * Time.deltaTime);
        bodyTransform.position = Vector3.Lerp(bodyTransform.position, targetPos, posT);

        // follow the calculated body rotation smoothly
        float rotT = 1f - Mathf.Exp(-bodyFollowRotSharpness * Time.deltaTime);
        bodyTransform.rotation = Quaternion.Slerp(bodyTransform.rotation, bodyRotation, rotT);
    }

    protected virtual void UpdateCrouch()
    {
        bool canCrouch = locomotionType == LocomotionType.GroundMode && grounded && !jumpedThisTick;
        if (!canCrouch) { crouching = false; }

        // change cam height and lower collider size
        if (cinemachineCameraTarget != null)
        {
            Vector3 target = camTargetBaseLocalPos;
            if (crouching) { target.y += crouchCameraYOffset; }

            float t = 1f - Mathf.Exp(-crouchSharpness * Time.deltaTime);
            cinemachineCameraTarget.localPosition = Vector3.Lerp(cinemachineCameraTarget.localPosition, target, t);
        }

        if (bodyCapsule != null)
        {
            float targetHeight = capsuleBaseHeight * (crouching ? crouchCapsuleHeightMult : 1f);

            // center.y shifts by half the height delta to only affect top height
            float heightDelta = targetHeight - bodyCapsule.height;
            Vector3 targetCenter = bodyCapsule.center;
            targetCenter.y += heightDelta * 0.5f;

            float t = 1f - Mathf.Exp(-crouchSharpness * Time.deltaTime);

            bodyCapsule.height = Mathf.Lerp(bodyCapsule.height, targetHeight, t);
            bodyCapsule.center = Vector3.Lerp(bodyCapsule.center, targetCenter, t);
        }
    }

    protected virtual void UpdateSprintFov()
    {
        if (cCam == null) { return; }

        float targetFov = baseFov;

        // only raise fov when sprinting
        if (sprinting) { targetFov = baseFov * sprintFovMult; }

        float t = 1f - Mathf.Exp(-fovSharpness * Time.deltaTime);
        cCam.Lens.FieldOfView = Mathf.Lerp(cCam.Lens.FieldOfView, targetFov, t);
    }

    protected virtual void UpdateCameraBobbing()
    {
        if (!enableCameraBobbing || cinemachineCameraTarget == null) { return; }

        // bob only in ground mode and grounded
        bool allowBob = locomotionType == LocomotionType.GroundMode && grounded;

        // use planar speed for intensity
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, upAxis);
        float speed01 = Mathf.Clamp01(planarVel.magnitude / Mathf.Max(0.01f, moveSpeed));

        // no input movement or not allowed, fade out bob
        Vector3 targetBob = Vector3.zero;

        if (allowBob && speed01 > 0.01f)
        {
            float mult = speed01;

            // sprint/crouch affects bob intensity
            if (sprinting) { mult *= sprintBobMult; }
            if (crouching) { mult *= crouchBobMult; }

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

    protected virtual void GroundMove(Vector2 moveInput)
    {
        if (cinemachineCameraTarget == null) { return; }

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

        // build desired direction relative to camera, projected on surface plane
        Vector3 camForward = Vector3.ProjectOnPlane(cinemachineCameraTarget.forward, upAxis);
        Vector3 camRight = Vector3.ProjectOnPlane(cinemachineCameraTarget.right, upAxis);

        // if camera is looking almost straight up/down, projection can get tiny, so fallback
        if (camForward.sqrMagnitude < 0.0001f) { camForward = Vector3.ProjectOnPlane(bodyRotation * Vector3.forward, upAxis); }
        if (camRight.sqrMagnitude < 0.0001f) { camRight = Vector3.ProjectOnPlane(bodyRotation * Vector3.right, upAxis); }

        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camRight * moveInput.x + camForward * moveInput.y);
        if (moveDir.sqrMagnitude > 1f) { moveDir.Normalize(); }

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

    protected virtual void GroundBodyRotation()
    {
        if (bodyTransform == null) { return; }

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

    protected virtual float GetStabiliseStrength()
    {
        if (locomotionType == LocomotionType.SpaceMode) { return 0f; }

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
            if (Physics.Raycast(ray, out hit, castDist, groundLayers, QueryTriggerInteraction.Ignore))
            {
                float d = Mathf.Clamp(hit.distance - stabilisePadding, 0f, range);
                proximity01 = 1f - (d / range);
            }
        }

        float curve = stabiliseCurve != null ? stabiliseCurve.Evaluate(proximity01) : proximity01;
        return torqueStrength * stabiliseMaxMult * Mathf.Clamp01(curve);
    }

    protected virtual void SpaceThrusters(Vector2 moveInput, bool jumpPressed, bool crouchPressed)
    {
        // add force as thrusters would have inertia

        if (cinemachineCameraTarget == null) { return; }

        Vector3 forward = cinemachineCameraTarget.forward;
        Vector3 right = cinemachineCameraTarget.right;
        Vector3 up = cinemachineCameraTarget.up;

        Vector3 accel = (right * moveInput.x + forward * moveInput.y);

        if (jumpPressed) { accel += up; }
        if (crouchPressed) { accel -= up; }

        if (accel.sqrMagnitude > 1f) { accel.Normalize(); }

        float speedMult = 1f;
        if (sprinting) { speedMult *= sprintSpeedMult; }

        rb.AddForce(accel * (thrusterAccel * speedMult), ForceMode.Acceleration);
    }

    protected virtual void SpaceBodyRotation()
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

    protected virtual InteractableObject GetLookInteractable()
    {
        Transform origin =
            interactOrigin != null ? interactOrigin :
            cinemachineCameraTarget != null ? cinemachineCameraTarget :
            transform;

        Ray ray = new Ray(origin.position, origin.forward);
        Debug.DrawRay(origin.position, origin.forward * interactRange, Color.cyan);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, interactRange, interactLayers, QueryTriggerInteraction.Collide))
        {
            return hit.collider.GetComponentInParent<InteractableObject>();
        }

        return null;
    }

    public virtual void TickInteract(bool interactHeld)
    {
        bool pressed = interactHeld && !interactWasHeld;
        bool released = !interactHeld && interactWasHeld;
        interactWasHeld = interactHeld;

        // once we interact, dont allow more interactions until the button is released
        if (interactUsedUntilRelease)
        {
            if (!interactHeld)
            {
                interactUsedUntilRelease = false;
            }
            else
            {
                return;
            }
        }

        InteractableObject current = GetLookInteractable();

        // if our obj changed, cancel previous hold
        if (currentInteractable != null && current != currentInteractable)
        {
            if (holding)
            {
                currentInteractable.CancelHold(transform.parent.gameObject);
            }

            currentInteractable = null;
            holdTimer = 0f;
            holding = false;
        }

        currentInteractable = current;

        // nothing to interact with
        if (currentInteractable == null)
        {
            holdTimer = 0f;
            holding = false;
            return;
        }

        // one shot used up etc
        if (!currentInteractable.CanInteract())
        {
            holdTimer = 0f;
            holding = false;
            return;
        }

        // if tap interact, only fire once
        if (!currentInteractable.requiresHold)
        {
            if (pressed)
            {
                currentInteractable.Interact(transform.parent.gameObject);

                // stop interacting until released
                interactUsedUntilRelease = true;

                // clear hold state
                currentInteractable = null;
                holdTimer = 0f;
                holding = false;
            }

            return;
        }

        // hold interact
        // if released while holding then cancel
        if (!interactHeld)
        {
            if (holding)
            {
                currentInteractable.CancelHold(transform.parent.gameObject);
            }

            holdTimer = 0f;
            holding = false;
            currentInteractable.HoldProgress(transform.parent.gameObject, 0f);
            return;
        }

        // start hold on press
        if (pressed)
        {
            holding = true;
            holdTimer = 0f;
            currentInteractable.BeginHold(transform.parent.gameObject);
        }

        // if we're holding, progress it
        if (holding)
        {
            float required = Mathf.Max(0.01f, currentInteractable.GetHoldTime());
            holdTimer += Time.deltaTime;

            float progress01 = Mathf.Clamp01(holdTimer / required);
            currentInteractable.HoldProgress(transform.parent.gameObject, progress01);

            if (holdTimer >= required)
            {
                currentInteractable.Interact(transform.parent.gameObject);

                // stop interacting until release
                interactUsedUntilRelease = true;

                // clear state so we dont immediately restart hold
                currentInteractable = null;
                holdTimer = 0f;
                holding = false;
            }
        }
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

        if (groundedCheckObj != null)
        {
            Gizmos.DrawSphere(groundedCheckObj.position, groundedRadius);
        }
    }
}