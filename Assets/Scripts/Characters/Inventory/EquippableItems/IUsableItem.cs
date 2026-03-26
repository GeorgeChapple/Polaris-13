using UnityEngine;

public interface IUsableItem
{
    void OnUse();
    void WireUp(GameObject player, string itemId);
}
