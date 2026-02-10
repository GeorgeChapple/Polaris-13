using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Made by: Jason Lodge
// Summary: Uses unity's new input system to change raw control values for movement, jumping, etc.
public class CC_PlayerInputManager : MonoBehaviour
{
    [Header("Character Input Values")]
    public Vector2 move;
    public Vector2 look;
    public bool jump;
    public bool interact;

    [Header("Movement Modifiers")]
    public bool sprint;
    public bool crouch;

    [Header("Space Input Values")]
    public float roll;

#if ENABLE_INPUT_SYSTEM
    public void OnMove(InputValue value) { MoveInput(value.Get<Vector2>()); }
    public void OnLook(InputValue value) { LookInput(value.Get<Vector2>()); }
    public void OnJump(InputValue value) { JumpInput(value.isPressed); }
    public void OnSprint(InputValue value) { SprintInput(value.isPressed); }
    public void OnCrouch(InputValue value) { CrouchInput(value.isPressed); }
    public void OnInteract(InputValue value) { InteractInput(value.isPressed); }
    public void OnRoll(InputValue value) { RollInput(value.Get<float>()); }

#endif

    public void MoveInput(Vector2 newMoveDirection) { move = newMoveDirection; }
    public void LookInput(Vector2 newLookDirection) { look = newLookDirection; }
    public void JumpInput(bool newJumpState) { jump = newJumpState; }
    public void SprintInput(bool newSprint) { sprint = newSprint; }
    public void CrouchInput(bool newCrouch) { crouch = newCrouch; }
    public void InteractInput(bool newInteractState) { interact = newInteractState; }
    public void RollInput(float newRoll) { roll = newRoll; }

}
