using UnityEngine;

public class SP_DestroyJunk : MonoBehaviour
{
    [HideInInspector] public Quaternion junkRotation;
    [HideInInspector] public Vector3 junkRotationRate;
    private SP_SpaceJunk spaceManager;

    private void Awake()
    {
        junkRotation.eulerAngles = Vector3.one * Random.value * 360;
        junkRotationRate = Vector3.one * Random.value * 10;
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
