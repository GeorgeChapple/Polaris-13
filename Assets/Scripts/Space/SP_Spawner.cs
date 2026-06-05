using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Script By : George Chapple
// Summary   : Spawns Space Objects

public class SP_Spawner : NetworkBehaviour
{
    public SP_SpawnSettings settings;
    private SP_SpaceManager spaceManager;
    private float timeLimit;
    private float timer = 0;
    private int lifetimeSpawned = 0;

    private Dictionary<INV_Item, float> cahcedDebrisItemProbabilites = new Dictionary<INV_Item, float>();

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
        spaceManager = FindFirstObjectByType<SP_SpaceManager>();
        cahcedDebrisItemProbabilites = GetItemProbabilites();
        timeLimit = GetRandomTimeLimit(settings);
    }

    private void Update()
    {
        if (spaceManager == null) { return; }
        if (spaceManager.rocket == null) { return; }
        if (settings == null) { return; }
        if (settings.spaceObjects == null || settings.spaceObjects.Count == 0) { return; }

        if (spaceManager.rocket.speed.Value > 0.1f)
        {
            if (timer < timeLimit)
            {
                timer += Time.deltaTime;
            }
            else if (settings.limited && lifetimeSpawned >= settings.limit)
            {
                // Safety Check
                if (!IsServer)
                {
                    return;
                }

                if (spaceManager.spawners.ContainsKey(this)) {
                    spaceManager.spawners.Remove(this);
                }

                // Despawn/destroy spawners
                NetworkObject netObj = GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                if (settings.ignoreMaxDebris || !spaceManager.maxDebrisReached && spaceManager.spawners[this] < settings.maxDebris)
                {
                    Vector2 spawnPosition = GetRandomSpawnPosition(settings.spawnbounds);
                    int prefabIndex = GetRandomPrefabIndex(settings);

                    if (prefabIndex < 0 || prefabIndex >= settings.spaceObjects.Count)
                    {
                        return;
                    }

                    if (settings.spaceObjects[prefabIndex].prefab == null)
                    {
                        return;
                    }

                    List<GameObject> toSpawn = new List<GameObject>();

                    if (settings.spaceObjects[prefabIndex].prefab.CompareTag("RBPOI"))
                    {
                        for (int i = 0; i < settings.spaceObjects[prefabIndex].prefab.transform.childCount; i++)
                        {
                            toSpawn.Add(settings.spaceObjects[prefabIndex].prefab.transform.GetChild(i).gameObject);
                        }
                    }
                    else
                    {
                        toSpawn.Add(settings.spaceObjects[prefabIndex].prefab);
                    }

                    foreach (GameObject prefab in toSpawn)
                    {
                        if (prefab == null) { continue; }

                        GameObject newDebris = Instantiate(
                                    prefab,
                                    new Vector3(
                                        spawnPosition.x + prefab.transform.position.x,
                                        spawnPosition.y + prefab.transform.position.y,
                                        spaceManager.spaceBounds.z / 2 + prefab.transform.position.z
                                    ),
                                    transform.rotation
                                );

                        SP_SpaceJunk junkComponent = newDebris.GetComponent<SP_SpaceJunk>();
                        if (junkComponent != null)
                        {
                            junkComponent.spawner = this;
                            junkComponent.originDirection = spaceManager.rocket.worldDirectionNetworked.Value;
                            junkComponent.moveMult = 1;
                        }

                        newDebris.transform.rotation = prefab.transform.rotation;

                        NetworkObject netObj = newDebris.GetComponent<NetworkObject>();
                        if (netObj != null && !netObj.IsSpawned)
                        {
                            netObj.Spawn();
                        }

                        // items should be initialised after network spawn to ensure is server gates dont prevent
                        INV_ItemDrop itemDrop = newDebris.GetComponent<INV_ItemDrop>();
                        if (itemDrop != null)
                        {
                            itemDrop.Init(GetRandomDebrisItem());
                        }

                        spaceManager.debrisCount++;
                    }

                    timer = 0;
                    timeLimit = GetRandomTimeLimit(settings);
                    if (settings.limited)
                    {
                        lifetimeSpawned++;
                    }
                }
            }
        }
    }

    private float GetRandomTimeLimit(SP_SpawnSettings settings)
    {
        if (settings == null) { return 0f; }

        return UnityEngine.Random.Range(settings.spawnTime.x, settings.spawnTime.y);
    }

    private int GetRandomPrefabIndex(SP_SpawnSettings settings)
    {
        if (settings == null) { return -1; }
        if (settings.spaceObjects == null || settings.spaceObjects.Count == 0) { return -1; }

        float totalWeight = 0f;

        for (int i = 0; i < settings.spaceObjects.Count; i++)
        {
            if (settings.spaceObjects[i].prefab == null) { continue; }

            totalWeight += Mathf.Max(0f, settings.spaceObjects[i].probability);
        }

        if (totalWeight <= 0f) { return 0; }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < settings.spaceObjects.Count; i++)
        {
            if (settings.spaceObjects[i].prefab == null) { continue; }

            currentWeight += Mathf.Max(0f, settings.spaceObjects[i].probability);

            if (randomValue <= currentWeight)
            {
                return i;
            }
        }

        return settings.spaceObjects.Count - 1;
    }

    private Dictionary<INV_Item, float> GetItemProbabilites()
    {
        // get all item probabilities and skip any we don't need
        Dictionary<INV_Item, float> allItemProbabilities = new Dictionary<INV_Item, float>();

        if (INV_ItemDatabase.Instance == null) { return allItemProbabilities; }
        if (INV_ItemDatabase.Instance.Items == null) { return allItemProbabilities; }

        foreach (INV_Item item in INV_ItemDatabase.Instance.Items)
        {
            if (item == null) { continue; }

            float probability = item.ChanceOfSpawnAsDebris;
            if (probability <= 0) { continue; }

            if (!allItemProbabilities.ContainsKey(item))
            {
                allItemProbabilities.Add(item, probability);
            }
        }

        return allItemProbabilities;
    }

    private INV_Item GetRandomDebrisItem()
    {
        if (cahcedDebrisItemProbabilites == null || cahcedDebrisItemProbabilites.Count == 0)
        {
            cahcedDebrisItemProbabilites = GetItemProbabilites();
        }

        return GetRandomItemFromTable(cahcedDebrisItemProbabilites);
    }

    private INV_Item GetRandomItemFromTable(Dictionary<INV_Item, float> itemTable)
    {
        if (itemTable == null || itemTable.Count == 0) { return null; }

        float totalWeight = 0f;

        foreach (KeyValuePair<INV_Item, float> item in itemTable)
        {
            if (item.Key == null) { continue; }

            totalWeight += Mathf.Max(0f, item.Value);
        }

        if (totalWeight <= 0f) { return null; }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (KeyValuePair<INV_Item, float> item in itemTable)
        {
            if (item.Key == null) { continue; }

            currentWeight += Mathf.Max(0f, item.Value);

            if (randomValue <= currentWeight)
            {
                return item.Key;
            }
        }

        return null;
    }

    private Vector2 GetRandomSpawnPosition(Vector4 bounds)
    {
        Vector2 pos = new Vector2();

        pos.x = UnityEngine.Random.Range(0, bounds.x / 2);
        pos.y = UnityEngine.Random.Range(0, bounds.y / 2);

        if (pos.x < bounds.z / 2 && pos.y < bounds.w / 2)
        {
            if (pos.x > pos.y)
            {
                pos.x = bounds.z / 2;
            }
            else
            {
                pos.y = bounds.w / 2;
            }
        }

        int randomNegative = UnityEngine.Random.Range(0, 2);
        if (randomNegative == 0)
        {
            pos.x *= -1;
        }
        randomNegative = UnityEngine.Random.Range(0, 2);
        if (randomNegative == 0)
        {
            pos.y *= -1;
        }

        return pos;
    }
}