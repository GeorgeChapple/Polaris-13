using Unity.Netcode;
using UnityEngine;

public class DebugSpawner : MonoBehaviour
{
    public bool spawn = false;
    public bool destroy = false;
    public GameObject prefab;

    private void Update()
    {
        if (spawn)
        {
            spawn = false;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        GameObject newGoob = Instantiate(
                            prefab,
                            transform.position + new Vector3(i, j, k),
                            transform.rotation
                            );
                    }
                }
            }
        }
        if ( destroy )
        {
            destroy = false;
            foreach (BD_Goober gb in FindObjectsByType<BD_Goober>(FindObjectsSortMode.None))
            {
                Destroy(gb.gameObject);
            }
        }
    }
}
