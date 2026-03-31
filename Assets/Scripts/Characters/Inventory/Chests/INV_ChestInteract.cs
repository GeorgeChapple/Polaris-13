using UnityEngine;

// Made By: Jason Lodge.
// Summary: Chest interact bridge.
// Lives on the chest inventory object and tells INV_Chest
// on itself to set up and open the inventory with the chest inventory.
public class INV_ChestInteract : MonoBehaviour
{
    private INV_Chest chest;

    private void Awake()
    {
        chest = GetComponent<INV_Chest>();
        if (chest == null)
        {
            chest = GetComponentInParent<INV_Chest>();
        }
    }

    public void OnInteractedWith(GameObject interactor)
    {
        if (interactor == null)
        {
            return;
        }

        if (chest == null)
        {
            chest = GetComponent<INV_Chest>();
            if (chest == null)
            {
                chest = GetComponentInParent<INV_Chest>();
            }
        }

        if (chest == null)
        {
            return;
        }

        chest.SetupEverything(interactor);
    }
}