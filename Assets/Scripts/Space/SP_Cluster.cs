using UnityEngine;

[CreateAssetMenu(fileName = "SP_Cluster", menuName = "Scriptable Objects/SP_Cluster")]
public class SP_Cluster : ScriptableObject
{
    GameObject prefab;
    Vector2 triggerRadius;
    Vector2 spawnRadius;
    Vector2 spaceRadius;
    int minSpawn;
    int maxSpawn;
}
