using UnityEngine;

public class TestItemScript : MonoBehaviour, IUsableItem
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
        Debug.Log($"{ownerPlayer.name}: Test Object Fired");
    }
}
