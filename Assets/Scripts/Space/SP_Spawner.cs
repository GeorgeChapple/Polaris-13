using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Script By : George Chapple
// Summary   : Spawns Space Objects

public class SP_Spawner : NetworkBehaviour
{
    [HideInInspector] public SP_SpawnSettings settings;
    private SP_SpaceManager spaceManager;
    private float timeLimit;
    private float timer = 0;
    private int lifetimeSpawned = 0;

    private Dictionary<INV_Item, float> sortedDebrisItemProbabilites = new Dictionary<INV_Item, float>();

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
        sortedDebrisItemProbabilites = SortItemProbabilites(false);
    }

    private void Update()
    {
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

                        INV_Chest chestObject = newDebris.GetComponent<INV_Chest>();
                        if (chestObject != null)
                        {
                            PopulateChest(chestObject);
                        }
                        else
                        {
                            // for POIs with chests inside
                            INV_Chest[] chestsInChildren = newDebris.GetComponentsInChildren<INV_Chest>();
                            if (chestsInChildren != null || chestsInChildren.Count() > 0)
                            {
                                foreach (INV_Chest chest in chestsInChildren)
                                {
                                    PopulateChest(chest); // note to self, add item rarities into this system
                                }
                            }
                        }



                        spaceManager.debris.Add(newDebris, spaceManager.rocket.worldDirection);
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

    private void PopulateChest(INV_Chest chest)
    {
        if (!IsServer || chest == null) { return; }

        INV_ChestLootProfile lootProfile = chest.LootProfile;
        if (lootProfile == null) { return; }

        chest.ClearItems_Server();

        int minItemCount = Mathf.Max(0, lootProfile.ItemCountRange.x);
        int maxItemCount = Mathf.Max(minItemCount, lootProfile.ItemCountRange.y);
        int itemCount = Random.Range(minItemCount, maxItemCount + 1);

        

        for (int i = 0; i < itemCount; i++)
        {
            INV_Item randomItem = GetRandomChestItem(chest);
            if (randomItem == null) { continue; }

            int minItemAmount = Mathf.Max(0, randomItem.AmountSpawnedInChestRange.x);
            int maxItemAmount = Mathf.Max(minItemAmount, randomItem.AmountSpawnedInChestRange.y);
            int itemAmount = randomItem.Stackable ? Random.Range(minItemAmount, maxItemAmount + 1) : 1;

            if (!chest.TryStoreItemDataAutoPlace(randomItem,itemAmount)) { break; }
        }

        chest.RefreshAllViewers();
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
        return index;
    }

    private Dictionary<INV_Item, float> SortItemProbabilites(bool forChest)
    {
        // sort all item probabilities
        Dictionary<INV_Item, float> allItemProbabilities = new Dictionary<INV_Item, float>();

        foreach (INV_Item item in INV_ItemDatabase.Instance.Items)
        {
            float probability = forChest ? item.ChanceOfSpawnInChest : item.ChanceOfSpawnAsDebris;
            if (probability <= 0) { continue; }

            allItemProbabilities.Add(item, probability);
        }

        return allItemProbabilities.OrderBy(entry => entry.Value).ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private INV_Item GetRandomDebrisItem()
    {
        return GetRandomItemFromTable(sortedDebrisItemProbabilites);
    }

    private INV_Item GetRandomItemFromTable(Dictionary<INV_Item, float> itemTable)
    {
        if (itemTable == null || itemTable.Count == 0) { return null; }

        // get highest probability
        float maxValue = itemTable.Last<KeyValuePair<INV_Item, float>>().Value;
        float percentage = UnityEngine.Random.Range(0, maxValue + 1) / maxValue;
        INV_Item itemToReturn = null;

        foreach (KeyValuePair<INV_Item, float> item in itemTable)
        {
            float threshold = item.Value / maxValue;
            itemToReturn = item.Key;
            if (percentage <= threshold) { break; }
        }

        return itemToReturn;
    }

    private INV_Item GetRandomChestItem(INV_Chest chest)
    {
        if (chest == null) { return null; }

        INV_ChestLootProfile lootProfile = chest.LootProfile;
        if (lootProfile == null) { return null; }

        List<INV_Item> allItems = (List<INV_Item>)INV_ItemDatabase.Instance.Items;
        if (allItems == null || allItems.Count == 0) { return null; }

        float totalWeight = 0f;

        for (int i = 0; i < allItems.Count; i++)
        {
            INV_Item item = allItems[i];
            if (item == null) { continue; }

            float weight = item.ChanceOfSpawnInChest * lootProfile.GetMultiplier(item.ItemRarityVal);
            if (weight <= 0f) { continue; }

            totalWeight += weight;
        }

        if (totalWeight <= 0f) { return null; }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float running = 0f;

        for (int i = 0; i < allItems.Count; i++)
        {
            INV_Item item = allItems[i];
            if (item == null) { continue; }

            float weight = item.ChanceOfSpawnInChest * lootProfile.GetMultiplier(item.ItemRarityVal);
            if (weight <= 0f) { continue; }

            running += weight;
            if (roll <= running) { return item; }
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