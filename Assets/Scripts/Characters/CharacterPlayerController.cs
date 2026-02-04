using UnityEngine;
using UnityEngine.InputSystem;

// Made by: Jason Lodge
// Summary: Player controller, drives all the locomotion code in the character base and things like interaction.
[RequireComponent(typeof(CharacterBase))]
public class CharacterPlayerController : MonoBehaviour
{
#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
#endif

    private PlayerInputManager input;
    private CharacterBase characterBase;

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
        input = GetComponent<PlayerInputManager>();
        characterBase = GetComponent<CharacterBase>();
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
        characterBase.TickMotorFixed(input.move, input.jump);

        input.jump = false;
    }

    private void LateUpdate()
    {
        // camera / rotation
        characterBase.TickCameraLate(input.look, IsCurrentDeviceMouse);
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
