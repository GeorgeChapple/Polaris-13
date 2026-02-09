using UnityEngine;
using UnityEngine.InputSystem;

// Made by: Jason Lodge
// Summary: Player controller, drives all the locomotion code in the character base and things like interaction.
// This is separated as the character base will be used by AI too to be modular.
[RequireComponent(typeof(CC_CharacterBase))]
public class CC_CharacterPlayerController : MonoBehaviour
{
#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
#endif

    private CC_PlayerInputManager input;
    private CC_CharacterBase characterBase;

    [Header("Cursor")]
    public bool lockCursorOnStart = true;

    private bool cursorLocked;

    private bool IsCurrentDeviceMouse
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return playerInput != null && playerInput.currentControlScheme == "KeyboardMouse";
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
        input = GetComponent<CC_PlayerInputManager>();
        characterBase = GetComponent<CC_CharacterBase>();
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && cursorLocked)
        {
            SetCursorLocked(true);
        }
    }

    private void FixedUpdate()
    {
        // movement / physics
        characterBase.TickFixed(input.move, input.jump, input.roll, input.sprint, input.crouch);

        input.jump = false;
    }

    private void LateUpdate()
    {
        // camera / rotation
        characterBase.TickLate(input.look, IsCurrentDeviceMouse);

        // interaction
        characterBase.TickInteract(input.interact);
    }

    private void SetCursorLocked(bool shouldLock)
    {
        cursorLocked = shouldLock;

        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
        if (!cursorLocked)
        {
            input.look = Vector2.zero;
        }
    }
}
