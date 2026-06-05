using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.VFX;
using Unity.Netcode;

public class SP_MapGenerator : NetworkBehaviour
{
    public Vector3 mapSize;
    public Biome[] biomes;
    public List<ClusterManager> clusters = new List<ClusterManager>();
    public NetworkVariable<int> seed = new NetworkVariable<int>();
    public Texture2D positionData;
    private int spawned = 0;
    private VisualEffect mapEffect;
    [SerializeField] private VisualEffect shipEffect;
    [SerializeField] private RS_Move ship;
    [SerializeField] private Transform x_Line;
    [SerializeField] private Transform y_Line;
    [SerializeField] private Transform z_Line;

    [SerializeField] private float movingTargetSpeed = 100f;
    private Vector3 moveTargetPositive;
    private Vector3 moveTargetNegative;
    private bool movingTarget;


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
        public float colourBrightness;
        public bool spawned;

        public Cluster(GameObject _prefab, Vector3 _position, float _radius, Color _colour, float _colourBrightness, bool _spawned)
        {
            prefab = _prefab;
            position = _position;
            radius = _radius;
            colour = _colour;
            colourBrightness = _colourBrightness;
            spawned = _spawned;
        }
    }

    private void Awake()
    {
        mapEffect = GetComponent<VisualEffect>();
    }

    private void Start()
    {
        if (IsServer){
            seed.Value = UnityEngine.Random.Range(0, 2147483647);
        }
        UnityEngine.Random.InitState(seed.Value);
        clusters = GenerateMap(biomes[0]);
        UpdateTextureData();
        if (IsServer) {
            StartCoroutine(CheckShipPosition());
        }
        //if (save)
        //{
        //    File.WriteAllBytes("Assets/Shaders/Untitled.png", positionData.EncodeToPNG());
        //    save = false;
        //}
    }

    private void Update()
    {
        shipEffect.SetVector3("_position", ClampVector(ship.worldPosition.Value, mapSize));
        shipEffect.SetVector3("_rotation", Quaternion.LookRotation(ship.worldDirectionNetworked.Value).eulerAngles + new Vector3(-90, 0, 0));
        UpdateTargetLines();
        UpdateTargetPosition();
    }

    private IEnumerator CheckShipPosition() {
        while (true) {
            for (int i = 0; i < clusters.Count; i++) {
                for (int j = 0; j < clusters[i].clusterGroup.Count; j++) {
                    Cluster cl = clusters[i].clusterGroup[j];
                    float magnitude = (ship.worldPosition.Value - cl.position).magnitude;
                    if (magnitude < cl.radius && !cl.spawned) {
                        Debug.Log("AHH");
                        if (cl.prefab != null) {
                           GameObject newObj = Instantiate(cl.prefab);
                           NetworkObject netObj = newObj.GetComponent<NetworkObject>();
                            if (netObj != null && !netObj.IsSpawned)
                            {
                                netObj.Spawn();
                            }
                        }
                        cl.spawned = true;
                    }
                    else if (magnitude > cl.radius && cl.spawned) {
                        cl.spawned = false;
                    }
                    yield return null;
                }
                yield return null;
            }
            yield return null;
        }
    }

    private void UpdateTargetLines() {
        Vector3 clampedTarget = ClampVector(ship.targetPosition.Value, mapSize);
        x_Line.position = new Vector3(0, clampedTarget.y, clampedTarget.z);
        y_Line.position = new Vector3(clampedTarget.x, 0, clampedTarget.z);
        z_Line.position = new Vector3(clampedTarget.x, clampedTarget.y, 0);
        x_Line.GetComponent<LineRenderer>().SetPosition(0, transform.position + x_Line.position + new Vector3(-0.5f, 0, 0));
        x_Line.GetComponent<LineRenderer>().SetPosition(1, transform.position + x_Line.position + new Vector3(0.5f, 0, 0));
        y_Line.GetComponent<LineRenderer>().SetPosition(0, transform.position + y_Line.position + new Vector3(0, -0.5f, 0));
        y_Line.GetComponent<LineRenderer>().SetPosition(1, transform.position + y_Line.position + new Vector3(0, 0.5f, 0));
        z_Line.GetComponent<LineRenderer>().SetPosition(0, transform.position + z_Line.position + new Vector3(0, 0, -0.5f));
        z_Line.GetComponent<LineRenderer>().SetPosition(1, transform.position + z_Line.position + new Vector3(0, 0, 0.5f));
    }

    private void UpdateTargetPosition() {
        if (!IsServer) { return; }
        if (!(ship.targetPosition.Value.x > mapSize.x / 2)) {
            ship.targetPosition.Value += new Vector3(moveTargetPositive.x, 0, 0) * Time.deltaTime * movingTargetSpeed;
        }
        if (!(ship.targetPosition.Value.y > mapSize.y / 2)) {
            ship.targetPosition.Value += new Vector3(0, moveTargetPositive.y, 0) * Time.deltaTime * movingTargetSpeed;
        }
        if (!(ship.targetPosition.Value.z > mapSize.z / 2)) {
            ship.targetPosition.Value += new Vector3(0, 0, moveTargetPositive.z) * Time.deltaTime * movingTargetSpeed;
        }
        if (!(ship.targetPosition.Value.x < mapSize.x / 2 * -1)) {
            ship.targetPosition.Value += new Vector3(moveTargetNegative.x, 0, 0) * Time.deltaTime * movingTargetSpeed;
        }
        if (!(ship.targetPosition.Value.y < mapSize.y / 2 * -1)) {
            ship.targetPosition.Value += new Vector3(0, moveTargetNegative.y, 0) * Time.deltaTime * movingTargetSpeed;
        }
        if (!(ship.targetPosition.Value.z < mapSize.z / 2 * -1)) {
            ship.targetPosition.Value += new Vector3(0, 0, moveTargetNegative.z) * Time.deltaTime * movingTargetSpeed;
        }
    }

    private void UpdateTextureData()
    {
        Texture2D newPositionData = new Texture2D(spawned, 4, TextureFormat.RGBA32_SIGNED, false);
        newPositionData.filterMode = FilterMode.Point;
        int i = 0;
        foreach (ClusterManager manager in clusters)
        {
            foreach (Cluster cluster in manager.clusterGroup)
            {
                newPositionData.SetPixel(i, 0, PositionToColour(cluster.position, mapSize));
                newPositionData.SetPixel(i, 1, RadiusToColour(cluster.radius, mapSize));
                newPositionData.SetPixel(i, 2, cluster.colour);
                newPositionData.SetPixel(i, 3, Color.white * (cluster.colourBrightness / 100f));
                i++;
            }
        }
        newPositionData.Apply();
        positionData = newPositionData;
        mapEffect.SetTexture("_positionData", positionData);
        mapEffect.SetInt("_spawnCount", spawned);
    }

    private Vector3 ClampVector(Vector3 position, Vector3 bounds)
    {
        Vector3 newPosition = new Vector3(
            position.x / bounds.x,
            position.y / bounds.y,
            position.z / bounds.z
            );
        return newPosition;
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

    private Color RadiusToColour(float radius, Vector3 bounds)
    {
        float diameter = radius * 2;
        Color newScale = new Color(
            diameter / bounds.x,
            diameter / bounds.y,
            diameter / bounds.z,
            1f
            );
        return newScale;
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
                        prefab.colour,
                        prefab.colourBrightness,
                        false
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

    

    [Rpc(SendTo.Server)]
    public void SetHighMoveAmountXPositiveRpc() {
        moveTargetPositive = new Vector3(1, moveTargetPositive.y, moveTargetPositive.z);
    }

    [Rpc(SendTo.Server)]
    public void SetHighMoveAmountYPositiveRpc() {
        moveTargetPositive = new Vector3(moveTargetPositive.x, 1, moveTargetPositive.z);
    }

    [Rpc(SendTo.Server)]
    public void SetHighMoveAmountZPositiveRpc() {
        moveTargetPositive = new Vector3(moveTargetPositive.x, moveTargetPositive.y, 1);
    }

    [Rpc(SendTo.Server)]
    public void SetLowMoveAmountXPositiveRpc() {
        moveTargetPositive = new Vector3(0, moveTargetPositive.y, moveTargetPositive.z);
    }

    [Rpc(SendTo.Server)]
    public void SetLowMoveAmountYPositiveRpc() {
        moveTargetPositive = new Vector3(moveTargetPositive.x, 0, moveTargetPositive.z);
    }

    [Rpc(SendTo.Server)]
    public void SetLowMoveAmountZPositiveRpc() {
        moveTargetPositive = new Vector3(moveTargetPositive.x, moveTargetPositive.y, 0);
    }

    [Rpc(SendTo.Server)]
    public void SetHighMoveAmountXNegativeRpc() {
        moveTargetNegative = new Vector3(-1, moveTargetNegative.y, moveTargetNegative.z);
    }

    [Rpc(SendTo.Server)]
    public void SetHighMoveAmountYNegativeRpc() {
        moveTargetNegative = new Vector3(moveTargetNegative.x, -1, moveTargetNegative.z);
    }

    [Rpc(SendTo.Server)]
    public void SetHighMoveAmountZNegativeRpc() {
        moveTargetNegative = new Vector3(moveTargetNegative.x, moveTargetNegative.y, -1);
    }

    [Rpc(SendTo.Server)]
    public void SetLowMoveAmountXNegativeRpc() {
        moveTargetNegative = new Vector3(0, moveTargetNegative.y, moveTargetNegative.z);
    }

    [Rpc(SendTo.Server)]
    public void SetLowMoveAmountYNegativeRpc() {
        moveTargetNegative = new Vector3(moveTargetNegative.x, 0, moveTargetNegative.z);
    }

    [Rpc(SendTo.Server)]
    public void SetLowMoveAmountZNegativeRpc() {
        moveTargetNegative = new Vector3(moveTargetNegative.x, moveTargetNegative.y, 0);
    }

    private void OnDrawGizmosSelected()
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
