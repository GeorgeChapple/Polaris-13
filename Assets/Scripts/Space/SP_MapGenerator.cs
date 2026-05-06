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
        clusters = GenerateMap(biomes[0]);
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
                Vector3 newPosition = Vector3.zero;
                float spaceRadius = UnityEngine.Random.Range(prefab.spaceRadius.x, prefab.spaceRadius.y);
                bool checkValidSpawn;
                newPosition = new Vector3(
                        UnityEngine.Random.Range((mapSize.x / 2f) * -1f, (mapSize.x / 2f)),
                        UnityEngine.Random.Range((mapSize.y / 2f) * -1f, (mapSize.y / 2f)),
                        UnityEngine.Random.Range((mapSize.z / 2f) * -1f, (mapSize.z / 2f))
                        );
                //while (true)
                //{
                //    checkValidSpawn = false;
                //    newPosition = new Vector3(
                //        UnityEngine.Random.Range(-1, 1) * (mapSize.x / 2), 
                //        UnityEngine.Random.Range(-1, 1) * (mapSize.y / 2), 
                //        UnityEngine.Random.Range(-1, 1) * (mapSize.z / 2)
                //        );
                //    foreach (Cluster cluster in newClusters)
                //    {
                //        float distance = (cluster.position - newPosition).magnitude;
                //        if (distance < spaceRadius)
                //        {
                //            checkValidSpawn = true;
                //            break;
                //        }
                //    }
                //    if (!checkValidSpawn)
                //    {
                //        break;
                //    }
                //}
                for (int j = 0; j < UnityEngine.Random.Range(prefab.minSpawn, prefab.maxSpawn); j++)
                {
                    Vector3 offset = new Vector3(
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-1f, 1f)
                        );

                    offset += newPosition;
                    offset = (newPosition - offset).normalized;
                    offset *= UnityEngine.Random.Range(prefab.spawnRadius.x, prefab.spawnRadius.y);

                    Cluster newCluster = new Cluster(
                        prefab.prefab,
                        newPosition + offset,
                        UnityEngine.Random.Range(prefab.triggerRadius.x, prefab.triggerRadius.y),
                        prefab.colour
                        );
                    newClusters.Add(newCluster);
                }
            }
        }
        return newClusters;
    }

    private void OnDrawGizmos()
    {
        CustomGizmos.DrawBox(Vector3.zero, transform.rotation, mapSize, Color.yellow);
        foreach (Cluster cluster in clusters)
        {
            Color gizmoColour = cluster.colour * new Color(1, 1, 1, 0.5f);
            Gizmos.color = gizmoColour;
            Gizmos.DrawSphere(cluster.position, cluster.radius);
        }
    }
}
