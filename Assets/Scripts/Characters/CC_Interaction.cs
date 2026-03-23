using Unity.Netcode;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Handles interaction and tap / hold interaction logic, also interaction traces.

public class CC_Interaction : NetworkBehaviour
{
    [Header("References")]
    public CC_Movement movement;
    public CC_CameraController cameraController;
    public CC_CharacterPlayerController playerController;

    [Header("Interaction")]
    public float interactRange = 2.5f;

    [Tooltip("Layers that contain interactables.")]
    public LayerMask interactLayers;

    [Tooltip("Origin used for interaction ray. If null, will use transform.")]
    public Transform interactOrigin;

    // interact
    [SerializeField] private InteractableObject currentInteractable;
    float holdTimer;
    bool holding;
    bool interactWasHeld;
    bool interactUsedUntilRelease;
    bool menuOpenedFromHold;

    void Awake()
    {
        if (movement == null) { movement = GetComponent<CC_Movement>(); }
        if (cameraController == null) { cameraController = GetComponent<CC_CameraController>(); }
        if (playerController == null) { playerController = GetComponent<CC_CharacterPlayerController>(); }
    }

    bool IsLocallyControlled()
    {
        return movement != null && movement.IsLocallyControlled();
    }

    protected virtual InteractableObject GetLookInteractable()
    {
        Transform origin =
            interactOrigin != null ? interactOrigin :
            cameraController != null && cameraController.cinemachineCameraTarget != null ? cameraController.cinemachineCameraTarget :
            transform;

        Ray ray = new Ray(origin.position, origin.forward);
        Debug.DrawRay(origin.position, origin.forward * interactRange, Color.cyan);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, interactRange, interactLayers))
        {
            return hit.collider.GetComponent<InteractableObject>();
        }

        return null;
    }

    public bool HasLookInteractable()
    {
        if (!IsLocallyControlled()) { return false; }
        return GetLookInteractable() != null;
    }

    public virtual void TickInteract(bool interactHeld)
    {
        if (!IsLocallyControlled()) { return; }

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
                currentInteractable.CancelHold(gameObject);
            }

            currentInteractable = null;
            holdTimer = 0f;
            holding = false;
            menuOpenedFromHold = false;
        }

        currentInteractable = current;

        // nothing to interact with
        if (currentInteractable == null)
        {
            holdTimer = 0f;
            holding = false;
            menuOpenedFromHold = false;
            return;
        }

        // one shot used up etc
        if (!currentInteractable.CanInteract())
        {
            holdTimer = 0f;
            holding = false;
            menuOpenedFromHold = false;
            return;
        }

        // interact menu objects
        // tap uses first action, hold opens menu
        if (currentInteractable.UsesInteractMenu())
        {
            if (pressed)
            {
                holding = true;
                holdTimer = 0f;
                menuOpenedFromHold = false;
                currentInteractable.BeginHold(gameObject);
            }

            if (interactHeld && holding)
            {
                float required = currentInteractable.GetMenuHoldTime();
                holdTimer += Time.deltaTime;

                float progress01 = Mathf.Clamp01(holdTimer / required);
                currentInteractable.HoldProgress(gameObject, progress01);

                if (!menuOpenedFromHold && holdTimer >= required)
                {
                    menuOpenedFromHold = true;

                    if (playerController != null)
                    {
                        playerController.OpenInteractMenu(currentInteractable, gameObject);
                    }

                    interactUsedUntilRelease = true;
                    currentInteractable.HoldProgress(gameObject, 0f);

                    currentInteractable = null;
                    holdTimer = 0f;
                    holding = false;
                }
            }

            if (released)
            {
                // if we released before the hold completed, treat it as a press interact
                if (!menuOpenedFromHold)
                {
                    if (holding)
                    {
                        currentInteractable.CancelHold(gameObject);
                    }

                    currentInteractable.Interact(gameObject);
                    interactUsedUntilRelease = true;
                }

                holdTimer = 0f;
                holding = false;
                menuOpenedFromHold = false;
            }

            return;
        }

        // if tap interact, only fire once
        if (!currentInteractable.requiresHold)
        {
            if (pressed)
            {
                currentInteractable.Interact(gameObject);

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
                currentInteractable.CancelHold(gameObject);
            }

            holdTimer = 0f;
            holding = false;
            currentInteractable.HoldProgress(gameObject, 0f);
            return;
        }

        // start hold on press
        if (pressed)
        {
            holding = true;
            holdTimer = 0f;
            currentInteractable.BeginHold(gameObject);
        }

        // if we're holding, progress it
        if (holding)
        {
            float required = Mathf.Max(0.01f, currentInteractable.GetHoldTime());
            holdTimer += Time.deltaTime;

            float progress01 = Mathf.Clamp01(holdTimer / required);
            currentInteractable.HoldProgress(gameObject, progress01);

            if (holdTimer >= required)
            {
                currentInteractable.Interact(gameObject);

                // stop interacting until release
                interactUsedUntilRelease = true;

                // clear state so we dont immediately restart hold
                currentInteractable = null;
                holdTimer = 0f;
                holding = false;
            }
        }
    }
}