using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Made By: Jason Lodge.
// Summary: Server authority enemy wave spawner.
// Spawns goobers in waves using local bounds and timing settings.

public class ENM_Spawner : NetworkBehaviour
{
    [Header("Enemy")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private int enemyLimit = 20;

    [Header("Waves")]
    public bool waveSpawn = false;
    [SerializeField] private int baseWaveAmount = 3;
    [SerializeField] private Vector2 waveDelay = new Vector2(20f, 120f);
    [SerializeField] private float timeBetweenSpawns = 0.25f;
    [SerializeField] private float intensityMult = 1f;

    [Header("Spawn Bounds")]
    [Tooltip("X/Y - Spawn Bounds\nZ/W - Negative Space X and Y (Set this higher to prevent objects spawning in front of ship)")]
    [SerializeField] private Vector4 spawnBounds = new Vector4(100, 100, 0, 0);
    [SerializeField] private float spawnDistance = 20f;
    [SerializeField] private float spawnSpacing = 1f;

    private Coroutine waveSpawnRoutine;
    private List<NetworkObject> cachedEnemies = new List<NetworkObject>();

    public int EnemyCount => cachedEnemies.Count;
    public int EnemyLimit => enemyLimit;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // only server controls enemy spawning
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        if (waveSpawn)
        {
            SetWaveSpawn(true);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            SetWaveSpawn(false);
            cachedEnemies.Clear();
        }

        base.OnNetworkDespawn();
    }

    public void SetWaveSpawnTrue()
    {
        SetWaveSpawn(true);
    }

    public void SetWaveSpawn(bool spawnWaves)
    {
        if (!IsServer)
        {
            return;
        }

        waveSpawn = spawnWaves;

        if (waveSpawn)
        {
            if (waveSpawnRoutine == null)
            {
                waveSpawnRoutine = StartCoroutine(WaveSpawnRoutine());
            }
        }
        else
        {
            if (waveSpawnRoutine != null)
            {
                StopCoroutine(waveSpawnRoutine);
                waveSpawnRoutine = null;
            }
        }
    }

    public void SpawnWave()
    {
        if (!IsServer)
        {
            return;
        }

        StartCoroutine(SpawnWaveRoutine());
    }

    private IEnumerator WaveSpawnRoutine()
    {
        while (waveSpawn)
        {
            float delay = Random.Range(waveDelay.x, waveDelay.y);
            delay /= Mathf.Max(intensityMult, 0.01f);

            yield return new WaitForSeconds(delay);

            yield return SpawnWaveRoutine();
        }

        waveSpawnRoutine = null;
    }

    private IEnumerator SpawnWaveRoutine()
    {
        if (prefab == null)
        {
            yield break;
        }

        CleanCachedEnemies();

        if (cachedEnemies.Count >= enemyLimit)
        {
            yield break;
        }

        Vector2 spawnPosition = GetRandomSpawnPosition(spawnBounds);
        int spawnAmount = Mathf.CeilToInt(baseWaveAmount * Mathf.Max(intensityMult, 0.01f));
        float spawnDelay = timeBetweenSpawns / Mathf.Max(intensityMult, 0.01f);

        for (int i = 0; i < spawnAmount; i++)
        {
            CleanCachedEnemies();

            if (cachedEnemies.Count >= enemyLimit)
            {
                yield break;
            }

            SpawnEnemy(spawnPosition, i);

            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void SpawnEnemy(Vector2 spawnPosition, int spawnIndex)
    {
        if (cachedEnemies.Count >= enemyLimit)
        {
            return;
        }

        Vector3 spawnOffset = GetSpawnOffset(spawnIndex);

        GameObject newGoob = Instantiate(
            prefab,
            transform.position + new Vector3(spawnPosition.x, spawnPosition.y, spawnDistance) + spawnOffset,
            transform.rotation
            );

        NetworkObject netObj = newGoob.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned)
        {
            netObj.Spawn();
            cachedEnemies.Add(netObj);
        }
        else if (netObj == null)
        {
            Destroy(newGoob);
        }
    }

    private void CleanCachedEnemies()
    {
        for (int i = cachedEnemies.Count - 1; i >= 0; i--)
        {
            if (cachedEnemies[i] == null)
            {
                cachedEnemies.RemoveAt(i);
                continue;
            }

            if (!cachedEnemies[i].IsSpawned)
            {
                cachedEnemies.RemoveAt(i);
            }
        }
    }

    private Vector3 GetSpawnOffset(int spawnIndex)
    {
        int x = spawnIndex % 3;
        int y = spawnIndex / 3;
        int z = spawnIndex % 2;

        return new Vector3(x, y, z) * spawnSpacing;
    }

    private Vector2 GetRandomSpawnPosition(Vector4 bounds)
    {
        Vector2 pos = new Vector2();

        pos.x = Random.Range(0, bounds.x / 2);
        pos.y = Random.Range(0, bounds.y / 2);

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

    private void OnDrawGizmosSelected()
    {
        CustomGizmos.DrawBox(
            transform.position + new Vector3(0, 0, spawnDistance),
            transform.rotation,
            new Vector2(spawnBounds.x, spawnBounds.y),
            Color.red
            );

        CustomGizmos.DrawBox(
            transform.position + new Vector3(0, 0, spawnDistance),
            transform.rotation,
            new Vector2(spawnBounds.z, spawnBounds.w),
            Color.yellow
            );
    }
}