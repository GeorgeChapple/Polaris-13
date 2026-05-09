using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;

public class SP_MapGenerator : MonoBehaviour
{
    public Vector3 mapSize;
    public Biome[] biomes;
    public int seed;
    public List<ClusterManager> clusters = new List<ClusterManager>();
    [SerializeField] private GameObject TEMP;
    public Texture2D positionData;
    private int spawned = 0;
    private VisualEffect effect;



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

    [Serializable]
    public struct ClusterManager
    {
        public bool activated;
        public List<Cluster> clusterGroup;

        public ClusterManager(List<Cluster> _clusters)
        {
            activated = false;
            clusterGroup = _clusters;
        }
    }

    [Serializable]
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

    private void Awake()
    {
        effect = GetComponent<VisualEffect>();
        UnityEngine.Random.InitState(seed);
        clusters = GenerateMap(biomes[0]);
    }

    private void FixedUpdate()
    { 
        Texture2D newData = new Texture2D(spawned, 1, TextureFormat.RGBA32_SIGNED, false);
        int i = 0;
        foreach (ClusterManager manager in clusters)
        {
            foreach(Cluster cluster in manager.clusterGroup)
            {
                newData.SetPixel(i, 1, PositionToColour(cluster.position, mapSize));
                i++;
            }
        }
        newData.Apply();
        positionData = newData;
        effect.SetTexture("_positionData", positionData);
        effect.SetInt("_spawnCount", spawned);
    }

    private Color PositionToColour(Vector3 position, Vector3 bounds) 
    {
        Color newPosition = new Color(
            position.x / bounds.x,
            position.y / bounds.y,
            position.z / bounds.z,
            1f
            );
        return newPosition;
    }

    private List<ClusterManager> GenerateMap(Biome biome)
    {
        spawned = 0;
        List<ClusterManager> newClusterManagers = new List<ClusterManager>();
        foreach (ClusterSpawn spawn in biome.clusterSpawns)
        {
            for (int i = 0; i < UnityEngine.Random.Range(spawn.minSpawn, spawn.maxSpawn); i++)
            {
                List<Cluster> newClusters = new List<Cluster>();
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
                        prefab.prefabs[UnityEngine.Random.Range(0, prefab.prefabs.Length)],
                        newPosition + offset,
                        UnityEngine.Random.Range(prefab.triggerRadius.x, prefab.triggerRadius.y),
                        prefab.colour
                        );
                    newClusters.Add(newCluster);
                    spawned++;
                }
                ClusterManager newManager = new ClusterManager(newClusters);
                newClusterManagers.Add(newManager);
            }
        }
        return newClusterManagers;
    }

    private void OnDrawGizmos()
    {
        CustomGizmos.DrawBox(Vector3.zero, transform.rotation, mapSize, Color.yellow);
        foreach (ClusterManager manager in clusters)
        {
            foreach (Cluster cluster in manager.clusterGroup)
            {
                Color gizmoColour = cluster.colour * new Color(1, 1, 1, 0.5f);
                Gizmos.color = gizmoColour;
                Gizmos.DrawSphere(cluster.position, cluster.radius);
            }
        }
    }
}
