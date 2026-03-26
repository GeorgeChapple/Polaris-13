using System.Collections.Generic;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Handles Chest Capabilities
// Lives on Chest Object, gets player ref and sets up chest inventory ui using INV_Inventory on player.
// Sets up chest inventory grid on player.
// Holds its own items on host as host runs procedural gen.
// 

public class INV_Chest : MonoBehaviour
{
    List<INV_Inventory.ItemInstance> items = new List<INV_Inventory.ItemInstance>();

    public void SetupEverything(GameObject player)
    {

    }
}
