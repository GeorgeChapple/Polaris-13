using UnityEngine;

public class SP_DestroyJunk : MonoBehaviour
{
    private SP_SpaceJunk spaceManager;

    private void Awake()
    {
        spaceManager = FindFirstObjectByType<SP_SpaceJunk>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!spaceManager.foundObjects.Contains(this.gameObject)) {
            spaceManager.debris.Remove(this.gameObject);
            Destroy(this.gameObject);
        }
    }
}
