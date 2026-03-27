using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

public class SP_SpaceJunk : NetworkBehaviour
{
    [SerializeField] private int maxDebris = 20;
    [HideInInspector] public RS_Move rocket; 
    public List<SP_SpawnSettings> spawners = new List<SP_SpawnSettings>();
    [HideInInspector] public List<GameObject> foundObjects = new List<GameObject>();
    public Dictionary<GameObject, Vector3> debris = new Dictionary<GameObject, Vector3>();
    public Vector3 spaceBounds = new Vector3(20, 20, 20);

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
        rocket = FindFirstObjectByType<RS_Move>(); 
    }

    void Update()
    {
        if (!IsServer) return;
        if (rocket == null) return;

        if (rocket.speed > 0.1f)
        {
            SpawnSpaceObjects();
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

    private void SpawnSpaceObjects()
    {
        foreach (SP_SpawnSettings spawner in spawners)
        {
            if ((debris.Count < maxDebris || spawner.ignoreMaxDebris) && !spawner.spawning)
            {
                StartCoroutine(WaitSpawnObject(spawner));
            }
        }
    }

    private IEnumerator WaitSpawnObject(SP_SpawnSettings spawner)
    {
        spawner.spawning = true;

        float t = 0;
        float w = Random.Range(spawner.spawnTime.x, spawner.spawnTime.y);
        while (t < w)
        {
            t += Time.deltaTime;
            yield return null;
        }

        int maxValue = 0;
        foreach (SP_SpawnSettings.SpaceObject obj in spawner.spaceObjects)
        {
            maxValue += obj.probability; 
            yield return null;
        }


        int randomValue = Random.Range(1, maxValue + 1);
        int objectIndex = 0;
        foreach (SP_SpawnSettings.SpaceObject obj in spawner.spaceObjects)
        {
            if (randomValue <= obj.probability)
            {
                break;
            }
            objectIndex++;
            yield return null;
        }

        Vector2 spawnPosition = GetRandomSpawnPosition(spawner.spawnbounds);

        GameObject newDebris = Instantiate(
                    spawner.spaceObjects[objectIndex].prefab,
                    new Vector3(
                        spawnPosition.x,
                        spawnPosition.y,
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

        spawner.spawning = false;
    }

    private Vector2 GetRandomSpawnPosition(Vector4 bounds)
    {
        Vector2 pos = new Vector2();

        pos.x = Random.Range(0, bounds.x / 2);
        pos.y = Random.Range(0, bounds.y / 2);

        pos.x = Mathf.Clamp(pos.x, bounds.z / 2, bounds.x);
        pos.y = Mathf.Clamp(pos.y, bounds.w / 2, bounds.x);

        int randomNegative = Random.Range(0, 2);
        if (randomNegative == 0)
        {
            pos.x *= -1;
        }
        randomNegative = Random.Range(0, 2);
        if (randomNegative == 0)
        {
            pos.y *= -1;
        }

        return pos;
    }

    private void MoveDebris()
    {
        List<GameObject> debrisObjects = new List<GameObject>(debris.Keys);

        foreach (GameObject obj in debrisObjects)
        {
            if (obj == null) continue;

            Rigidbody rb = obj.GetComponent<Rigidbody>();

            if (rb == null) continue;

            Vector3 objDirection = (Vector3.back + debris[obj] - rocket.worldDirection).normalized * rocket.speed;
            rb.MovePosition(rb.position + objDirection * Time.deltaTime);
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

    private void SortProbabilities(SP_SpawnSettings spawner)
    {
        spawner.spaceObjects = MergeSort(spawner.spaceObjects);
    }

    private List<SP_SpawnSettings.SpaceObject> MergeSort(List<SP_SpawnSettings.SpaceObject> objects)
    {
        if (!objects.Any()) return new List<SP_SpawnSettings.SpaceObject>();

        int mid = objects.Count / 2;

        List<SP_SpawnSettings.SpaceObject> left = GetListSegment(objects, 0, mid);
        List<SP_SpawnSettings.SpaceObject> right = GetListSegment(objects, mid + 1, objects.Count - 1);

        if (left.Count > 1)
        {
            MergeSort(left);
        }
        if (right.Count > 1)
        {
            MergeSort(right);
        }

        MergeList(objects, left, right);

        return objects;
    }

    private void MergeList(List<SP_SpawnSettings.SpaceObject> list, List<SP_SpawnSettings.SpaceObject> left, List<SP_SpawnSettings.SpaceObject> right)
    {
        int i, j, k;
        i = j = k = 0;
        SP_SpawnSettings.SpaceObject obj;

        while (i < left.Count && j < right.Count)
        {
            if (left[i].probability >= right[j].probability)
            {
                obj = left[i++];
            }
            else
            {
                obj = right[j++];
            }
            list[k++] = obj;
        }

        while (i < left.Count)
        {
            list[k++] = left[i++];
        }
        while (j < right.Count)
        {
            list[k++] = left[j++];
        }
    }

    private List<SP_SpawnSettings.SpaceObject> GetListSegment(List<SP_SpawnSettings.SpaceObject> list, int startIndex, int endIndex)
    {
        List<SP_SpawnSettings.SpaceObject> newList = new List<SP_SpawnSettings.SpaceObject>();
        for (int i = startIndex; i <= endIndex;)
        {
            newList.Add(list[i]);
        }
        return newList;
    }

    private void OnDrawGizmos()
    {
        DrawBox(Vector3.zero, transform.rotation, spaceBounds, Color.red);
        foreach (SP_SpawnSettings spawner in spawners)
        {
            if (spawner != null)
            {
                DrawBox(new Vector3(0, 0, spaceBounds.z / 2), transform.rotation, new Vector2(spawner.spawnbounds.x, spawner.spawnbounds.y), spawner.gizmoColour);
                DrawBox(new Vector3(0, 0, spaceBounds.z / 2), transform.rotation, new Vector2(spawner.spawnbounds.z, spawner.spawnbounds.w), spawner.gizmoColour);
            }
        }
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