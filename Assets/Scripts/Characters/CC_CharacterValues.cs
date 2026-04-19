using System.Collections;
using TMPro;
using Unity.Netcode;
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

        public bool appearOnUpdated;

        [Tooltip("How long the ui stays visible after being updated.")]
        public float visibleDelay = 1f;

        [Tooltip("How fast the ui alpha lerps in.")]
        public float alphaLerpInSpeed = 10f;

        [Tooltip("How fast the ui alpha lerps out.")]
        public float alphaLerpOutSpeed = 6f;

        float lastFill = -1f;
        float hideTimer;
        float currentAlpha = 1f;
        bool isShowing;

        public void Init(float f01)
        {
            if (img == null) { return; }

            float v = Mathf.Clamp01(f01);

            lastFill = v;
            img.fillAmount = v;

            if (appearOnUpdated)
            {
                currentAlpha = 0f;
                hideTimer = 0f;
                isShowing = false;
                SetAlpha(0f);
            }
            else
            {
                currentAlpha = 1f;
                hideTimer = 0f;
                isShowing = true;
                SetAlpha(1f);
            }
        }

        public void SetFill(float f01, bool triggerAppear = true)
        {
            if (img == null) { return; }

            float v = Mathf.Clamp01(f01);

            // if value changed, show ui
            if (appearOnUpdated && triggerAppear)
            {
                if (lastFill < 0f || !Mathf.Approximately(lastFill, v))
                {
                    Show();
                }
            }

            lastFill = v;
            img.fillAmount = v;
        }

        public void Tick()
        {
            if (img == null) { return; }
            if (!appearOnUpdated) { return; }

            // lerp in while showing
            if (isShowing)
            {
                currentAlpha = Mathf.Lerp(currentAlpha, 1f, alphaLerpInSpeed * Time.deltaTime);

                // snap close values to 1
                if (currentAlpha >= 0.99f) { currentAlpha = 1f; }

                if (hideTimer > 0f)
                {
                    hideTimer -= Time.deltaTime;
                }
                else
                {
                    isShowing = false;
                }
            }
            else
            {
                // lerp out after delay
                currentAlpha = Mathf.Lerp(currentAlpha, 0f, alphaLerpOutSpeed * Time.deltaTime);

                // snap small values to 0
                if (currentAlpha <= 0.01f) { currentAlpha = 0f; }
            }

            SetAlpha(currentAlpha);
        }

        public void Show()
        {
            if (img == null) { return; }

            hideTimer = visibleDelay;
            isShowing = true;
        }

        void SetAlpha(float a)
        {
            if (img == null) { return; }

            Color c = img.color;
            c.a = Mathf.Clamp01(a);
            img.color = c;
        }
    }

    [Header("Health")]
    public float maxHealth = 100f;
    [SerializeField] float health = 100f;

    private float damageToTakePerTick = 0f;
    private float damageTickRateRuntime = 0f;
    private int damageDebuffTicks = 0;
    private float damageStartDelayRuntime = 0f;
    private float currentDamageDelayTimer = 0f;
    private float currentDamageTickTimer = 0f;
    private bool damageStarted = false;

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

    [Header("Hunger")]
    public float maxHunger = 100f;
    [SerializeField] private float hunger = 100f;

    [Header("Hunger Settings")]
    public float hungerDrainPerSecond = 0.25f;
    [SerializeField, Min(0)] private float hungerDrainDelay = 0f;
    [SerializeField] private float maxHungerDrainDelay = 120f;

    [Header("Hunger Empty Damage Settings")]
    [Tooltip("How long to wait after hunger becomes empty before taking damage.")]
    public float hungerEmptyDamageDelay = 5f;

    [Tooltip("How much damage to apply each tick while hunger is empty.")]
    public float hungerEmptyDamagePerTick = 2f;

    [Tooltip("How often to apply hunger empty damage.")]
    public float hungerEmptyDamageTickRate = 1f;

    float hungerEmptyDamageDelayTimer;
    float hungerEmptyDamageTickTimer;
    bool hungerEmptyDamageStarted;

    [Header("Thirst")]
    public float maxThirst = 100f;
    [SerializeField] private float thirst = 100f;

    [Header("Thirst Settings")]
    public float thirstDrainPerSecond = 0.4f;
    [SerializeField, Min(0)] private float thirstDrainDelay = 0f;
    [SerializeField] private float maxThirstDrainDelay = 120f;

    [Header("Thirst Empty Damage Settings")]
    [Tooltip("How long to wait after thirst becomes empty before taking damage.")]
    public float thirstEmptyDamageDelay = 3f;

    [Tooltip("How much damage to apply each tick while thirst is empty.")]
    public float thirstEmptyDamagePerTick = 4f;

    [Tooltip("How often to apply thirst empty damage.")]
    public float thirstEmptyDamageTickRate = 1f;

    float thirstEmptyDamageDelayTimer;
    float thirstEmptyDamageTickTimer;
    bool thirstEmptyDamageStarted;

    [Header("Oxygen")]
    public float maxOxygen = 100f;
    [SerializeField] float oxygen = 100f;

    [Header("Oxygen Settings")]
    [Tooltip("Passive oxygen drain per second while in space mode.")]
    public float oxygenDrainPerSecondInSpace = 1f;

    [Header("Oxygen Empty Damage Settings")]
    [Tooltip("How long to wait after oxygen becomes empty before taking damage.")]
    public float oxygenEmptyDamageDelay = 1f;

    [Tooltip("How much damage to apply each tick while oxygen is empty.")]
    public float oxygenEmptyDamagePerTick = 8f;

    [Tooltip("How often to apply oxygen empty damage.")]
    public float oxygenEmptyDamageTickRate = 1f;

    float oxygenEmptyDamageDelayTimer;
    float oxygenEmptyDamageTickTimer;
    bool oxygenEmptyDamageStarted;

    [Header("Oxygen Thruster Settings")]
    [Tooltip("Base oxygen drain per second for thruster usage. Use the multipliers below per use case.")]
    public float oxygenThrusterDrainPerSecond = 2f;

    public float oxygenRegenPerSecond = 40f;

    [Tooltip("Delay before oxygen regen starts after draining.")]
    public float oxygenRegenDelay = 1f;

    [Tooltip("Extra delay applied when oxygen has been depleted (hit 0).")]
    public float oxygenDepletedRegenDelay = 3f;

    [Tooltip("Multiplier applied while using airborne ground thrusters.")]
    public float groundThrusterOxygenDrainMult = 1.5f;

    [Tooltip("Multiplier applied while using regular space movement thrusters.")]
    public float spaceMoveOxygenDrainMult = 1f;

    [Tooltip("Multiplier applied while using space stabilisation.")]
    public float spaceStabiliseOxygenDrainMult = 0.5f;

    float oxygenRegenDelayTimer;
    bool oxygenWasDepleted;

    [Header("Death")]
    [Tooltip("How long to wait before respawning after death.")]
    [SerializeField] private float deathRespawnDelay = 3f;

    [Tooltip("Simple centered death image root shown while dead.")]
    [SerializeField] private GameObject deathScreenRoot;

    public bool isDead;

    [Header("UI")]
    public FillUI[] healthUI;
    public FillUI[] hungerUI;
    public FillUI[] thirstUI;
    public FillUI[] staminaUI;
    public FillUI[] oxygenUI;
    public TextMeshProUGUI speedText;

    // Getters
    public float Health => health;
    public float Hunger => hunger;
    public float Thirst => thirst;
    public float Stamina => stamina;
    public float Oxygen => oxygen;

    private Rigidbody rb;
    public float DeathRespawnDelay => deathRespawnDelay;

    // Init / Reset
    public void SetDefaults()
    {
        // ensures everything is within bounds
        SetMaxHealth(maxHealth, true);
        SetMaxHunger(maxHunger, true);
        SetMaxThirst(maxThirst, true);
        SetMaxStamina(maxStamina, true);
        SetMaxOxygen(maxOxygen, true);

        isDead = (health <= 0f);

        staminaRegenDelayTimer = 0f;
        staminaWasDepleted = (stamina <= 0f);

        oxygenRegenDelayTimer = 0f;
        oxygenWasDepleted = (oxygen <= 0f);

        damageToTakePerTick = 0f;
        damageTickRateRuntime = 0f;
        damageDebuffTicks = 0;
        damageStartDelayRuntime = 0f;
        currentDamageDelayTimer = 0f;
        currentDamageTickTimer = 0f;
        damageStarted = false;

        hungerEmptyDamageDelayTimer = 0f;
        hungerEmptyDamageTickTimer = 0f;
        hungerEmptyDamageStarted = false;

        thirstEmptyDamageDelayTimer = 0f;
        thirstEmptyDamageTickTimer = 0f;
        thirstEmptyDamageStarted = false;

        oxygenEmptyDamageDelayTimer = 0f;
        oxygenEmptyDamageTickTimer = 0f;
        oxygenEmptyDamageStarted = false;

        InitUI();
        RefreshUI(false);
        UpdateDeathScreenState();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        SetDefaults();
    }

    void Update()
    {
        if (CanSimulateLocally())
        {
            TickDamage();
            TickHunger();
            TickThirst();
            TickEmptyValueDamage();

            // rudimentary death checks
            if (!isDead && health <= 0f)
            {
                Kill();
            }
        }

        if (speedText != null && rb != null)
        {
            speedText.SetText(System.Convert.ToInt32(rb.linearVelocity.magnitude).ToString());
        }

        UpdateDeathScreenState();
        TickUIVisibility();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // update bars when tweaking values
        maxHealth = Mathf.Max(0f, maxHealth);
        maxHunger = Mathf.Max(0f, maxHunger);
        maxThirst = Mathf.Max(0f, maxThirst);
        maxStamina = Mathf.Max(0f, maxStamina);
        maxOxygen = Mathf.Max(0f, maxOxygen);

        health = Mathf.Clamp(health, 0f, maxHealth);
        hunger = Mathf.Clamp(hunger, 0f, maxHunger);
        thirst = Mathf.Clamp(thirst, 0f, maxThirst);
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
        oxygen = Mathf.Clamp(oxygen, 0f, maxOxygen);

        hungerDrainPerSecond = Mathf.Max(0f, hungerDrainPerSecond);
        thirstDrainPerSecond = Mathf.Max(0f, thirstDrainPerSecond);

        hungerEmptyDamageDelay = Mathf.Max(0f, hungerEmptyDamageDelay);
        hungerEmptyDamagePerTick = Mathf.Max(0f, hungerEmptyDamagePerTick);
        hungerEmptyDamageTickRate = Mathf.Max(0.01f, hungerEmptyDamageTickRate);

        thirstEmptyDamageDelay = Mathf.Max(0f, thirstEmptyDamageDelay);
        thirstEmptyDamagePerTick = Mathf.Max(0f, thirstEmptyDamagePerTick);
        thirstEmptyDamageTickRate = Mathf.Max(0.01f, thirstEmptyDamageTickRate);

        oxygenEmptyDamageDelay = Mathf.Max(0f, oxygenEmptyDamageDelay);
        oxygenEmptyDamagePerTick = Mathf.Max(0f, oxygenEmptyDamagePerTick);
        oxygenEmptyDamageTickRate = Mathf.Max(0.01f, oxygenEmptyDamageTickRate);

        oxygenThrusterDrainPerSecond = Mathf.Max(0f, oxygenThrusterDrainPerSecond);
        oxygenDrainPerSecondInSpace = Mathf.Max(0f, oxygenDrainPerSecondInSpace);
        oxygenRegenPerSecond = Mathf.Max(0f, oxygenRegenPerSecond);
        oxygenRegenDelay = Mathf.Max(0f, oxygenRegenDelay);
        oxygenDepletedRegenDelay = Mathf.Max(0f, oxygenDepletedRegenDelay);

        deathRespawnDelay = Mathf.Max(0f, deathRespawnDelay);

        isDead = (health <= 0f);

        InitUI();
        RefreshUI(false);
        UpdateDeathScreenState();
    }
#endif

    void UpdateDeathScreenState()
    {
        if (deathScreenRoot == null) { return; }

        if (deathScreenRoot.activeSelf != isDead)
        {
            deathScreenRoot.SetActive(isDead);
        }
    }

    // UI
    public void RefreshUI(bool triggerAppear = true)
    {
        float h01 = (maxHealth <= 0f) ? 0f : (health / maxHealth);
        float hu01 = (maxHunger <= 0f) ? 0f : (hunger / maxHunger);
        float t01 = (maxThirst <= 0f) ? 0f : (thirst / maxThirst);
        float s01 = (maxStamina <= 0f) ? 0f : (stamina / maxStamina);
        float o01 = (maxOxygen <= 0f) ? 0f : (oxygen / maxOxygen);

        if (healthUI != null)
        {
            for (int i = 0; i < healthUI.Length; i++) { if (healthUI[i] != null) { healthUI[i].SetFill(h01, triggerAppear); } }
        }

        if (hungerUI != null)
        {
            for (int i = 0; i < hungerUI.Length; i++) { if (hungerUI[i] != null) { hungerUI[i].SetFill(hu01, triggerAppear); } }
        }

        if (thirstUI != null)
        {
            for (int i = 0; i < thirstUI.Length; i++) { if (thirstUI[i] != null) { thirstUI[i].SetFill(t01, triggerAppear); } }
        }

        if (staminaUI != null)
        {
            for (int i = 0; i < staminaUI.Length; i++) { if (staminaUI[i] != null) { staminaUI[i].SetFill(s01, triggerAppear); } }
        }

        if (oxygenUI != null)
        {
            for (int i = 0; i < oxygenUI.Length; i++) { if (oxygenUI[i] != null) { oxygenUI[i].SetFill(o01, triggerAppear); } }
        }
    }

    void InitUI()
    {
        float h01 = (maxHealth <= 0f) ? 0f : (health / maxHealth);
        float hu01 = (maxHunger <= 0f) ? 0f : (hunger / maxHunger);
        float t01 = (maxThirst <= 0f) ? 0f : (thirst / maxThirst);
        float s01 = (maxStamina <= 0f) ? 0f : (stamina / maxStamina);
        float o01 = (maxOxygen <= 0f) ? 0f : (oxygen / maxOxygen);

        if (healthUI != null)
        {
            for (int i = 0; i < healthUI.Length; i++) { if (healthUI[i] != null) { healthUI[i].Init(h01); } }
        }

        if (hungerUI != null)
        {
            for (int i = 0; i < hungerUI.Length; i++) { if (hungerUI[i] != null) { hungerUI[i].Init(hu01); } }
        }

        if (thirstUI != null)
        {
            for (int i = 0; i < thirstUI.Length; i++) { if (thirstUI[i] != null) { thirstUI[i].Init(t01); } }
        }

        if (staminaUI != null)
        {
            for (int i = 0; i < staminaUI.Length; i++) { if (staminaUI[i] != null) { staminaUI[i].Init(s01); } }
        }

        if (oxygenUI != null)
        {
            for (int i = 0; i < oxygenUI.Length; i++) { if (oxygenUI[i] != null) { oxygenUI[i].Init(o01); } }
        }
    }

    void TickUIVisibility()
    {
        if (healthUI != null)
        {
            for (int i = 0; i < healthUI.Length; i++) { if (healthUI[i] != null) { healthUI[i].Tick(); } }
        }

        if (hungerUI != null)
        {
            for (int i = 0; i < hungerUI.Length; i++) { if (hungerUI[i] != null) { hungerUI[i].Tick(); } }
        }

        if (thirstUI != null)
        {
            for (int i = 0; i < thirstUI.Length; i++) { if (thirstUI[i] != null) { thirstUI[i].Tick(); } }
        }

        if (staminaUI != null)
        {
            for (int i = 0; i < staminaUI.Length; i++) { if (staminaUI[i] != null) { staminaUI[i].Tick(); } }
        }

        if (oxygenUI != null)
        {
            for (int i = 0; i < oxygenUI.Length; i++) { if (oxygenUI[i] != null) { oxygenUI[i].Tick(); } }
        }
    }

    // Health
    public void SetHealth(float value, bool clamp = true)
    {
        health = clamp ? Mathf.Clamp(value, 0f, maxHealth) : value;
        isDead = (health <= 0f);
        RefreshUI();
        UpdateDeathScreenState();
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
        UpdateDeathScreenState();
    }

    /// <summary>
    /// Damage This Player over time.
    /// Damage thrown in will be applied per tick until debuffTicks is 0.
    /// If You need instant just use RemoveHealth().
    /// Keep in mind that if a request to take damage is received, it will update the current values the player is receiving unless the new one is weaker than the old.
    /// </summary>
    /// <param name="damage">How much damage the player receives per tick.</param>
    /// <param name="damageTickRate">How many seconds between each tick of damage.</param>
    /// <param name="debuffTicks">How many times this damage will trigger.</param>
    /// <param name="damageStartDelay">How long before the first damage tick starts.</param>
    public void TakeDamage(float damage, float damageTickRate, int debuffTicks, float damageStartDelay = 0f)
    {
        damage = Mathf.Clamp(damage, 0.1f, 100f);
        damageTickRate = Mathf.Clamp(damageTickRate, 0.01f, 60f);
        debuffTicks = Mathf.Clamp(debuffTicks, 1, 500);
        damageStartDelay = Mathf.Clamp(damageStartDelay, 0f, 60f);

        // if any variable is greater than the current variables just add it on.
        // pretty simple way to do poison damage.
        // if we do want it to be multiple debuffs it wont be hard to do.
        if (damageTickRate > damageTickRateRuntime) { damageTickRateRuntime = damageTickRate; }
        if (debuffTicks > damageDebuffTicks) { damageDebuffTicks = debuffTicks; }
        if (damage > damageToTakePerTick) { damageToTakePerTick = damage; }
        if (damageStartDelay > damageStartDelayRuntime) { damageStartDelayRuntime = damageStartDelay; }

        if (!damageStarted)
        {
            currentDamageDelayTimer = damageStartDelayRuntime;
            currentDamageTickTimer = 0f;
            damageStarted = true;
        }
    }

    public void TickDamage()
    {
        // if we've used all of our poison damage ticks, don't do anything.
        if (damageDebuffTicks <= 0)
        {
            damageToTakePerTick = 0f;
            damageTickRateRuntime = 0f;
            damageStartDelayRuntime = 0f;
            currentDamageDelayTimer = 0f;
            currentDamageTickTimer = 0f;
            damageStarted = false;
            return;
        }

        if (!damageStarted)
        {
            currentDamageDelayTimer = damageStartDelayRuntime;
            currentDamageTickTimer = 0f;
            damageStarted = true;
        }

        if (currentDamageDelayTimer > 0f)
        {
            currentDamageDelayTimer -= Time.deltaTime;
            return;
        }

        currentDamageTickTimer -= Time.deltaTime;

        if (currentDamageTickTimer <= 0f)
        {
            RemoveHealth(damageToTakePerTick);
            currentDamageTickTimer = damageTickRateRuntime;
            damageDebuffTicks--;
        }
    }

    // damage the player while survival values are empty
    void TickEmptyValueDamage()
    {
        if (isDead) { return; }

        TickHungerEmptyDamage();
        TickThirstEmptyDamage();
        TickOxygenEmptyDamage();
    }

    void TickHungerEmptyDamage()
    {
        if (hunger > 0f)
        {
            hungerEmptyDamageDelayTimer = 0f;
            hungerEmptyDamageTickTimer = 0f;
            hungerEmptyDamageStarted = false;
            return;
        }

        if (!hungerEmptyDamageStarted)
        {
            hungerEmptyDamageDelayTimer = hungerEmptyDamageDelay;
            hungerEmptyDamageTickTimer = 0f;
            hungerEmptyDamageStarted = true;
        }

        if (hungerEmptyDamageDelayTimer > 0f)
        {
            hungerEmptyDamageDelayTimer -= Time.deltaTime;
            return;
        }

        hungerEmptyDamageTickTimer -= Time.deltaTime;

        if (hungerEmptyDamageTickTimer <= 0f)
        {
            RemoveHealth(hungerEmptyDamagePerTick);
            hungerEmptyDamageTickTimer = hungerEmptyDamageTickRate;
        }
    }

    void TickThirstEmptyDamage()
    {
        if (thirst > 0f)
        {
            thirstEmptyDamageDelayTimer = 0f;
            thirstEmptyDamageTickTimer = 0f;
            thirstEmptyDamageStarted = false;
            return;
        }

        if (!thirstEmptyDamageStarted)
        {
            thirstEmptyDamageDelayTimer = thirstEmptyDamageDelay;
            thirstEmptyDamageTickTimer = 0f;
            thirstEmptyDamageStarted = true;
        }

        if (thirstEmptyDamageDelayTimer > 0f)
        {
            thirstEmptyDamageDelayTimer -= Time.deltaTime;
            return;
        }

        thirstEmptyDamageTickTimer -= Time.deltaTime;

        if (thirstEmptyDamageTickTimer <= 0f)
        {
            RemoveHealth(thirstEmptyDamagePerTick);
            thirstEmptyDamageTickTimer = thirstEmptyDamageTickRate;
        }
    }

    void TickOxygenEmptyDamage()
    {
        if (oxygen > 0f)
        {
            oxygenEmptyDamageDelayTimer = 0f;
            oxygenEmptyDamageTickTimer = 0f;
            oxygenEmptyDamageStarted = false;
            return;
        }

        if (!oxygenEmptyDamageStarted)
        {
            oxygenEmptyDamageDelayTimer = oxygenEmptyDamageDelay;
            oxygenEmptyDamageTickTimer = 0f;
            oxygenEmptyDamageStarted = true;
        }

        if (oxygenEmptyDamageDelayTimer > 0f)
        {
            oxygenEmptyDamageDelayTimer -= Time.deltaTime;
            return;
        }

        oxygenEmptyDamageTickTimer -= Time.deltaTime;

        if (oxygenEmptyDamageTickTimer <= 0f)
        {
            RemoveHealth(oxygenEmptyDamagePerTick);
            oxygenEmptyDamageTickTimer = oxygenEmptyDamageTickRate;
        }
    }

    public void Kill()
    {
        health = 0f;
        isDead = true;
        RefreshUI();
        UpdateDeathScreenState();
    }

    public void Revive(float healthPercent = 1f)
    {
        isDead = false;

        SetHealth(maxHealth * Mathf.Clamp01(healthPercent), true);
        RefreshUI();
        UpdateDeathScreenState();
    }

    // Hunger
    public void SetHunger(float value, bool clamp = true)
    {
        hunger = clamp ? Mathf.Clamp(value, 0f, maxHunger) : value;
        RefreshUI();
    }

    public void AddHunger(float amount)
    {
        SetHunger(hunger + amount, true);
    }

    public void RemoveHunger(float amount)
    {
        SetHunger(hunger - Mathf.Abs(amount), true);
    }

    public void SetMaxHunger(float value, bool refill = false)
    {
        maxHunger = Mathf.Max(0f, value);

        if (refill) { hunger = maxHunger; }
        else { hunger = Mathf.Clamp(hunger, 0f, maxHunger); }

        RefreshUI();
    }

    // Call once per frame to drain hunger.
    public void TickHunger(bool drain = true)
    {
        hungerDrainDelay = Mathf.Clamp(hungerDrainDelay, 0f, maxHungerDrainDelay);

        if (hungerDrainDelay > 0f) { return; }
        if (!drain) { return; }

        DrainHunger(hungerDrainPerSecond * Time.deltaTime);
    }

    public void DrainHunger(float amount)
    {
        SetHunger(hunger - amount, true);
    }

    public bool HasHunger(float min = 0.01f)
    {
        return hunger > min;
    }

    public void AddHungerDelay(float delay)
    {
        hungerDrainDelay += delay;
        StartCoroutine(TickHungerDelay());
    }

    private IEnumerator TickHungerDelay()
    {
        while (hungerDrainDelay > 0f)
        {
            hungerDrainDelay -= Time.deltaTime;
            yield return null;
        }
    }

    // Thirst
    public void SetThirst(float value, bool clamp = true)
    {
        thirst = clamp ? Mathf.Clamp(value, 0f, maxThirst) : value;
        RefreshUI();
    }

    public void AddThirst(float amount)
    {
        SetThirst(thirst + amount, true);
    }

    public void RemoveThirst(float amount)
    {
        SetThirst(thirst - Mathf.Abs(amount), true);
    }

    public void SetMaxThirst(float value, bool refill = false)
    {
        maxThirst = Mathf.Max(0f, value);

        if (refill) { thirst = maxThirst; }
        else { thirst = Mathf.Clamp(thirst, 0f, maxThirst); }

        RefreshUI();
    }

    // Call once per frame to drain thirst.
    public void TickThirst(bool drain = true)
    {
        thirstDrainDelay = Mathf.Clamp(thirstDrainDelay, 0f, maxThirstDrainDelay);

        if (thirstDrainDelay > 0f) { return; }
        if (!drain) { return; }

        DrainThirst(thirstDrainPerSecond * Time.deltaTime);
    }

    public void DrainThirst(float amount)
    {
        SetThirst(thirst - amount, true);
    }

    public bool HasThirst(float min = 0.01f)
    {
        return thirst > min;
    }

    public void AddThirstDelay(float delay)
    {
        thirstDrainDelay += delay;
        StartCoroutine(TickThirstDelay());
    }

    private IEnumerator TickThirstDelay()
    {
        while (thirstDrainDelay > 0f)
        {
            thirstDrainDelay -= Time.deltaTime;
            yield return null;
        }
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
        if (!allowRegen) { return; }

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

    // Oxygen
    public void SetOxygen(float value, bool clamp = true)
    {
        oxygen = clamp ? Mathf.Clamp(value, 0f, maxOxygen) : value;

        if (oxygen <= 0f) { oxygenWasDepleted = true; }

        RefreshUI();
        UpdateDeathScreenState();
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

        oxygenWasDepleted = (oxygen <= 0f);

        RefreshUI();
        UpdateDeathScreenState();
    }

    // Call once per frame to drain/regenerate oxygen used by thrusters.
    public void TickOxygenThrusterUsage(bool usingOxygen, bool allowRegen, float drainMultiplier)
    {
        if (usingOxygen)
        {
            DrainOxygen((oxygenThrusterDrainPerSecond * Mathf.Max(0f, drainMultiplier)) * Time.deltaTime);

            if (oxygenWasDepleted)
            {
                oxygenRegenDelayTimer = oxygenDepletedRegenDelay;
            }
            else
            {
                oxygenRegenDelayTimer = oxygenRegenDelay;
            }

            return;
        }

        if (!allowRegen) { return; }

        if (oxygenRegenDelayTimer > 0f)
        {
            oxygenRegenDelayTimer -= Time.deltaTime;
            return;
        }

        RegenOxygen(oxygenRegenPerSecond * Time.deltaTime);
    }

    public void TickPassiveOxygenDrainInSpace(bool inSpace)
    {
        if (!inSpace) { return; }

        DrainOxygen(oxygenDrainPerSecondInSpace * Time.deltaTime);

        if (oxygenWasDepleted)
        {
            oxygenRegenDelayTimer = oxygenDepletedRegenDelay;
        }
        else
        {
            oxygenRegenDelayTimer = oxygenRegenDelay;
        }
    }

    public void DrainOxygen(float amount)
    {
        float prev = oxygen;

        SetOxygen(oxygen - amount, true);

        if (prev > 0f && oxygen <= 0f)
        {
            oxygenWasDepleted = true;
        }
    }

    public void RegenOxygen(float amount)
    {
        if (amount <= 0f) { return; }

        if (oxygenWasDepleted && oxygen <= 0f)
        {
            if (oxygenRegenDelayTimer > 0f) { return; }
        }

        SetOxygen(oxygen + amount, true);

        if (oxygen > 0f)
        {
            oxygenWasDepleted = false;
        }
    }

    public bool HasOxygen(float min = 0.01f)
    {
        return oxygen > min;
    }

    public void RespawnReset()
    {
        isDead = false;

        health = maxHealth;
        hunger = maxHunger;
        thirst = maxThirst;
        stamina = maxStamina;
        oxygen = maxOxygen;

        staminaRegenDelayTimer = 0f;
        staminaWasDepleted = false;

        oxygenRegenDelayTimer = 0f;
        oxygenWasDepleted = false;

        damageToTakePerTick = 0f;
        damageTickRateRuntime = 0f;
        damageDebuffTicks = 0;
        damageStartDelayRuntime = 0f;
        currentDamageDelayTimer = 0f;
        currentDamageTickTimer = 0f;
        damageStarted = false;

        hungerEmptyDamageDelayTimer = 0f;
        hungerEmptyDamageTickTimer = 0f;
        hungerEmptyDamageStarted = false;

        thirstEmptyDamageDelayTimer = 0f;
        thirstEmptyDamageTickTimer = 0f;
        thirstEmptyDamageStarted = false;

        oxygenEmptyDamageDelayTimer = 0f;
        oxygenEmptyDamageTickTimer = 0f;
        oxygenEmptyDamageStarted = false;

        RefreshUI();
        UpdateDeathScreenState();
    }

    // Utility
    public void SetAll(
        float newHealth, float newMaxHealth,
        float newHunger, float newMaxHunger,
        float newThirst, float newMaxThirst,
        float newStamina, float newMaxStamina,
        float newOxygen, float newMaxOxygen
    )
    {
        SetMaxHealth(newMaxHealth, false);
        SetHealth(newHealth, true);

        SetMaxHunger(newMaxHunger, false);
        SetHunger(newHunger, true);

        SetMaxThirst(newMaxThirst, false);
        SetThirst(newThirst, true);

        SetMaxStamina(newMaxStamina, false);
        SetStamina(newStamina, true);

        SetMaxOxygen(newMaxOxygen, false);
        SetOxygen(newOxygen, true);

        RefreshUI();
        UpdateDeathScreenState();
    }

    private bool CanSimulateLocally()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();

        if (netObj == null || !netObj.IsSpawned) { return true; }
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) { return true; }

        return NetworkManager.Singleton.IsServer;
    }
}