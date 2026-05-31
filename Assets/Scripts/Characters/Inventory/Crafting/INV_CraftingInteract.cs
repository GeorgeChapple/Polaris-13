using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Made By: Jason Lodge.
// Summary: Crafting interact bridge.
// Lives on the crafting station object and tells INV_Crafting
// on the player to set up and open the crafting menu.

public class INV_CraftingInteract : NetworkBehaviour
{
    [Header("Crafting")]
    [Tooltip("True if this interaction counts as using a crafting station.")]
    [SerializeField] private bool countsAsCraftingStation = true;

    [SerializeField] private NET_AnimatorSync netAnimSync;
    [SerializeField] private AUD_SFX sfx;

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

        crafting.SetupEverything(countsAsCraftingStation, this);
    }

    public void OnCraft()
    {
        if (netAnimSync != null) 
        {
            netAnimSync.SetTrigger("Craft");
            StartCoroutine(WaitAndReset());
        }
        if (sfx != null) { sfx.PlaySound("Craft"); }
    }
    IEnumerator WaitAndReset()
    {
        yield return new WaitForSeconds(1);
        if (netAnimSync != null) { netAnimSync.ResetTrigger("Craft"); }
        StopAllCoroutines();
        yield return null;
    }
}