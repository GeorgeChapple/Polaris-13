using System;
using Unity.Netcode;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Checks passive equipment in the players inventory and applies the benefits.
public class INV_PlayerEquipment : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private INV_Inventory inventory;
    [SerializeField] private CC_CharacterValues characterValues;
    [SerializeField] private CC_Movement movement;

    private float baseMaxOxygen;
    private float baseOxygenRegenPerSecond;

    private float baseGroundThrusterAccel;
    private float baseGroundThrusterUpSpeedCap;
    private float baseThrusterAccel;
    private float baseSpaceStabiliseAccel;

    private bool cachedBaseValues;

    private NetworkVariable<EquipmentStats> equipmentStats = new NetworkVariable<EquipmentStats>
    (
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public EquipmentStats CurrentStats => equipmentStats.Value;

    public struct EquipmentStats : INetworkSerializable, IEquatable<EquipmentStats>
    {
        public float additionalMaxOxygen;
        public float additionalOxygenRegenPerSecond;

        public float additionalGroundThrusterAccel;
        public float additionalGroundThrusterUpSpeedCap;
        public float additionalThrusterAccel;
        public float additionalSpaceStabiliseAccel;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref additionalMaxOxygen);
            serializer.SerializeValue(ref additionalOxygenRegenPerSecond);

            serializer.SerializeValue(ref additionalGroundThrusterAccel);
            serializer.SerializeValue(ref additionalGroundThrusterUpSpeedCap);
            serializer.SerializeValue(ref additionalThrusterAccel);
            serializer.SerializeValue(ref additionalSpaceStabiliseAccel);
        }

        public bool Equals(EquipmentStats other)
        {
            return Mathf.Approximately(additionalMaxOxygen, other.additionalMaxOxygen)
                && Mathf.Approximately(additionalOxygenRegenPerSecond, other.additionalOxygenRegenPerSecond)
                && Mathf.Approximately(additionalGroundThrusterAccel, other.additionalGroundThrusterAccel)
                && Mathf.Approximately(additionalGroundThrusterUpSpeedCap, other.additionalGroundThrusterUpSpeedCap)
                && Mathf.Approximately(additionalThrusterAccel, other.additionalThrusterAccel)
                && Mathf.Approximately(additionalSpaceStabiliseAccel, other.additionalSpaceStabiliseAccel);
        }
    }

    private void Awake()
    {
        CacheRefs();
        CacheBaseValues();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CacheRefs();
        CacheBaseValues();

        equipmentStats.OnValueChanged += OnEquipmentStatsChanged;

        if (inventory != null)
        {
            inventory.OnPlayerInventoryChanged += RefreshEquipmentFromInventory;
        }

        RefreshEquipmentFromInventory();
    }

    public override void OnNetworkDespawn()
    {
        equipmentStats.OnValueChanged -= OnEquipmentStatsChanged;

        if (inventory != null)
        {
            inventory.OnPlayerInventoryChanged -= RefreshEquipmentFromInventory;
        }

        base.OnNetworkDespawn();
    }

    private void CacheRefs()
    {
        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (characterValues == null)
        {
            characterValues = GetComponent<CC_CharacterValues>();
        }

        if (movement == null)
        {
            movement = GetComponent<CC_Movement>();
        }
    }

    private void CacheBaseValues()
    {
        if (cachedBaseValues)
        {
            return;
        }

        if (characterValues != null)
        {
            baseMaxOxygen = characterValues.maxOxygen;
            baseOxygenRegenPerSecond = characterValues.oxygenRegenPerSecond;
        }

        if (movement != null)
        {
            baseGroundThrusterAccel = movement.groundThrusterAccel;
            baseGroundThrusterUpSpeedCap = movement.groundThrusterUpSpeedCap;
            baseThrusterAccel = movement.thrusterAccel;
            baseSpaceStabiliseAccel = movement.spaceStabiliseAccel;
        }

        cachedBaseValues = true;
    }

    // called when inventory changes, inventory opens, item drops, crafting removes items etc.
    public void RefreshEquipmentFromInventory()
    {
        CacheRefs();
        CacheBaseValues();

        if (!IsOwner || inventory == null)
        {
            return;
        }

        EquipmentStats newStats = BuildEquipmentStats();

        ApplyEquipmentStats(newStats);

        if (!equipmentStats.Value.Equals(newStats))
        {
            equipmentStats.Value = newStats;
        }
    }

    private EquipmentStats BuildEquipmentStats()
    {
        EquipmentStats stats = new EquipmentStats();

        for (int i = 0; i < inventory.Items.Count; i++)
        {
            INV_Inventory.ItemInstance inst = inventory.Items[i];
            if (inst?.data == null)
            {
                continue;
            }

            INV_Item item = inst.data;

            if (!item.PassiveEquipment)
            {
                continue;
            }

            switch (item.EquipmentTypeVal)
            {
                case INV_Item.EquipmentType.Oxygen:
                    stats.additionalMaxOxygen += item.AdditionalMaxOxygen;
                    stats.additionalOxygenRegenPerSecond += item.AdditionalOxygenRegenPerSecond;
                    break;

                case INV_Item.EquipmentType.Thruster:
                    stats.additionalGroundThrusterAccel += item.AdditionalGroundThrusterAccel;
                    stats.additionalGroundThrusterUpSpeedCap += item.AdditionalGroundThrusterUpSpeedCap;
                    stats.additionalThrusterAccel += item.AdditionalThrusterAccel;
                    stats.additionalSpaceStabiliseAccel += item.AdditionalSpaceStabilisationAccel;
                    break;

                case INV_Item.EquipmentType.Radiation:
                    // no radiation function yet, leaving this here for later.
                    break;
            }
        }

        return stats;
    }

    private void OnEquipmentStatsChanged(EquipmentStats oldStats, EquipmentStats newStats)
    {
        ApplyEquipmentStats(newStats);
    }

    private void ApplyEquipmentStats(EquipmentStats stats)
    {
        CacheRefs();
        CacheBaseValues();

        if (characterValues != null)
        {
            characterValues.SetMaxOxygen(baseMaxOxygen + stats.additionalMaxOxygen, false);
            characterValues.oxygenRegenPerSecond = baseOxygenRegenPerSecond + stats.additionalOxygenRegenPerSecond;
        }

        if (movement != null)
        {
            movement.groundThrusterAccel = baseGroundThrusterAccel + stats.additionalGroundThrusterAccel;
            movement.groundThrusterUpSpeedCap = baseGroundThrusterUpSpeedCap + stats.additionalGroundThrusterUpSpeedCap;
            movement.thrusterAccel = baseThrusterAccel + stats.additionalThrusterAccel;
            movement.spaceStabiliseAccel = baseSpaceStabiliseAccel + stats.additionalSpaceStabiliseAccel;
        }
    }
}