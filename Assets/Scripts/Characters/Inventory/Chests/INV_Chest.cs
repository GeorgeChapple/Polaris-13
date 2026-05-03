using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Handles Chest Capabilities
// Lives on Chest Object, gets player ref and sets up chest inventory ui using INV_Inventory on player.
// Holds its own items on host as plain data, then sends snapshots to any viewers currently looking at it.
public class INV_Chest : NetworkBehaviour
{
    [System.Serializable]
    public class ChestItemData
    {
        public string uniqueId;
        public string itemId;
        public int quantity = 1;
        public int cellX;
        public int cellY;
        public int rotation;
    }

    [System.Serializable]
    private class ChestSnapshotWrapper
    {
        public List<ChestItemData> items = new List<ChestItemData>();
    }

    [Header("Loot Profile")]
    [SerializeField] private INV_ChestLootProfile lootProfile;
    [SerializeField] private bool populateLootOnSpawn = true;
    [SerializeField] private bool hasPopulatedLoot;

    [Header("Chest Data")]
    [SerializeField] private List<ChestItemData> items = new List<ChestItemData>();

    [Header("Chest Grid")]
    [SerializeField] private int chestGridMaxHeight = 4;
    [SerializeField] private int chestGridMaxWidth = 10;

    [Header("Debug")]
    [SerializeField] private bool logChest;

    // server side viewer tracking
    private readonly HashSet<ulong> viewingClientIds = new HashSet<ulong>();

    public INV_ChestLootProfile LootProfile => lootProfile;

    public IReadOnlyList<ChestItemData> Items => items;
    public int GridHeight => chestGridMaxHeight;
    public int GridWidth => chestGridMaxWidth;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // only server sets up chest loot
        if (!IsServer) { return; }

        PopulateChest_Server();
    }

    public void SetupEverything(GameObject player)
    {
        if (player == null) { return; }

        NetworkObject playerNetObj = player.GetComponent<NetworkObject>();
        if (playerNetObj == null) { playerNetObj = player.GetComponentInParent<NetworkObject>(); }
        if (playerNetObj == null) { return; }

        if (IsServer)
        {
            OpenChest_Server(playerNetObj.OwnerClientId);
            return;
        }

        RequestOpenChestRpc(new NetworkObjectReference(playerNetObj));
    }

    [Rpc(SendTo.Server)]
    private void RequestOpenChestRpc(NetworkObjectReference playerRef, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!playerRef.TryGet(out NetworkObject playerObj) || playerObj == null) { return; }
        if (playerObj.OwnerClientId != senderClientId) { return; }

        OpenChest_Server(senderClientId);
    }

    [Rpc(SendTo.Server)]
    public void RequestCloseChestRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        CloseChestViewer_Server(senderClientId);
    }

    private void OpenChest_Server(ulong targetClientId)
    {
        if (!IsServer) { return; }

        viewingClientIds.Add(targetClientId);

        if (logChest)
        {
            Debug.Log($"Opening chest for client {targetClientId}. Item count: {items.Count}", this);
        }

        RefreshAllViewers();
    }

    private void CloseChestViewer_Server(ulong targetClientId)
    {
        if (!IsServer) { return; }
        viewingClientIds.Remove(targetClientId);
    }

    public void RefreshViewer()
    {
        RefreshAllViewers();
    }

    public void RefreshAllViewers()
    {
        if (!IsServer || viewingClientIds.Count == 0) { return; }

        string snapshotJson = BuildSnapshotJson();

        ulong[] viewers = new ulong[viewingClientIds.Count];
        viewingClientIds.CopyTo(viewers);

        RefreshChestForViewersClientRpc(snapshotJson, viewers);
    }

    [Rpc(SendTo.Everyone)]
    private void RefreshChestForViewersClientRpc(string snapshotJson, ulong[] viewers)
    {
        if (!ShouldLocalClientHandleRefresh(viewers)) { return; }

        INV_Inventory localInventory = FindLocalInventory();
        if (localInventory == null) { return; }

        List<ChestItemData> snapshot = ParseSnapshotJson(snapshotJson);
        localInventory.OpenChestView(this, snapshot);

        CC_CharacterPlayerController localController = FindLocalPlayerController();
        if (localController != null && !localController.InMenu)
        {
            localController.SetMonitoringMenu(true, true);
        }
    }

    private bool ShouldLocalClientHandleRefresh(ulong[] viewers)
    {
        if (NetworkManager == null) { return false; }

        ulong localId = NetworkManager.LocalClientId;

        for (int i = 0; i < viewers.Length; i++)
        {
            if (viewers[i] == localId) { return true; }
        }

        return false;
    }

    private INV_Inventory FindLocalInventory()
    {
        INV_PlayerInventoryNet[] nets = FindObjectsByType<INV_PlayerInventoryNet>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < nets.Length; i++)
        {
            INV_PlayerInventoryNet net = nets[i];
            if (net == null || !net.IsOwner) { continue; }

            INV_Inventory inventory = net.GetComponentInChildren<INV_Inventory>();
            if (inventory != null) { return inventory; }
        }

        return null;
    }

    private CC_CharacterPlayerController FindLocalPlayerController()
    {
        CC_CharacterPlayerController[] controllers = FindObjectsByType<CC_CharacterPlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            CC_CharacterPlayerController controller = controllers[i];
            if (controller == null || !controller.IsOwner) { continue; }
            return controller;
        }

        return null;
    }

    public void ClearItems_Server()
    {
        if (!IsServer) { return; }
        items.Clear();
    }

    public bool TryStoreItemDataAutoPlace(INV_Item item, int quantity = 1)
    {
        if (!IsServer || item == null || quantity <= 0) { return false; }

        INV_Inventory.ItemInstance.Rotation[] rotations =
        {
            INV_Inventory.ItemInstance.Rotation.Up,
            INV_Inventory.ItemInstance.Rotation.Right,
            INV_Inventory.ItemInstance.Rotation.Down,
            INV_Inventory.ItemInstance.Rotation.Left
        };

        for (int r = 0; r < rotations.Length; r++)
        {
            for (int y = 0; y < chestGridMaxHeight; y++)
            {
                for (int x = 0; x < chestGridMaxWidth; x++)
                {
                    ChestItemData data = new ChestItemData
                    {
                        uniqueId = System.Guid.NewGuid().ToString(),
                        itemId = item.ItemID,
                        quantity = quantity,
                        cellX = x,
                        cellY = y,
                        rotation = (int)rotations[r]
                    };

                    if (!CanPlaceItemData(data, null)) { continue; }

                    items.Add(data);
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryStoreItemData(string itemId, int quantity, Vector2Int cell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (!IsServer || string.IsNullOrWhiteSpace(itemId) || quantity <= 0) { return false; }

        ChestItemData data = new ChestItemData
        {
            uniqueId = System.Guid.NewGuid().ToString(),
            itemId = itemId,
            quantity = quantity,
            cellX = cell.x,
            cellY = cell.y,
            rotation = (int)rotation
        };

        if (!CanPlaceItemData(data, null)) { return false; }

        items.Add(data);
        return true;
    }

    public bool TryMoveItemData(string uniqueId, Vector2Int cell, INV_Inventory.ItemInstance.Rotation rotation)
    {
        if (!IsServer || string.IsNullOrWhiteSpace(uniqueId)) { return false; }

        ChestItemData current = GetItemDataByUniqueId(uniqueId);
        if (current == null) { return false; }

        ChestItemData test = new ChestItemData
        {
            uniqueId = current.uniqueId,
            itemId = current.itemId,
            quantity = current.quantity,
            cellX = cell.x,
            cellY = cell.y,
            rotation = (int)rotation
        };

        if (!CanPlaceItemData(test, uniqueId)) { return false; }

        current.cellX = cell.x;
        current.cellY = cell.y;
        current.rotation = (int)rotation;
        return true;
    }

    public bool TryGetItemData(string uniqueId, out ChestItemData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(uniqueId)) { return false; }

        ChestItemData src = GetItemDataByUniqueId(uniqueId);
        if (src == null) { return false; }

        data = new ChestItemData
        {
            uniqueId = src.uniqueId,
            itemId = src.itemId,
            quantity = src.quantity,
            cellX = src.cellX,
            cellY = src.cellY,
            rotation = src.rotation
        };

        return true;
    }

    public bool TrySetItemQuantity(string uniqueId, int quantity)
    {
        if (!IsServer || string.IsNullOrWhiteSpace(uniqueId)) { return false; }

        for (int i = 0; i < items.Count; i++)
        {
            ChestItemData item = items[i];
            if (item == null || item.uniqueId != uniqueId) { continue; }

            item.quantity = Mathf.Max(0, quantity);
            return true;
        }

        return false;
    }

    public bool TryRemoveItem(string uniqueId)
    {
        if (!IsServer || string.IsNullOrWhiteSpace(uniqueId)) { return false; }

        for (int i = 0; i < items.Count; i++)
        {
            ChestItemData item = items[i];
            if (item == null || item.uniqueId != uniqueId) { continue; }

            items.RemoveAt(i);
            return true;
        }

        return false;
    }

    public bool TryRemoveItemQuantity(string uniqueId, int amount)
    {
        if (!IsServer || string.IsNullOrWhiteSpace(uniqueId) || amount <= 0) { return false; }

        for (int i = 0; i < items.Count; i++)
        {
            ChestItemData item = items[i];
            if (item == null || item.uniqueId != uniqueId) { continue; }

            item.quantity = Mathf.Max(0, item.quantity - amount);
            return true;
        }

        return false;
    }

    public bool TryRemoveLastStoredItem()
    {
        if (!IsServer || items.Count <= 0) { return false; }

        items.RemoveAt(items.Count - 1);
        return true;
    }

    private ChestItemData GetItemDataByUniqueId(string uniqueId)
    {
        for (int i = 0; i < items.Count; i++)
        {
            ChestItemData item = items[i];
            if (item == null || item.uniqueId != uniqueId) { continue; }
            return item;
        }

        return null;
    }

    private string BuildSnapshotJson()
    {
        ChestSnapshotWrapper wrapper = new ChestSnapshotWrapper
        {
            items = new List<ChestItemData>(items)
        };

        return JsonUtility.ToJson(wrapper);
    }

    private List<ChestItemData> ParseSnapshotJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return new List<ChestItemData>(); }

        ChestSnapshotWrapper wrapper = JsonUtility.FromJson<ChestSnapshotWrapper>(json);
        if (wrapper == null || wrapper.items == null) { return new List<ChestItemData>(); }

        return wrapper.items;
    }

    private bool CanPlaceItemData(ChestItemData data, string ignoreUniqueId)
    {
        if (data == null) { return false; }

        INV_Item item = INV_ItemDatabase.Instance != null ? INV_ItemDatabase.Instance.GetItemById(data.itemId) : null;
        if (item == null) { return false; }

        List<Vector2Int> offsets;
        Vector2Int size;
        BuildOffsetsForItem(item, (INV_Inventory.ItemInstance.Rotation)data.rotation, out offsets, out size);

        if (size.x <= 0 || size.y <= 0) { return false; }
        if (data.cellX < 0 || data.cellY < 0) { return false; }
        if (data.cellX + size.x > chestGridMaxWidth) { return false; }
        if (data.cellY + size.y > chestGridMaxHeight) { return false; }

        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int off = offsets[i];
            int x = data.cellX + off.x;
            int y = data.cellY + off.y;

            for (int c = 0; c < items.Count; c++)
            {
                ChestItemData other = items[c];
                if (other == null) { continue; }
                if (!string.IsNullOrWhiteSpace(ignoreUniqueId) && other.uniqueId == ignoreUniqueId) { continue; }

                INV_Item otherItem = INV_ItemDatabase.Instance != null ? INV_ItemDatabase.Instance.GetItemById(other.itemId) : null;
                if (otherItem == null) { continue; }

                List<Vector2Int> otherOffsets;
                Vector2Int otherSize;
                BuildOffsetsForItem(otherItem, (INV_Inventory.ItemInstance.Rotation)other.rotation, out otherOffsets, out otherSize);

                for (int o = 0; o < otherOffsets.Count; o++)
                {
                    Vector2Int otherOff = otherOffsets[o];
                    if (x == other.cellX + otherOff.x && y == other.cellY + otherOff.y) { return false; }
                }
            }
        }

        return true;
    }

    private void BuildOffsetsForItem(INV_Item item, INV_Inventory.ItemInstance.Rotation rot, out List<Vector2Int> finalOffsets, out Vector2Int finalSize)
    {
        finalOffsets = new List<Vector2Int>();
        finalSize = Vector2Int.one;

        if (item == null) { return; }

        List<string> shape = item.InventorySpaceShape;

        if (shape == null || shape.Count == 0)
        {
            Vector2Int s = item.ItemGridSize;
            List<Vector2Int> rawRect = new List<Vector2Int>();

            for (int y = 0; y < s.y; y++)
            {
                for (int x = 0; x < s.x; x++)
                {
                    rawRect.Add(new Vector2Int(x, y));
                }
            }

            ApplyRotatedOffsets(rawRect, s, rot, out finalOffsets, out finalSize);
            return;
        }

        int h = shape.Count;
        int w = 0;

        for (int i = 0; i < shape.Count; i++)
        {
            if (!string.IsNullOrEmpty(shape[i])) { w = Mathf.Max(w, shape[i].Length); }
        }

        Vector2Int baseSize = new Vector2Int(Mathf.Max(1, w), Mathf.Max(1, h));

        List<Vector2Int> raw = new List<Vector2Int>();
        for (int y = 0; y < h; y++)
        {
            string row = string.IsNullOrEmpty(shape[y]) ? "" : shape[y];

            for (int x = 0; x < w; x++)
            {
                char c = x < row.Length ? row[x] : '-';
                if (c == '+') { raw.Add(new Vector2Int(x, y)); }
            }
        }

        ApplyRotatedOffsets(raw, baseSize, rot, out finalOffsets, out finalSize);
    }

    private void ApplyRotatedOffsets(List<Vector2Int> rawOffsets, Vector2Int baseSize, INV_Inventory.ItemInstance.Rotation rot, out List<Vector2Int> finalOffsets, out Vector2Int finalSize)
    {
        finalOffsets = new List<Vector2Int>();
        finalSize = Vector2Int.one;

        List<Vector2Int> rotated = new List<Vector2Int>(rawOffsets);

        int width = baseSize.x;
        int height = baseSize.y;

        int turns = rot == INV_Inventory.ItemInstance.Rotation.Up ? 0 :
                    rot == INV_Inventory.ItemInstance.Rotation.Right ? 1 :
                    rot == INV_Inventory.ItemInstance.Rotation.Down ? 2 : 3;

        for (int t = 0; t < turns; t++)
        {
            List<Vector2Int> next = new List<Vector2Int>(rotated.Count);

            for (int i = 0; i < rotated.Count; i++)
            {
                next.Add(new Vector2Int(rotated[i].y, width - 1 - rotated[i].x));
            }

            rotated = next;

            int oldWidth = width;
            width = height;
            height = oldWidth;
        }

        if (rotated.Count == 0)
        {
            finalOffsets.Add(Vector2Int.zero);
            finalSize = Vector2Int.one;
            return;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        for (int i = 0; i < rotated.Count; i++)
        {
            Vector2Int p = rotated[i];

            if (p.x < minX) { minX = p.x; }
            if (p.y < minY) { minY = p.y; }
            if (p.x > maxX) { maxX = p.x; }
            if (p.y > maxY) { maxY = p.y; }
        }

        for (int i = 0; i < rotated.Count; i++)
        {
            Vector2Int p = rotated[i];
            finalOffsets.Add(new Vector2Int(p.x - minX, p.y - minY));
        }

        finalSize = new Vector2Int((maxX - minX) + 1, (maxY - minY) + 1);
    }

    private void PopulateChest_Server()
    {
        if (!IsServer || !populateLootOnSpawn || hasPopulatedLoot) { return; }
        if (lootProfile == null) { return; }

        hasPopulatedLoot = true;

        ClearItems_Server();

        int minItemCount = Mathf.Max(0, lootProfile.ItemCountRange.x);
        int maxItemCount = Mathf.Max(minItemCount, lootProfile.ItemCountRange.y);
        int itemCount = Random.Range(minItemCount, maxItemCount + 1);

        for (int i = 0; i < itemCount; i++)
        {
            INV_Item randomItem = GetRandomChestItem();
            if (randomItem == null) { continue; }

            int minItemAmount = Mathf.Max(0, randomItem.AmountSpawnedInChestRange.x);
            int maxItemAmount = Mathf.Max(minItemAmount, randomItem.AmountSpawnedInChestRange.y);
            int itemAmount = randomItem.Stackable ? Random.Range(minItemAmount, maxItemAmount + 1) : 1;

            if (!TryStoreItemDataAutoPlace(randomItem, itemAmount)) { break; }
        }

        RefreshAllViewers();
    }

    private INV_Item GetRandomChestItem()
    {
        if (lootProfile == null) { return null; }
        if (INV_ItemDatabase.Instance == null) { return null; }

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
}