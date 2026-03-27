using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SP_SpawnSettings", menuName = "Scriptable Objects/SP_SpawnSettings")]
public class SP_SpawnSettings : ScriptableObject
{
    [Tooltip("Colour indicator for spawn bounds")]
    public Color gizmoColour = Color.white;
    [Tooltip("Spawn Debris Ignoring Max Debris")]
    public bool ignoreMaxDebris = false;
    [Tooltip("X/Y - Spawn Bounds\nZ/W - Negative Space X and Y (Set this higher to prevent objects spawning in front of ship)")]
    public Vector4 spawnbounds = new Vector4(100, 100, 0, 0);
    [Tooltip("Random range for spawn time between space objects.")]
    public Vector2 spawnTime = new Vector2(0.2f, 0.4f);
    [Tooltip("Space object prefabs and their spawn probabilities.")]
    public List<SpaceObject> spaceObjects = new List<SpaceObject>();

    [HideInInspector]
    public bool spawning = false;

    public struct SpaceObject
    {
        [Tooltip("Space Object Prefab")]
        public GameObject prefab;
        [Tooltip("Spawner will generate a random number between 1 and the sum of all probabilities, then it will check if it is less than each probability value from lowest to highest and select the object to spawn accordingly.")]
        public int probability;
    }
}
