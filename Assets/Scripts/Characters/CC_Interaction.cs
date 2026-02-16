using UnityEngine;

// Made by: Jason Lodge
// Summary: Interaction handler.

public class CC_Interaction : MonoBehaviour
{
    [Header("References")]
    public Transform viewTransform;

    [Header("Interaction")]
    public float interactRange = 2.5f;

    [Tooltip("Layers that contain interactables.")]
    public LayerMask interactLayers;

    [Tooltip("Origin used for interaction ray.")]
    public Transform interactOrigin;

    // interact
    InteractableObject currentInteractable;
    float holdTimer;
    bool holding;
    bool interactWasHeld;
    bool interactUsedUntilRelease;

    public virtual void TickInteract(bool interactHeld)
    {
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
        }

        currentInteractable = current;

        // nothing to interact with
        if (currentInteractable == null)
        {
            holdTimer = 0f;
            holding = false;
            return;
        }

        // one shot used up etc
        if (!currentInteractable.CanInteract())
        {
            holdTimer = 0f;
            holding = false;
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

    protected virtual InteractableObject GetLookInteractable()
    {
        Transform origin =
            interactOrigin != null ? interactOrigin :
            viewTransform != null ? viewTransform :
            transform;

        Ray ray = new Ray(origin.position, origin.forward);
        Debug.DrawRay(origin.position, origin.forward * interactRange, Color.cyan);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, interactRange, interactLayers, QueryTriggerInteraction.Collide))
        {
            return hit.collider.GetComponentInParent<InteractableObject>();
        }

        return null;
    }
}
