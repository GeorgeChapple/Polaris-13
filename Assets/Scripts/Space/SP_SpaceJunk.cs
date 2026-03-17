using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

public class SP_SpaceJunk : NetworkBehaviour
{
    [SerializeField] private int maxDebris = 20;
    [SerializeField] private Vector2 spawnTimeRange = new Vector2(3, 6);
    private float spawnTimeLimit;
    private float spawnTimer;
    [HideInInspector] public RS_Move rocket;
    public List<GameObject> debrisPrefabs = new List<GameObject>();
    [HideInInspector] public List<GameObject> foundObjects = new List<GameObject>();
    public Dictionary<GameObject, Vector3> debris = new Dictionary<GameObject, Vector3>();
    public Vector3 spaceBounds = new Vector3(20, 20, 20);
    public Vector2 spawnBounds = new Vector2(20, 20);

    private void Awake()
    {
        InitialiseComponents();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // only server controls junk spawning and movement
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        InitialiseComponents();
    }

    private void InitialiseComponents()
    {
        spawnTimeLimit = Random.Range(spawnTimeRange.x, spawnTimeRange.y);
        rocket = FindFirstObjectByType<RS_Move>();
    }

    void Update()
    {
        if (!IsServer) return;
        if (rocket == null) return;

        if (rocket.speed > 0.1f)
        {
            SpawnDebris();
        }

        MoveDebris();

        Collider[] colliders = Physics.OverlapBox(transform.position, spaceBounds / 2, transform.rotation);
        foundObjects.Clear();

        foreach (Collider col in colliders)
        {
            foundObjects.Add(col.gameObject);
        }

        CC_Movement[] players = FindObjectsByType<CC_Movement>(FindObjectsSortMode.None);
        foreach (CC_Movement player in players)
        {
            if (!foundObjects.Contains(player.gameObject))
            {
                player.Body.position = GameObject.FindGameObjectsWithTag("SpawnPoint")[player.OwnerClientId].transform.position;
            }
        }
    }

    private void SpawnDebris()
    {
        if (debris.Count < maxDebris)
        {
            if (spawnTimer <= spawnTimeLimit)
            {
                spawnTimer += Time.deltaTime;
            }
            else
            {
                GameObject newDebris = Instantiate(
                    debrisPrefabs[Random.Range(0, debrisPrefabs.Count)],
                    new Vector3(
                        Random.Range(spawnBounds.x / 2 * -1, spawnBounds.x / 2),
                        Random.Range(spawnBounds.y / 2 * -1, spawnBounds.y / 2),
                        spaceBounds.z / 2
                    ),
                    transform.rotation
                );

                newDebris.transform.eulerAngles = Vector3.back;

                NetworkObject netObj = newDebris.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                {
                    netObj.Spawn();
                }

                debris.Add(newDebris, rocket.worldDirection);
                spawnTimeLimit = Random.Range(spawnTimeRange.x, spawnTimeRange.y);
                spawnTimer = 0;

                SP_Junk junk = newDebris.GetComponent<SP_Junk>();
                if (junk != null)
                {
                    junk.objDirection = (Vector3.back + debris[newDebris] - rocket.worldDirection).normalized * rocket.speed;
                }
            }
        }
    }

    private void MoveDebris()
    {
        List<GameObject> debrisObjects = new List<GameObject>(debris.Keys);

        foreach (GameObject obj in debrisObjects)
        {
            if (obj == null) continue;

            SP_Junk junkComponent = obj.GetComponent<SP_Junk>();
            Rigidbody rb = obj.GetComponent<Rigidbody>();

            if (junkComponent == null || rb == null) continue;

            rb.MovePosition(rb.position + junkComponent.objDirection * Time.deltaTime);
        }
    }

    public void RemoveDebris(GameObject obj)
    {
        if (obj == null) { return; }

        if (debris.ContainsKey(obj))
        {
            debris.Remove(obj);
        }
    }

    private void OnDrawGizmos()
    {
        DrawBox(Vector3.zero, transform.rotation, spaceBounds, Color.red);
        DrawBox(new Vector3(0, 0, spaceBounds.z / 2), transform.rotation, spawnBounds, Color.green);
    }

    public void DrawBox(Vector3 pos, Quaternion rot, Vector3 scale, Color c)
    {
        Matrix4x4 m = new Matrix4x4();
        m.SetTRS(pos, rot, scale);

        var point1 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, 0.5f));
        var point2 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, 0.5f));
        var point3 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, -0.5f));
        var point4 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, -0.5f));

        var point5 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, 0.5f));
        var point6 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, 0.5f));
        var point7 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, -0.5f));
        var point8 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, -0.5f));

        Debug.DrawLine(point1, point2, c);
        Debug.DrawLine(point2, point3, c);
        Debug.DrawLine(point3, point4, c);
        Debug.DrawLine(point4, point1, c);

        Debug.DrawLine(point5, point6, c);
        Debug.DrawLine(point6, point7, c);
        Debug.DrawLine(point7, point8, c);
        Debug.DrawLine(point8, point5, c);

        Debug.DrawLine(point1, point5, c);
        Debug.DrawLine(point2, point6, c);
        Debug.DrawLine(point3, point7, c);
        Debug.DrawLine(point4, point8, c);
    }
}