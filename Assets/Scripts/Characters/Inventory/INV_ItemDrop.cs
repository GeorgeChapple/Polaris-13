using UnityEngine;

public class INV_ItemDrop : MonoBehaviour
{
    [SerializeField] private INV_Item item;

    // called by player interact script(unity event)
    public void TryAddToInventory()
    {
        // check if can be added using inventory
        // (needs to be instanced per player so check the inventory of the player trying to pick it up)        
    }
}
