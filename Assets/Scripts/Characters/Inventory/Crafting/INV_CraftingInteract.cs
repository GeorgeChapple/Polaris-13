using UnityEngine;

// Made By: Jason Lodge.
// Summary: Crafting interact bridge.
// Lives on the crafting station object and tells INV_Crafting
// on the player to set up and open the crafting menu.

public class INV_CraftingInteract : MonoBehaviour
{
    [Header("Crafting")]
    [Tooltip("True if this interaction counts as using a crafting station.")]
    [SerializeField] private bool countsAsCraftingStation = true;

    public void OnInteractedWith(GameObject interactor)
    {
        if (interactor == null)
        {
            return;
        }

        INV_Crafting crafting = interactor.GetComponent<INV_Crafting>();
        if (crafting == null)
        {
            crafting = interactor.GetComponentInChildren<INV_Crafting>();
        }
        if (crafting == null)
        {
            return;
        }

        crafting.SetupEverything(countsAsCraftingStation);
    }
}