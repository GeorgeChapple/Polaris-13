using UnityEngine;

public class CC_INV_ConsumableItem : MonoBehaviour, IUsableItem
{
    private GameObject ownerPlayer;
    private string itemId;

    public void WireUp(GameObject player, string ItemId)
    {
        ownerPlayer = player;
        itemId = ItemId;
    }

    public void OnUse()
    {
        //use item
        INV_PlayerInventoryNet playerInventoryNet = ownerPlayer.GetComponent<INV_PlayerInventoryNet>();
        if (playerInventoryNet != null) 
        {
            playerInventoryNet.RequestUseItemInInventory(itemId);        
        }
    }
}
