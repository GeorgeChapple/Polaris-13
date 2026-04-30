using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Made by: Jason Lodge
// Summary: Warning System Handler.
public class RS_WarningSystem : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject[] warningLights;

    [Header("Settings")]
    [SerializeField] private WarningData[] warnings;

    private float[] warningTimers; // runtime refs to tick timers
    private float warningLightTimer; // runtime ref to warning light tick timer
    private bool warningLightState; // runtime ref to warning light flash state

    [System.Serializable]
    public class WarningData
    {
        public string name;
        public Image imageToAffect;
        public Sprite warningSprite;
        public int tickRate;
        public bool shipWarning;
        public bool active;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Init();
    }

    private void Update()
    {
        // tick active warnings on ui.
        for (int i = 0; i < warnings.Length; i++)
        {
            if (warnings[i] != null)
            {
                if (warnings[i].active && warnings[i].shipWarning)
                {
                    TickWarning(i);
                }
            }
        }

        TickWarningLights();
    }

    private void Init()
    {
        warningTimers = new float[warnings.Length];

        for (int i = 0; i < warnings.Length; i++)
        {
            if (warnings[i] != null)
            {
                if (warnings[i].imageToAffect != null)
                {
                    warnings[i].imageToAffect.sprite = warnings[i].warningSprite;
                    warnings[i].imageToAffect.enabled = false;
                }
            }
        }

        foreach (GameObject warningLight in warningLights)
        {
            if (warningLight != null)
            {
                warningLight.SetActive(false);
            }
        }
    }

    private void TickWarning(int warningIndex)
    {
        if (warnings[warningIndex].imageToAffect == null) { return; }
        if (warnings[warningIndex].tickRate <= 0) { warnings[warningIndex].tickRate = 1; }

        warningTimers[warningIndex] += Time.deltaTime;

        if (warningTimers[warningIndex] >= 1f / warnings[warningIndex].tickRate)
        {
            warningTimers[warningIndex] = 0f;

            warnings[warningIndex].imageToAffect.enabled = !warnings[warningIndex].imageToAffect.enabled;
        }
    }

    private void TickWarningLights()
    {
        WarningData activeWarning = GetActiveShipWarning();

        if (activeWarning == null)
        {
            warningLightTimer = 0f;
            warningLightState = false;

            foreach (GameObject warningLight in warningLights)
            {
                if (warningLight != null)
                {
                    warningLight.SetActive(false);
                }
            }

            return;
        }

        if (activeWarning.tickRate <= 0) { activeWarning.tickRate = 1; }

        warningLightTimer += Time.deltaTime;

        if (warningLightTimer >= 1f / activeWarning.tickRate)
        {
            warningLightTimer = 0f;
            warningLightState = !warningLightState;

            foreach (GameObject warningLight in warningLights)
            {
                if (warningLight != null)
                {
                    warningLight.SetActive(warningLightState);
                }
            }
        }
    }

    private WarningData GetActiveShipWarning()
    {
        foreach (WarningData warning in warnings)
        {
            if (warning != null)
            {
                if (warning.active && warning.shipWarning)
                {
                    return warning;
                }
            }
        }

        return null;
    }

    public WarningData[] GetWarnings()
    {
        return warnings;
    }

    public void CallWarningByName(string warningName)
    {
        for (int i = 0; i < warnings.Length; i++)
        {
            if (warnings[i] != null)
            {
                if (warnings[i].name == warningName && warnings[i].shipWarning)
                {
                    warnings[i].active = true;
                    warningTimers[i] = 0f;

                    if (warnings[i].imageToAffect != null)
                    {
                        warnings[i].imageToAffect.enabled = true;
                    }

                    return;
                }
            }
        }
    }

    public void EndWarningByName(string warningName)
    {
        for (int i = 0; i < warnings.Length; i++)
        {
            if (warnings[i] != null)
            {
                if (warnings[i].name == warningName && warnings[i].shipWarning)
                {
                    warnings[i].active = false;
                    warningTimers[i] = 0f;

                    if (warnings[i].imageToAffect != null)
                    {
                        warnings[i].imageToAffect.enabled = false;
                    }

                    return;
                }
            }
        }
    }
}