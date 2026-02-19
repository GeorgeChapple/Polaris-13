using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CC_ClientPlayerControl : NetworkBehaviour
{
    [SerializeField] private CC_BodyAndCamera bodyAndCamera;
    [SerializeField] private GameObject cam;
    [SerializeField] private CustomGravityRigidbody m_CustomGravityRigidbody;
    [SerializeField] private CC_Movement m_CharacterMovement;
    [SerializeField] private CC_BodyAndCamera m_BodyAndCamera;
    [SerializeField] private CC_PlayerController m_CharacterPlayerController;
    [SerializeField] private CC_PlayerInputManager m_PlayerInputManager;
    [SerializeField] private PlayerInput m_PlayerInput;

    [HideInInspector] public bool ready = false;

    public bool WireCameraIn(GameObject c)
    {
        cam = c;
        if (cam != null) { return true; }
        return false;
    }

    public void Init()
    {
        m_CustomGravityRigidbody = GetComponent<CustomGravityRigidbody>();
        m_CharacterMovement = GetComponent<CC_Movement>();
        m_BodyAndCamera = GetComponent<CC_BodyAndCamera>();
        m_CharacterPlayerController = GetComponent<CC_PlayerController>();
        m_PlayerInputManager = GetComponent<CC_PlayerInputManager>();
        m_PlayerInput = GetComponent<PlayerInput>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log("ON NETWORK SPAWN", this);
        StartCoroutine(FixWiring());
    }
    private IEnumerator FixWiring()
    { 
        m_PlayerInput.enabled = false;
        m_PlayerInputManager.enabled = false;
        m_CharacterPlayerController.enabled = false;

        if (!ready) 
        { 
            Debug.Log($"{name} not ready yet.", this);

        }
        
        while (cam.transform.parent.gameObject == null)
        {
            yield return null;
        }

        if (!IsOwner)
        {
            Destroy(cam.transform.parent.gameObject);

        }

        if (IsOwner)
        {
            m_PlayerInput.enabled = true;
        }

        if (IsServer)
        {
            m_PlayerInputManager.enabled = true;
            m_CharacterPlayerController.enabled = true;
        }

        yield return null;
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;

        UpdateInputRPC(
            m_PlayerInputManager.move, m_PlayerInputManager.look, m_PlayerInputManager.jump, m_PlayerInputManager.interact,
            m_PlayerInputManager.sprint, m_PlayerInputManager.crouch,
            m_PlayerInputManager.roll,
            m_PlayerInputManager.pause, m_PlayerInputManager.inventory, m_PlayerInputManager.rotateItem, m_PlayerInputManager.dropItem, m_PlayerInputManager.dropHeldItem, m_PlayerInputManager.hotBar
        );
    }

    [Rpc(target: SendTo.Server)]
    private void UpdateInputRPC(
        Vector2 move, Vector2 look, bool jump, bool interact,
        bool sprint, bool crouch,
        float roll,
        bool pause, bool inventory, bool rotateItem, bool dropItem, bool dropHeldItem, float hotbar
    )
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
