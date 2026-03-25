using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class CC_ClientPlayerControl : NetworkBehaviour
{
    [SerializeField] private GameObject cam;
    [SerializeField] private CustomGravityRigidbody m_CustomGravityRigidbody;
    [SerializeField] private CC_Movement m_Movement;
    [SerializeField] private CC_CharacterPlayerController m_CharacterPlayerController;
    [SerializeField] private CC_PlayerInputManager m_PlayerInputManager;
    [SerializeField] private PlayerInput m_PlayerInput;

    public bool WireCameraIn(GameObject c)
    {
        cam = c;
        if (cam != null) { return true; }
        return false;
    }

    public void Init()
    {
        m_CustomGravityRigidbody = GetComponent<CustomGravityRigidbody>();
        m_Movement = GetComponent<CC_Movement>();
        m_CharacterPlayerController = GetComponent<CC_CharacterPlayerController>();
        m_PlayerInputManager = GetComponent<CC_PlayerInputManager>();
        m_PlayerInput = GetComponent<PlayerInput>();
        //m_CustomGravityRigidbody.enabled = false;
        //m_CharacterBase.enabled = false;
        //m_CharacterPlayerController.enabled = false;
        //m_PlayerInputManager.enabled = false;
        //m_PlayerInput.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            m_PlayerInputManager.enabled = true;
            m_PlayerInput.enabled = true;
        }

        if (IsServer)
        {
            m_CustomGravityRigidbody.enabled = true;
            m_Movement.enabled = true;
            m_CharacterPlayerController.enabled = true;
        }
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;

        UpdateInputRPC(
            m_PlayerInputManager.move, m_PlayerInputManager.look, m_PlayerInputManager.jump, m_PlayerInputManager.interact,
            m_PlayerInputManager.sprint, m_PlayerInputManager.crouch,
            m_PlayerInputManager.roll,
            m_PlayerInputManager.pause, m_PlayerInputManager.monitoringMenu, m_PlayerInputManager.rotateItem, m_PlayerInputManager.dropItem, m_PlayerInputManager.dropHeldItem, m_PlayerInputManager.hotBar
        );
    }

    [Rpc(target:SendTo.Server)]
    private void UpdateInputRPC(
        Vector2 move, Vector2 look, bool jump, bool interact, 
        bool sprint, bool crouch,
        float roll,
        bool pause, bool inventory, bool rotateItem, bool dropItem, bool dropHeldItem, float hotbar
    ) {
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
        m_PlayerInputManager.MonitoringMenuInput(inventory);
        m_PlayerInputManager.RotateItemInput(rotateItem);
        m_PlayerInputManager.DropItemInput(dropItem);
        m_PlayerInputManager.DropHeldItemInput(dropHeldItem);
        m_PlayerInputManager.HotbarInput(hotbar);
    }
}
