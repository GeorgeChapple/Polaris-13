using UnityEngine;

// Made By: Jason Lodge
// Summary: Data holder for chest loot weighting.
[CreateAssetMenu(menuName = "Inventory/Chest Loot Profile")]
public class INV_ChestLootProfile : ScriptableObject
{
    [System.Serializable]
    public class RarityWeight
    {
        public INV_Item.ItemRarity rarity;
        public float multiplier = 1f;
    }

    [Header("Loot Count")]
    [SerializeField] private Vector2Int itemCountRange = new Vector2Int(1, 3);

    [Header("Rarity Multipliers")]
    [SerializeField] private RarityWeight[] rarityWeights;

    public Vector2Int ItemCountRange => itemCountRange;

    public float GetMultiplier(INV_Item.ItemRarity rarity)
    {
        if (rarityWeights == null || rarityWeights.Length == 0) { return 1f; }

        for (int i = 0; i < rarityWeights.Length; i++)
        {
            if (rarityWeights[i].rarity == rarity) { return Mathf.Max(0f, rarityWeights[i].multiplier); }
        }

        return 1f;
    }
}