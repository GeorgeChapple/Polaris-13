using UnityEngine;

// Made By: Jason Lodge.
// Summary: Chest interact bridge.
// Lives on the chest inventory object and tells INV_Chest
// on itself to set up and open the inventory with the chest inventory.
public class INV_ChestInteract : MonoBehaviour
{
    public void OnInteractedWith(GameObject interactor)
    {
        if (interactor == null)
        {
            return;
        }

        INV_Chest chest = interactor.GetComponent<INV_Chest>();
        if (chest == null)
        {
            chest = interactor.GetComponentInChildren<INV_Chest>();
        }

        if (chest == null)
        {
            Debug.LogWarning("Could not find INV_Chest on interactor.", interactor);
            return;
        }

        chest.SetupEverything(interactor);
    }
}
