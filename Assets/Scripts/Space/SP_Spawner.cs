using System;
using Unity.Multiplayer.Center.NetcodeForGameObjectsExample.DistributedAuthority;
using Unity.Netcode;
using UnityEngine;

public class SP_Spawner : NetworkBehaviour
{
    [HideInInspector] public SP_SpawnSettings settings;
    private SP_SpaceManager spaceManager;
    private float timeLimit;
    private float timer = 0;
    private int lifetimeSpawned = 0;

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
    }

    private void Update()
    {
        if (timer < timeLimit)
        {
            timer += Time.deltaTime;
        }
        else if (settings.limited && lifetimeSpawned > settings.limit )
        {
            // Safety Check
            if (!IsServer)
            {
                return;
            }

            spaceManager.spawners.Remove(this);

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

                GameObject newDebris = Instantiate(
                            settings.spaceObjects[prefabIndex].prefab,
                            new Vector3(
                                spawnPosition.x,
                                spawnPosition.y,
                                spaceManager.spaceBounds.z / 2
                            ),
                            transform.rotation
                        );

                SP_SpaceJunk junkComponent = newDebris.GetComponent<SP_SpaceJunk>();
                if (junkComponent != null)
                {
                    junkComponent.spawner = this;
                }

                newDebris.transform.eulerAngles = Vector3.back;

                NetworkObject netObj = newDebris.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                {
                    netObj.Spawn();
                }

                spaceManager.debris.Add(newDebris, spaceManager.rocket.worldDirection);

                timer = 0;
                timeLimit = GetRandomTimeLimit(settings);
                if (settings.limited)
                {
                    lifetimeSpawned++;
                }
            }
        }
    }

    private float GetRandomTimeLimit(SP_SpawnSettings settings)
    {
        return UnityEngine.Random.Range(settings.spawnTime.x, settings.spawnTime.y);
    }

    private int GetRandomPrefabIndex(SP_SpawnSettings settings)
    {
        int maxValue = settings.spaceObjects[settings.spaceObjects.Count - 1].probability;
        float percentage = (float)UnityEngine.Random.Range(0, maxValue + 1) / (float)maxValue;
        int index = 0;
        foreach (SP_SpawnSettings.SpaceObject obj in settings.spaceObjects)
        {
            float threshold = (float)obj.probability / (float)maxValue;
            if (percentage <= threshold)
            {
                break;
            }
            else
            {
                index++;
            }
        }
        Debug.Log("index: " + index);
        return index;
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
