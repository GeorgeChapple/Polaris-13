using UnityEngine;

public class INV_ItemDrop : MonoBehaviour
{
    [SerializeField] private INV_Item item;

    // called by player interact script
    public void TryAddToInventory()
    {
        // placeholder, need a per player ver
        INV_Inventory inv = FindAnyObjectByType<INV_Inventory>();
        if (inv == null) { return; }

        bool added = inv.TryAddItem(item);
        if (added)
        {
            Destroy(gameObject);
        }
    }
}
