using UnityEngine;
using UnityEngine.InputSystem;

// Made by: Jason Lodge
// Summary: Player controller, drives all the locomotion code in the character base and things like interaction.
public class CharacterPlayerController : MonoBehaviour
{
    [Header("Player Movement")]
    public float moveSpeed = 5f;
    public float accelerationRate = 1.2f;
    public float RotationSpeed = 1.0f;

    [Header("Jump / Gravity")]
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Grounded Check")]
    public bool grounded = true;
    public float groundedOffset = 0.1f;
    public float groundedRadius = 0.5f;
    public LayerMask groundLayers;

    public float jumpTimeout = 0.1f;
    public float fallTimeout = 0.15f;

    public float terminalVelocity = 53.0f;
    private float verticalVelocity;
    private float rotationVelocity;
    private float jumpTimeoutDelta;
    private float fallTimeoutDelta;
    private float speed;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public GameObject CinemachineCameraTarget;
    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 90.0f;
    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -90.0f;

    // cinemachine
    public float cinemachineTargetPitch;

#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
#endif
    private PlayerInputManager input;
    private GameObject cam;
    private Rigidbody rb;
    private const float _threshold = 0.01f;

    private bool IsCurrentDeviceMouse
    {
        get
        {
            #if ENABLE_INPUT_SYSTEM
            return playerInput.currentControlScheme == "KeyboardMouse";
            #else
				return false;
            #endif
        }
    }

    private void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
#endif
        input = GetComponent<PlayerInputManager>();
        rb = GetComponent<Rigidbody>();
    }


    void FixedUpdate()
    {
        GroundedCheck();
        Move();
        JumpAndGravity();
    }

    private void LateUpdate()
    {
        CameraRotation();

    }

    private void GroundedCheck()
    {
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
        grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
    }
    private void CameraRotation()
    {
        // if there is an input
        if (input.look.sqrMagnitude >= _threshold)
        {
            //Don't multiply mouse input by Time.deltaTime
            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            cinemachineTargetPitch += input.look.y * RotationSpeed * deltaTimeMultiplier;
            rotationVelocity = input.look.x * RotationSpeed * deltaTimeMultiplier;

            // clamp our pitch rotation
            cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, BottomClamp, TopClamp);

            // rotate the player left and right
            transform.Rotate(Vector3.up * rotationVelocity);
        }
    }

    private void Move()
    {
        Vector3 inputDir = transform.right * input.move.x + transform.forward * input.move.y;
        inputDir.y = 0f;
        float targetSpeed = input.move == Vector2.zero ? 0f : moveSpeed;
        float currentSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
        speed = Mathf.Lerp(currentSpeed, targetSpeed * input.move.magnitude, Time.fixedDeltaTime * accelerationRate);

        Vector3 horizontalMove = inputDir.normalized * speed * Time.fixedDeltaTime;

        // Combine horizontal move with vertical velocity
        Vector3 move = rb.position + horizontalMove + Vector3.up * verticalVelocity * Time.fixedDeltaTime;

        rb.MovePosition(move);
    }

    private void JumpAndGravity()
    {
        if (grounded)
        {
            fallTimeoutDelta = fallTimeout;

            if (verticalVelocity <= 0f)
            {
                verticalVelocity = 0f;
            }
            if (input.jump && jumpTimeoutDelta <= 0f)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            if (jumpTimeoutDelta > 0f)
            {
                jumpTimeoutDelta -= Time.deltaTime;
            }
        }
        else
        {
            jumpTimeoutDelta = jumpTimeout;

            if (fallTimeoutDelta > 0f)
            {
                fallTimeoutDelta -= Time.deltaTime;
            }
            if (verticalVelocity < terminalVelocity)
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

        }
        input.jump = false;
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }


    private void OnDrawGizmosSelected()
    {
        Color col = grounded ? new Color(0, 1, 0, 0.35f) : new Color(1, 0, 0, 0.35f);
        Gizmos.color = col;
        Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z), groundedRadius);
    }
}

