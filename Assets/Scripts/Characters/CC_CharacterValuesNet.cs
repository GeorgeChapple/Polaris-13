using Unity.Netcode;
using UnityEngine;

// Made By: Jason Lodge.
// Summary: Server authority sync for character values.
// Server ticks the real values, then mirrors them to clients with network vars.

public class CC_CharacterValuesNet : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private CC_CharacterValues characterValues;

    private NetworkVariable<float> health = new NetworkVariable<float>(0f);
    private NetworkVariable<float> maxHealth = new NetworkVariable<float>(0f);

    private NetworkVariable<float> hunger = new NetworkVariable<float>(0f);
    private NetworkVariable<float> maxHunger = new NetworkVariable<float>(0f);

    private NetworkVariable<float> thirst = new NetworkVariable<float>(0f);
    private NetworkVariable<float> maxThirst = new NetworkVariable<float>(0f);

    private NetworkVariable<float> stamina = new NetworkVariable<float>(0f);
    private NetworkVariable<float> maxStamina = new NetworkVariable<float>(0f);

    private NetworkVariable<float> oxygen = new NetworkVariable<float>(0f);
    private NetworkVariable<float> maxOxygen = new NetworkVariable<float>(0f);

    private NetworkVariable<bool> enableNoOxygenDeath = new NetworkVariable<bool>(true);
    private NetworkVariable<bool> enableHunger = new NetworkVariable<bool>(true);
    private NetworkVariable<bool> enableThirst = new NetworkVariable<bool>(true);

    private void Awake()
    {
        if (characterValues == null)
        {
            characterValues = GetComponent<CC_CharacterValues>();
        }
    }

    // setup network state once the object has spawned.
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        health.OnValueChanged += OnAnyValueChanged;
        maxHealth.OnValueChanged += OnAnyValueChanged;

        hunger.OnValueChanged += OnAnyValueChanged;
        maxHunger.OnValueChanged += OnAnyValueChanged;

        thirst.OnValueChanged += OnAnyValueChanged;
        maxThirst.OnValueChanged += OnAnyValueChanged;

        stamina.OnValueChanged += OnAnyValueChanged;
        maxStamina.OnValueChanged += OnAnyValueChanged;

        oxygen.OnValueChanged += OnAnyValueChanged;
        maxOxygen.OnValueChanged += OnAnyValueChanged;

        enableNoOxygenDeath.OnValueChanged += OnAnyBoolValueChanged;
        enableHunger.OnValueChanged += OnAnyBoolValueChanged;
        enableThirst.OnValueChanged += OnAnyBoolValueChanged;

        if (IsServer)
        {
            PushFromCharacterValues_Server();
        }
        else
        {
            ApplyToCharacterValues_Client();
        }
    }

    // clean up network subscriptions when despawned.
    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= OnAnyValueChanged;
        maxHealth.OnValueChanged -= OnAnyValueChanged;

        hunger.OnValueChanged -= OnAnyValueChanged;
        maxHunger.OnValueChanged -= OnAnyValueChanged;

        thirst.OnValueChanged -= OnAnyValueChanged;
        maxThirst.OnValueChanged -= OnAnyValueChanged;

        stamina.OnValueChanged -= OnAnyValueChanged;
        maxStamina.OnValueChanged -= OnAnyValueChanged;

        oxygen.OnValueChanged -= OnAnyValueChanged;
        maxOxygen.OnValueChanged -= OnAnyValueChanged;

        enableNoOxygenDeath.OnValueChanged -= OnAnyBoolValueChanged;
        enableHunger.OnValueChanged -= OnAnyBoolValueChanged;
        enableThirst.OnValueChanged -= OnAnyBoolValueChanged;

        base.OnNetworkDespawn();
    }

    private void LateUpdate()
    {
        // update visuals after movement and camera changes.
        if (!IsSpawned || characterValues == null)
        {
            return;
        }

        if (IsServer)
        {
            PushFromCharacterValues_Server();
        }
    }

    private void OnAnyValueChanged(float oldValue, float newValue)
    {
        if (IsServer)
        {
            return;
        }

        ApplyToCharacterValues_Client();
    }

    private void OnAnyBoolValueChanged(bool oldValue, bool newValue)
    {
        if (IsServer)
        {
            return;
        }

        ApplyToCharacterValues_Client();
    }

    private void PushFromCharacterValues_Server()
    {
        health.Value = characterValues.Health;
        maxHealth.Value = characterValues.maxHealth;

        hunger.Value = characterValues.Hunger;
        maxHunger.Value = characterValues.maxHunger;

        thirst.Value = characterValues.Thirst;
        maxThirst.Value = characterValues.maxThirst;

        stamina.Value = characterValues.Stamina;
        maxStamina.Value = characterValues.maxStamina;

        oxygen.Value = characterValues.Oxygen;
        maxOxygen.Value = characterValues.maxOxygen;

        enableNoOxygenDeath.Value = characterValues.EnableNoOxygenDeath;
        enableHunger.Value = characterValues.EnableHunger;
        enableThirst.Value = characterValues.EnableThirst;
    }

    private void ApplyToCharacterValues_Client()
    {
        if (characterValues == null)
        {
            return;
        }

        characterValues.SetAll
        (
            health.Value, maxHealth.Value,
            hunger.Value, maxHunger.Value,
            thirst.Value, maxThirst.Value,
            stamina.Value, maxStamina.Value,
            oxygen.Value, maxOxygen.Value
        );

        characterValues.SetSurvivalToggles
        (
            enableNoOxygenDeath.Value,
            enableHunger.Value,
            enableThirst.Value
        );
    }
}