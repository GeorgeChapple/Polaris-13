using UnityEngine;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: Houses all character values (reusable for npcs).
// List of all - Health, Stamina / Oxygen, Level/Exp, TBC.
public class CC_CharacterValues : MonoBehaviour
{
    [System.Serializable]
    public class FillUI
    {
        public Image img;

        public void SetFill(float f01)
        {
            if (img == null) { return; }

            float v = Mathf.Clamp01(f01);

            img.fillAmount = Mathf.Clamp01(v);
        }
    }

    [Header("Health")]
    public float maxHealth = 100f;
    [SerializeField] float health = 100f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    [SerializeField] float stamina = 100f;

    [Header("Stamina Settings")]
    public float sprintStaminaDrainPerSecond = 12f;
    public float staminaRegenPerSecond = 10f;

    [Tooltip("Delay before stamina regen starts after draining.")]
    public float staminaRegenDelay = 0.5f;

    [Tooltip("Extra delay applied when stamina has been depleted (hit 0).")]
    public float staminaDepletedRegenDelay = 1.75f;

    float staminaRegenDelayTimer;
    bool staminaWasDepleted;

    [Header("Thrusters")]
    public float maxThruster = 100f;
    [SerializeField] float thruster = 100f;

    [Header("Thruster Settings")]
    public float thrusterRegenPerSecond = 40f;

    [Tooltip("Delay before thruster regen starts after draining.")]
    public float thrusterRegenDelay = 1f;

    [Tooltip("Extra delay applied when thrusters have been depleted (hit 0).")]
    public float thrusterDepletedRegenDelay = 3f;

    float thrusterRegenDelayTimer;
    bool thrusterWasDepleted;

    [Header("Oxygen")]
    public float maxOxygen = 100f;
    [SerializeField] float oxygen = 100f;

    [Header("Level / Exp")]
    [SerializeField] int level = 1;
    [SerializeField] int exp = 0;

    [Tooltip("Exp required for next level. will change to be based on current level eventually.")]
    public int expToNextLevel = 100;

    public bool isDead;

    [Header("UI")]
    public FillUI[] healthUI;
    public FillUI[] staminaUI;
    public FillUI[] thrusterUI;
    public FillUI[] oxygenUI;
    public FillUI[] expUI;

    // Getters
    public float Health => health;
    public float Stamina => stamina;
    public float Thruster => thruster;
    public float Oxygen => oxygen;

    public int Level => level;
    public int Exp => exp;

    // Init / Reset
    public void SetDefaults()
    {
        // ensures everything is within bounds
        SetMaxHealth(maxHealth, true);
        SetMaxStamina(maxStamina, true);
        SetMaxThruster(maxThruster, true);
        SetMaxOxygen(maxOxygen, true);

        SetLevel(level, false);
        SetExp(exp, false);

        isDead = (health <= 0f);

        staminaRegenDelayTimer = 0f;
        staminaWasDepleted = (stamina <= 0f);

        thrusterRegenDelayTimer = 0f;
        thrusterWasDepleted = (thruster <= 0f);

        RefreshUI();
    }

    void Awake()
    {
        SetDefaults();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // update bars when tweaking values
        maxHealth = Mathf.Max(0f, maxHealth);
        maxStamina = Mathf.Max(0f, maxStamina);
        maxThruster = Mathf.Max(0f, maxThruster);
        maxOxygen = Mathf.Max(0f, maxOxygen);

        health = Mathf.Clamp(health, 0f, maxHealth);
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
        thruster = Mathf.Clamp(thruster, 0f, maxThruster);
        oxygen = Mathf.Clamp(oxygen, 0f, maxOxygen);

        level = Mathf.Max(1, level);
        exp = Mathf.Max(0, exp);
        expToNextLevel = Mathf.Max(1, expToNextLevel);

        isDead = (health <= 0f);

        RefreshUI();
    }
#endif

    // UI
    public void RefreshUI()
    {
        float h01 = (maxHealth <= 0f) ? 0f : (health / maxHealth);
        float s01 = (maxStamina <= 0f) ? 0f : (stamina / maxStamina);
        float t01 = (maxThruster <= 0f) ? 0f : (thruster / maxThruster);
        float o01 = (maxOxygen <= 0f) ? 0f : (oxygen / maxOxygen);
        float e01 = (expToNextLevel <= 0) ? 0f : Mathf.Clamp01((float)exp / expToNextLevel);

        if (healthUI != null)
        {
            for (int i = 0; i < healthUI.Length; i++) { if (healthUI[i] != null) { healthUI[i].SetFill(h01); } }
        }

        if (staminaUI != null)
        {
            for (int i = 0; i < staminaUI.Length; i++) { if (staminaUI[i] != null) { staminaUI[i].SetFill(s01); } }
        }

        if (thrusterUI != null)
        {
            for (int i = 0; i < thrusterUI.Length; i++) { if (thrusterUI[i] != null) { thrusterUI[i].SetFill(t01); } }
        }

        if (oxygenUI != null)
        {
            for (int i = 0; i < oxygenUI.Length; i++) { if (oxygenUI[i] != null) { oxygenUI[i].SetFill(o01); } }
        }

        if (expUI != null)
        {
            for (int i = 0; i < expUI.Length; i++) { if (expUI[i] != null) { expUI[i].SetFill(e01); } }
        }
    }

    // Health
    public void SetHealth(float value, bool clamp = true)
    {
        health = clamp ? Mathf.Clamp(value, 0f, maxHealth) : value;
        isDead = (health <= 0f);
        RefreshUI();
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
        RefreshUI();
    }

    public void Kill()
    {
        SetHealth(0f, true);
        isDead = true;
        RefreshUI();
    }

    public void Revive(float healthPercent = 1f)
    {
        isDead = false;
        SetHealth(maxHealth * Mathf.Clamp01(healthPercent), true);
        RefreshUI();
    }

    // Stamina
    public void SetStamina(float value, bool clamp = true)
    {
        stamina = clamp ? Mathf.Clamp(value, 0f, maxStamina) : value;

        // depleted state
        if (stamina <= 0f) { staminaWasDepleted = true; }

        RefreshUI();
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

        staminaWasDepleted = (stamina <= 0f);

        RefreshUI();
    }

    // Call once per frame to drain/regenerate stamina.
    public void TickStamina(bool sprinting, bool allowRegen)
    {
        // sprint drains stamina
        if (sprinting)
        {
            DrainStamina(sprintStaminaDrainPerSecond * Time.deltaTime);

            if (staminaWasDepleted)
            {
                // if depleted set timer to longer one
                staminaRegenDelayTimer = staminaDepletedRegenDelay;
            }
            else
            {
                // regen delay after any drain
                staminaRegenDelayTimer = staminaRegenDelay;
            }
            return;
        }

        // if we are not allowed to regen, hold timer
        if (!allowRegen)
        {
            return;
        }

        // regen after delay
        if (staminaRegenDelayTimer > 0f)
        {
            staminaRegenDelayTimer -= Time.deltaTime;
            return;
        }

        RegenStamina(staminaRegenPerSecond * Time.deltaTime);
    }

    public void DrainStamina(float amount)
    {
        float prev = stamina;

        SetStamina(stamina - amount, true);

        // if just hit 0, apply longer delay
        if (prev > 0f && stamina <= 0f)
        {
            staminaWasDepleted = true;
        }
    }

    public void RegenStamina(float amount)
    {
        if (amount <= 0f) { return; }

        // if depleted, enforce the delay before any regen
        if (staminaWasDepleted && stamina <= 0f)
        {
            // if timer still running, do nothing
            if (staminaRegenDelayTimer > 0f) { return; }
        }

        SetStamina(stamina + amount, true);

        // once we have stamina again, clear depleted flag
        if (stamina > 0f)
        {
            staminaWasDepleted = false;
        }
    }

    public bool HasStamina(float min = 0.01f)
    {
        return stamina > min;
    }

    // Thruster
    public void SetThruster(float value, bool clamp = true)
    {
        thruster = clamp ? Mathf.Clamp(value, 0f, maxThruster) : value;

        if (thruster <= 0f) { thrusterWasDepleted = true; }

        RefreshUI();
    }

    public void AddThruster(float amount)
    {
        SetThruster(thruster + amount, true);
    }

    public void RemoveThruster(float amount)
    {
        SetThruster(thruster - Mathf.Abs(amount), true);
    }

    public void SetMaxThruster(float value, bool refill = false)
    {
        maxThruster = Mathf.Max(0f, value);
        if (refill) { thruster = maxThruster; }
        else { thruster = Mathf.Clamp(thruster, 0f, maxThruster); }

        thrusterWasDepleted = (thruster <= 0f);

        RefreshUI();
    }

    // Call once per frame to drain/regenerate thruster.
    public void TickThruster(bool usingThrusters, bool allowRegen, float drainPerSecond)
    {
        if (usingThrusters)
        {
            DrainThruster(drainPerSecond * Time.deltaTime);

            if (thrusterWasDepleted)
            {
                thrusterRegenDelayTimer = thrusterDepletedRegenDelay;
            }
            else
            {
                thrusterRegenDelayTimer = thrusterRegenDelay;
            }
            return;
        }

        if (!allowRegen)
        {
            return;
        }

        if (thrusterRegenDelayTimer > 0f)
        {
            thrusterRegenDelayTimer -= Time.deltaTime;
            return;
        }

        RegenThruster(thrusterRegenPerSecond * Time.deltaTime);
    }

    public void DrainThruster(float amount)
    {
        float prev = thruster;

        SetThruster(thruster - amount, true);

        if (prev > 0f && thruster <= 0f)
        {
            thrusterWasDepleted = true;
        }
    }

    public void RegenThruster(float amount)
    {
        if (amount <= 0f) { return; }

        if (thrusterWasDepleted && thruster <= 0f)
        {
            if (thrusterRegenDelayTimer > 0f) { return; }
        }

        SetThruster(thruster + amount, true);

        if (thruster > 0f)
        {
            thrusterWasDepleted = false;
        }
    }

    public bool HasThruster(float min = 0.01f)
    {
        return thruster > min;
    }

    // Oxygen
    public void SetOxygen(float value, bool clamp = true)
    {
        oxygen = clamp ? Mathf.Clamp(value, 0f, maxOxygen) : value;
        RefreshUI();
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
        RefreshUI();
    }

    // Level / Exp
    public void SetLevel(int newLevel, bool resetExp = true)
    {
        level = Mathf.Max(1, newLevel);
        if (resetExp) { exp = 0; }
        RefreshUI();
    }

    public void AddLevel(int amount, bool resetExp = true)
    {
        SetLevel(level + amount, resetExp);
    }

    public void SetExp(int newExp, bool allowLevelUp = true)
    {
        exp = Mathf.Max(0, newExp);
        if (allowLevelUp) { TryLevelUp(); }
        RefreshUI();
    }

    public void AddExp(int amount, bool allowLevelUp = true)
    {
        exp = Mathf.Max(0, exp + amount);
        if (allowLevelUp) { TryLevelUp(); }
        RefreshUI();
    }

    public bool TryLevelUp()
    {
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
        float newThruster, float newMaxThruster,
        float newOxygen, float newMaxOxygen,
        int newLevel, int newExp
    )
    {
        SetMaxHealth(newMaxHealth, false);
        SetHealth(newHealth, true);

        SetMaxStamina(newMaxStamina, false);
        SetStamina(newStamina, true);

        SetMaxThruster(newMaxThruster, false);
        SetThruster(newThruster, true);

        SetMaxOxygen(newMaxOxygen, false);
        SetOxygen(newOxygen, true);

        SetLevel(newLevel, false);
        SetExp(newExp, true);

        RefreshUI();
    }
}