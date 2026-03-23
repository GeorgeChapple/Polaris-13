using UnityEngine;

public class TestItemScript : MonoBehaviour, IUsableItem
{
    public void OnUse()
    {
        Debug.Log("Test Object Fired");
    }
}
