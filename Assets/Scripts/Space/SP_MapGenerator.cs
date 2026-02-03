using UnityEngine;

public class SP_MapGenerator : MonoBehaviour
{
    public Vector3 mapSize;
    public GameObject[] biomes;
    public int seed;

    private void Awake()
    {
        Random.InitState(seed);
    }

    
}
