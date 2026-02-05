using UnityEngine;

// Made by: Jason Lodge
// Summary: Holds all shared logic between all characters, player included.
// That is movement, Values like health and levels, (will have more as i figure it out) etc.
// Will also interact with this stuff: https://catlikecoding.com/unity/tutorials/movement/complex-gravity/

// Notes for future: I wanted to use rigid body so the space travelling and all of that could be dynamic
// , like if your floating and use thrusters to move it feels spacey and if you got close to one of the
// complex gravity drivers then it wouldnt be fighting against it like a character controller would.

// So for future reference,
// Ground mode: Rigidbody is kinematic(exception: explosions or other)
// Space mode: Rigidbody is not kinematic
// that all needs to be proven to work of course but i'll get to that later.


[RequireComponent(typeof(Rigidbody))]
public class CharacterBase : MonoBehaviour
{
    public enum locomotionType { GroundMode, SpaceMode }

    public locomotionType type = locomotionType.GroundMode;

    [Header("Player Movement")]
    public float moveSpeed = 5f;
    public float accelerationRate = 12f;

    [Header("Jump / Gravity")]
    public float jumpPower = 10f;

    [Header("Surface Stabalisation")]
    public float torqueStrength = 2f;

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
    public float cinemachineTargetPitch;

    // internals
    protected Rigidbody rb;

    protected float verticalVelocity;
    protected float yaw; // accumulated yaw
    protected float jumpTimeoutDelta;
    protected float fallTimeoutDelta;

    // keep velocity as a "desired" and smooth towards it
    protected Vector3 desiredPlanarVelocity;

    Vector3 upAxis;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        jumpTimeoutDelta = jumpTimeout;
        fallTimeoutDelta = fallTimeout;
    }

    // Call in FixedUpdate (physics)
    public virtual void TickMotorFixed(Vector2 moveInput, bool jumpInput)
    {
        TorqueStabalisation();
        GroundedCheck();
        MoveAndGravity(moveInput);
        Jump(jumpInput);
    }

    // Call in LateUpdate (camera).
    public virtual void TickCameraLate(Vector2 lookInput, bool isMouse)
    {
        CameraRotation(lookInput, isMouse);
    }

    protected virtual void GroundedCheck()
    {
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

        if (invertY)
        {
            lookY = -lookY;
        }

        // pitch
        cinemachineTargetPitch -= lookY; // mouse up = look up
        cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);
        cinemachineCameraTarget.localRotation = Quaternion.Euler(cinemachineTargetPitch, 0f, 0f);

        // yaw
        yaw += lookX;
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

        // note - this all needs to be translated into local for when the character would be upside down
    }

    protected virtual void TorqueStabalisation()
    {
        // check if we have ground below us, before stabilising our rotation
        // shoot ray in current gravity direction, limit its range
        Vector3 currentGravity = CustomGravity.GetGravity(rb.position, out upAxis);

        // get a ray to shoot toward the surface up to check if we're close enough to stabilise ourselves
        Ray ray = new Ray(transform.position, -upAxis);

        if (grounded || Physics.Raycast(ray, 5)) // stabilise ourselves
        {
            Vector3 torqueAxis = Vector3.Cross(transform.up, upAxis);
            rb.AddTorque(torqueAxis * torqueStrength, ForceMode.Force);
        }
    }

    protected virtual void MoveAndGravity(Vector2 moveInput)
    {
        // convert input into a direction relative to current facing direction
        Vector3 inputDir = (transform.right * moveInput.x + transform.forward * moveInput.y);
        inputDir.y = 0f;

        // current planar velocity from Rigidbody
        Vector3 currentVel = rb.linearVelocity;
        Vector3 currentPlanar = new Vector3(currentVel.x, currentVel.y, currentVel.z);

        // calculate our target speed with current gravity included
        float targetSpeed = (moveInput == Vector2.zero) ? 0f : moveSpeed;
        Vector3 targetPlanar = inputDir.normalized * targetSpeed;
        Vector3 currentVely = new Vector3(0, currentVel.y, 0);
        targetPlanar += currentVely;


        // smooth toward the target planar velocity
        desiredPlanarVelocity = Vector3.Lerp(currentPlanar, targetPlanar, accelerationRate * Time.fixedDeltaTime);

        if (grounded && jumpTimeout <= 0)
        {
            desiredPlanarVelocity = new Vector3(desiredPlanarVelocity.x, 0, desiredPlanarVelocity.z);
        }

        // preserve Y from verticalVelocity (jump / gravity)
        Vector3 finalVelocity = desiredPlanarVelocity;
        rb.linearVelocity = finalVelocity;
    }


    protected virtual void Jump(bool jumpPressed)
    { // translate this to local
        if (grounded)
        {
            fallTimeoutDelta = fallTimeout;

            if (jumpPressed && jumpTimeoutDelta <= 0f) { rb.AddForce(new Vector3(0, jumpPower, 0), ForceMode.Impulse); grounded = false; }
            if (jumpTimeoutDelta > 0f) { jumpTimeoutDelta -= Time.deltaTime; }
        }
        else
        {
            jumpTimeoutDelta = jumpTimeout;
            if (fallTimeoutDelta > 0f) { fallTimeoutDelta -= Time.deltaTime; }
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
        Gizmos.DrawSphere(groundedCheckObj.position, groundedRadius);
    }
}
