using UnityEngine;
using System.Collections.Generic;
using System;

// Script By : George Chapple
// Summary   : Space spawner properties

[CreateAssetMenu(fileName = "SpaceSpawnSettings", menuName = "Space/SP_SpawnSettings")]
public class SP_SpawnSettings : ScriptableObject
{
    public string spawnerName = "New Spawner";
    [Tooltip("Colour indicator for spawn bounds")]
    public Color gizmoColour = Color.white;
    [Tooltip("Makes spawner ignore the max debris count")]
    public bool ignoreMaxDebris = false;
    [Tooltip("Maximum debris the spawner is allowed to spawn up to (capped by the space manager maximum debris count)")]
    public int maxDebris = 10;
    [Tooltip("If set, spawner will have a limit of how many objects can spawn")]
    public bool limited = false;
    [Tooltip("Limited amount of objects that can be spawned (unused if limited is not set to true)")]
    public int limit = 10;
    [Tooltip("X/Y - Spawn Bounds\nZ/W - Negative Space X and Y (Set this higher to prevent objects spawning in front of ship)")]
    public Vector4 spawnbounds = new Vector4(100, 100, 0, 0);
    [Tooltip("Random range for spawn time between space objects.")]
    public Vector2 spawnTime = new Vector2(0.2f, 0.4f);
    [Tooltip("Space object prefabs and their spawn probabilities.")]
    public List<SpaceObject> spaceObjects = new List<SpaceObject>();

    [Serializable]
    public struct SpaceObject
    {
        [Tooltip("Space Object Prefab")]
        public GameObject prefab;
        [Tooltip("Spawner will generate a random number between 1 and the sum of all probabilities, then it will check if it is less than each probability value from lowest to highest and select the object to spawn accordingly.")]
        public int probability;
    }
}
