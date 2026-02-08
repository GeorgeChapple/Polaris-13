using UnityEngine;

// Made By: Jason Lodge
// Summary: Houses all character values (reusable for npcs).
// List of all - Health, Stamina / Oxygen, Level/Exp, TBC.
public class CC_CharacterValues : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    [SerializeField] float health = 100f;

    [Header("Stamina / Oxygen")]
    public float maxStamina = 100f;
    [SerializeField] float stamina = 100f;

    public float maxOxygen = 100f;
    [SerializeField] float oxygen = 100f;

    [Header("Level / Exp")]
    [SerializeField] int level = 1;
    [SerializeField] int exp = 0;

    [Tooltip("Exp required for next level. will change to be based on current level eventually.")]
    public int expToNextLevel = 100;

    public bool isDead;

    // Getters
    public float Health => health;
    public float Stamina => stamina;
    public float Oxygen => oxygen;

    public int Level => level;
    public int Exp => exp;

    // Init / Reset
    public void SetDefaults()
    {
        // ensures everything is within bounds
        SetMaxHealth(maxHealth, true);
        SetMaxStamina(maxStamina, true);
        SetMaxOxygen(maxOxygen, true);

        SetLevel(level, false);
        SetExp(exp, false);

        isDead = (health <= 0f);
    }

    void Awake()
    {
        SetDefaults();
    }

    // Health
    public void SetHealth(float value, bool clamp = true)
    {
        health = clamp ? Mathf.Clamp(value, 0f, maxHealth) : value;
        isDead = (health <= 0f);
    }

    public void AddHealth(float amount)
    {
        SetHealth(health + amount, true);
    }

    public void RemoveHealth(float amount)
    {
        SetHealth(health - Mathf.Abs(amount), true);
    }

    public void SetMaxHealth(float value, bool refill = false)
    {
        maxHealth = Mathf.Max(0f, value);
        if (refill) { health = maxHealth; }
        else { health = Mathf.Clamp(health, 0f, maxHealth); }

        isDead = (health <= 0f);
    }

    public void Kill()
    {
        SetHealth(0f, true);
        isDead = true;
    }

    public void Revive(float healthPercent = 1f)
    {
        isDead = false;
        SetHealth(maxHealth * Mathf.Clamp01(healthPercent), true);
    }

    // Stamina
    public void SetStamina(float value, bool clamp = true)
    {
        stamina = clamp ? Mathf.Clamp(value, 0f, maxStamina) : value;
    }

    public void AddStamina(float amount)
    {
        SetStamina(stamina + amount, true);
    }

    public void RemoveStamina(float amount)
    {
        SetStamina(stamina - Mathf.Abs(amount), true);
    }

    public void SetMaxStamina(float value, bool refill = false)
    {
        maxStamina = Mathf.Max(0f, value);
        if (refill) { stamina = maxStamina; }
        else { stamina = Mathf.Clamp(stamina, 0f, maxStamina); }
    }

    // Oxygen
    public void SetOxygen(float value, bool clamp = true)
    {
        oxygen = clamp ? Mathf.Clamp(value, 0f, maxOxygen) : value;
    }

    public void AddOxygen(float amount)
    {
        SetOxygen(oxygen + amount, true);
    }

    public void RemoveOxygen(float amount)
    {
        SetOxygen(oxygen - Mathf.Abs(amount), true);
    }

    public void SetMaxOxygen(float value, bool refill = false)
    {
        maxOxygen = Mathf.Max(0f, value);
        if (refill) { oxygen = maxOxygen; }
        else { oxygen = Mathf.Clamp(oxygen, 0f, maxOxygen); }
    }

    // Level / Exp
    public void SetLevel(int newLevel, bool resetExp = true)
    {
        level = Mathf.Max(1, newLevel);
        if (resetExp) { exp = 0; }
    }

    public void AddLevel(int amount, bool resetExp = true)
    {
        SetLevel(level + amount, resetExp);
    }

    public void SetExp(int newExp, bool allowLevelUp = true)
    {
        exp = Mathf.Max(0, newExp);
        if (allowLevelUp) { TryLevelUp(); }
    }

    public void AddExp(int amount, bool allowLevelUp = true)
    {
        exp = Mathf.Max(0, exp + amount);
        if (allowLevelUp) { TryLevelUp(); }
    }

    public bool TryLevelUp()
    {
        // simple flat exp threshold for now
        bool leveled = false;

        while (expToNextLevel > 0 && exp >= expToNextLevel)
        {
            exp -= expToNextLevel;
            level += 1;
            leveled = true;
        }

        return leveled;
    }

    // Utility
    public void SetAll(
        float newHealth, float newMaxHealth,
        float newStamina, float newMaxStamina,
        float newOxygen, float newMaxOxygen,
        int newLevel, int newExp
    )
    {
        SetMaxHealth(newMaxHealth, false);
        SetHealth(newHealth, true);

        SetMaxStamina(newMaxStamina, false);
        SetStamina(newStamina, true);

        SetMaxOxygen(newMaxOxygen, false);
        SetOxygen(newOxygen, true);

        SetLevel(newLevel, false);
        SetExp(newExp, true);
    }
}
