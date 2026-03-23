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
    public bool useItemPrimary;

    [Header("Movement Modifiers")]
    public bool sprint;
    public bool crouch;
    public bool stabiliseThrusters;

    [Header("Space Input Values")]
    public float roll;

    [Header("Menu Values")]
    public bool pause;
    public bool inventory;
    public bool rotateItem;
    public bool dropItem;
    public bool dropHeldItem;
    public float hotBar;

    [Header("Hotbar Slot Values")]
    public bool hotbarSlot1;
    public bool hotbarSlot2;
    public bool hotbarSlot3;
    public bool hotbarSlot4;

#if ENABLE_INPUT_SYSTEM
    public void OnMove(InputValue value) { MoveInput(value.Get<Vector2>()); }
    public void OnLook(InputValue value) { LookInput(value.Get<Vector2>()); }
    public void OnJump(InputValue value) { JumpInput(value.isPressed); }
    public void OnSprint(InputValue value) { SprintInput(value.isPressed); }
    public void OnCrouch(InputValue value) { CrouchInput(value.isPressed); }
    public void OnStabiliseThrusters(InputValue value) { StabiliseThrustersInput(value.isPressed); }
    public void OnInteract(InputValue value) { InteractInput(value.isPressed); }
    public void OnUseItemPrimary(InputValue value) { UseItemPrimary(value.isPressed); }
    public void OnRotateItem(InputValue value) { RotateItemInput(value.isPressed); }
    public void OnDropItem(InputValue value) { DropItemInput(value.isPressed); }
    public void OnDropHeldItem(InputValue value) { DropHeldItemInput(value.isPressed); }
    public void OnPause(InputValue value) { PauseInput(value.isPressed); }
    public void OnInventory(InputValue value) { InventoryInput(value.isPressed); }
    public void OnHotbar(InputValue value) { HotbarInput(value.Get<float>()); }
    public void OnHotbarSlot1(InputValue value) { HotbarSlot1Input(value.isPressed); }
    public void OnHotbarSlot2(InputValue value) { HotbarSlot2Input(value.isPressed); }
    public void OnHotbarSlot3(InputValue value) { HotbarSlot3Input(value.isPressed); }
    public void OnHotbarSlot4(InputValue value) { HotbarSlot4Input(value.isPressed); }
    public void OnRoll(InputValue value) { RollInput(value.Get<float>()); }
#endif

    public void MoveInput(Vector2 newMoveDirection) { move = newMoveDirection; }
    public void LookInput(Vector2 newLookDirection) { look = newLookDirection; }
    public void JumpInput(bool newJumpState) { jump = newJumpState; }
    public void SprintInput(bool newSprint) { sprint = newSprint; }
    public void CrouchInput(bool newCrouch) { crouch = newCrouch; }
    public void StabiliseThrustersInput(bool newState) { stabiliseThrusters = newState; }
    public void InteractInput(bool newInteractState) { interact = newInteractState; }
    public void UseItemPrimary(bool newUseItemPrimaryState) { useItemPrimary = newUseItemPrimaryState; }
    public void RotateItemInput(bool newRotateItemState) { rotateItem = newRotateItemState; }
    public void DropItemInput(bool newDropItemState) { dropItem = newDropItemState; }
    public void DropHeldItemInput(bool newDropHeldItemState) { dropHeldItem = newDropHeldItemState; }
    public void PauseInput(bool newPauseState) { pause = newPauseState; }
    public void InventoryInput(bool newInventoryState) { inventory = newInventoryState; }
    public void HotbarInput(float newHotBarState) { hotBar = newHotBarState; }
    public void HotbarSlot1Input(bool newState) { hotbarSlot1 = newState; }
    public void HotbarSlot2Input(bool newState) { hotbarSlot2 = newState; }
    public void HotbarSlot3Input(bool newState) { hotbarSlot3 = newState; }
    public void HotbarSlot4Input(bool newState) { hotbarSlot4 = newState; }
    public void RollInput(float newRollState) { roll = newRollState; }
}