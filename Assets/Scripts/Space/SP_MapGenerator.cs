using System;
using System.Collections.Generic;
using Unity.VisualScripting;
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
        public ClusterSpawn[] clusterSpawns;
    }

    [Serializable]
    public struct ClusterSpawn
    {
        public SP_Cluster clusterPrefab;
        public int minSpawn;
        public int maxSpawn;
    }

    public struct Cluster
    {
        public GameObject prefab;
        public Vector3 position;
        public float radius;
        public Color colour;

        public Cluster(GameObject _prefab, Vector3 _position, float _radius, Color _colour)
        {
            prefab = _prefab;
            position = _position;
            radius = _radius;
            colour = _colour;
        }
    }

    private List<Cluster> GenerateMap(Biome biome)
    {
        List<Cluster> newClusters = new List<Cluster>();
        foreach (ClusterSpawn spawn in biome.clusterSpawns)
        {
            for (int i = 0; i < UnityEngine.Random.Range(spawn.minSpawn, spawn.maxSpawn); i++)
            {
                SP_Cluster prefab = spawn.clusterPrefab;
                
            }
        }

        return newClusters;
    }
}
