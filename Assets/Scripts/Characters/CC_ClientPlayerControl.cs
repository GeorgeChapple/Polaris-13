using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Made by: George Chapple
// Added To By: Jason Lodge
// Summary: Host authority for controlling clients.
// Notes:
// - Owner client:
//   - reads local input
//   - sends to host
// - Host:
//   - applies physics for everyone

public class CC_ClientPlayerControl : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private CC_Movement m_Movement;
    [SerializeField] private CC_BodyAndCamera m_BodyAndCamera;
    [SerializeField] private CC_PlayerInputManager m_PlayerInputManager;
    [SerializeField] private PlayerInput m_PlayerInput;
    [SerializeField] private CustomGravityRigidbody m_CustomGravityRigidbody;
    [SerializeField] private CC_Interaction m_Interaction;

    // host input state
    Vector2 h_Move;
    Vector2 h_Look;
    bool h_Jump;
    bool h_Interact;
    bool h_Sprint;
    bool h_Crouch;
    float h_Roll;

    private void Awake()
    {
        // cache refs
        m_Movement = GetComponent<CC_Movement>();
        m_BodyAndCamera = GetComponent<CC_BodyAndCamera>();
        m_PlayerInputManager = GetComponent<CC_PlayerInputManager>();
        m_PlayerInput = GetComponent<PlayerInput>();
        m_CustomGravityRigidbody = GetComponent<CustomGravityRigidbody>();
        m_Interaction = GetComponent<CC_Interaction>();

        // turn off things to be updated (network spawn enables what is needed)
        if (m_CustomGravityRigidbody != null) { m_CustomGravityRigidbody.enabled = false; }
        if (m_Movement != null) { m_Movement.enabled = false; }
        if (m_Interaction != null) { m_Interaction.enabled = false; }
        if (m_PlayerInputManager != null) { m_PlayerInputManager.enabled = false; }
        if (m_PlayerInput != null) { m_PlayerInput.enabled = false; }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // owner reads input locally
        if (IsOwner)
        {
            if (m_PlayerInputManager != null) { m_PlayerInputManager.enabled = true; }
            if (m_PlayerInput != null) { m_PlayerInput.enabled = true; }
        }

        // host runs physics for everyone
        if (IsHost)
        {
            if (m_CustomGravityRigidbody != null) { m_CustomGravityRigidbody.enabled = true; }
            if (m_Movement != null) { m_Movement.enabled = true; }
            if (m_Interaction != null) { m_Interaction.enabled = true; }
        }
    }

    private void Update()
    {
        // owner sends input to host
        if (!IsOwner) { return; }
        if (m_PlayerInputManager == null) { return; }

        UpdateInputRPC(
            m_PlayerInputManager.move,
            m_PlayerInputManager.look,
            m_PlayerInputManager.jump,
            m_PlayerInputManager.interact,
            m_PlayerInputManager.sprint,
            m_PlayerInputManager.crouch,
            m_PlayerInputManager.roll,
            m_PlayerInputManager.pause,
            m_PlayerInputManager.inventory,
            m_PlayerInputManager.rotateItem,
            m_PlayerInputManager.dropItem,
            m_PlayerInputManager.dropHeldItem,
            m_PlayerInputManager.hotBar
        );
    }

    private void FixedUpdate()
    {
        // only host simulates
        if (!IsHost) { return; }
        if (m_Movement == null) { return; }

        // physics
        m_Movement.TickFixed(h_Move, h_Jump, h_Sprint, h_Crouch);
        if (m_BodyAndCamera != null)
        {
            m_BodyAndCamera.TickFixed(h_Roll);
        }
    }

    private void LateUpdate()
    {
        if (!IsHost) { return; }
        if (m_BodyAndCamera != null) { m_BodyAndCamera.TickLate(h_Look, false); }
        if (m_Movement != null) { m_Movement.TickLate(); }
        if (m_Interaction != null) { m_Interaction.TickInteract(h_Interact); }
    }

    [Rpc(target: SendTo.Server)]
    private void UpdateInputRPC(Vector2 move, Vector2 look, bool jump, bool interact,
        bool sprint, bool crouch, float roll, bool pause, bool inventory,
        bool rotateItem, bool dropItem, bool dropHeldItem, float hotbar)
    {
        // host input cache
        h_Move = move;
        h_Look = look;
        h_Jump = jump;
        h_Interact = interact;
        h_Sprint = sprint;
        h_Crouch = crouch;
        h_Roll = roll;

        // keep input manager mirrored on host for scripts that read it
        if (m_PlayerInputManager != null)
        {
            // Character Input Values
            m_PlayerInputManager.MoveInput(move);
            m_PlayerInputManager.LookInput(look);
            m_PlayerInputManager.JumpInput(jump);
            m_PlayerInputManager.InteractInput(interact);

            // Movement Modifiers
            m_PlayerInputManager.SprintInput(sprint);
            m_PlayerInputManager.CrouchInput(crouch);

            // Space Input Values
            m_PlayerInputManager.RollInput(roll);

            // Menu Values
            m_PlayerInputManager.PauseInput(pause);
            m_PlayerInputManager.InventoryInput(inventory);
            m_PlayerInputManager.RotateItemInput(rotateItem);
            m_PlayerInputManager.DropItemInput(dropItem);
            m_PlayerInputManager.DropHeldItemInput(dropHeldItem);
            m_PlayerInputManager.HotbarInput(hotbar);
        }
    }
}