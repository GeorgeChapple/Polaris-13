using UnityEngine;

[CreateAssetMenu(fileName = "SP_Cluster", menuName = "Scriptable Objects/SP_Cluster")]
public class SP_Cluster : ScriptableObject
{
    public GameObject[] prefabs;
    public Vector2 triggerRadius;
    public Vector2 spawnRadius;
    public Vector2 spaceRadius;
    public int minSpawn;
    public int maxSpawn;
    public Color colour;
}
