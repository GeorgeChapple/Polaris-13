using System;
using System.Collections.Generic;
using UnityEngine;

public class SP_MapGenerator : MonoBehaviour
{
    public Vector3 mapSize;
    public Biome[] biomes;
    public int seed;

    public List<Cluster> clusters = new List<Cluster>();

    private void Awake()
    {
        UnityEngine.Random.InitState(seed);
    }

    [Serializable]
    public struct Biome
    {
        ClusterSpawn[] clusterSpawns;
    }

    public struct ClusterSpawn
    {
        SP_Cluster clusterPrefab;
        int minSpawn;
        int maxSpawn;
    }

    public struct Cluster
    {
        GameObject prefab;
        Vector3 position;
        float radius;
    }
}
