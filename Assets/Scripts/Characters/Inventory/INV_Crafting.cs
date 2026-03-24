using System.Collections.Generic;
using UnityEngine;

// Made By: Jason Lodge.
// Summary: Will handle all crafting capabilities.
// Will use the item database and inventory items from interactor
// and set up a list with buttons figuring out which items the player can currently craft.
// 

public class INV_Crafting : MonoBehaviour
{
    public List<INV_Item> craftables = new List<INV_Item>();
    public List<INV_Item> nonCraftables = new List<INV_Item>();

    public void OnInteractedWith(GameObject interactor)
    {
        // set up lists
        craftables.Clear();
        nonCraftables.Clear();

        // set up menu
        INV_Inventory inv = interactor.GetComponent<INV_Inventory>();

        // check for craftables
        foreach (var item in INV_ItemDatabase.Instance.Items)
        {
            bool craftable = false;
            foreach (var craftReq in item.CraftingRequirements)
            {
                foreach (var invItem in inv.Items)
                {
                    if (invItem.data.ItemID == craftReq.item.ItemID) 
                    {
                        if (invItem.quantity >= craftReq.amount)
                        {
                            craftable = true;
                            break;
                        }                    
                    }
                }
            }
            if (craftable) { craftables.Add(item); }
            else { nonCraftables.Add(item); }
        }
    }
}
