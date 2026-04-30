using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using static RS_WarningSystem;

// Made by: Jason Lodge
// Summary: Warning System listener and local handler for player.
public class UI_WarningSystem : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform warningParent;
    [SerializeField] private GameObject warningPrefab;

    [Header("Settings")]
    [SerializeField] private WarningData[] warnings;

    private Image[] warningUIs; // runtime refs to images on ui
    private float[] warningTimers; // runtime refs to tick timers
    private RS_WarningSystem ship; // runtime ref to ship warning system

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Init();
        SyncWarnings();
    }

    private void Update()
    {
        // tick active warnings on ui.
        for (int i = 0; i < warnings.Length; i++)
        {
            if (warnings[i] != null && warningUIs[i] != null)
            {
                if (warnings[i].active)
                {
                    // flash.
                    TickWarning(i);
                }
            }
        }
    }

    private void Init()
    {
        warningUIs = new Image[warnings.Length];
        warningTimers = new float[warnings.Length];

        for (int i = 0; i < warnings.Length; i++)
        {
            if (warnings[i] != null)
            {
                GameObject warningObj = Instantiate(warningPrefab, warningParent);

                Image warningImage = warningObj.GetComponent<Image>();
                if (warningImage == null) { warningImage = warningObj.GetComponentInChildren<Image>(); }

                warningUIs[i] = warningImage;

                if (warningUIs[i] != null)
                {
                    warningUIs[i].sprite = warnings[i].warningSprite;
                    warningUIs[i].enabled = false;
                }
            }
        }
    }

    private void SyncWarnings()
    {
        ship = FindFirstObjectByType<RS_WarningSystem>();

        if (ship == null) { return; }

        WarningData[] shipWarnings = ship.GetWarnings();

        foreach (WarningData shipWarning in shipWarnings)
        {
            if (shipWarning != null)
            {
                if (shipWarning.active)
                {
                    CallWarningByName(shipWarning.name);
                }
            }
        }
    }

    private void TickWarning(int warningIndex)
    {
        if (warnings[warningIndex].tickRate <= 0) { warnings[warningIndex].tickRate = 1; }

        warningTimers[warningIndex] += Time.deltaTime;

        if (warningTimers[warningIndex] >= 1f / warnings[warningIndex].tickRate)
        {
            warningTimers[warningIndex] = 0f;

            warningUIs[warningIndex].enabled = !warningUIs[warningIndex].enabled;
        }
    }

    public void CallWarningByName(string warningName)
    {
        for (int i = 0; i < warnings.Length; i++)
        {
            if (warnings[i] != null)
            {
                if (warnings[i].name == warningName)
                {
                    warnings[i].active = true;
                    warningTimers[i] = 0f;

                    if (warningUIs[i] != null)
                    {
                        warningUIs[i].enabled = true;
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
                if (warnings[i].name == warningName)
                {
                    warnings[i].active = false;
                    warningTimers[i] = 0f;

                    if (warningUIs[i] != null)
                    {
                        warningUIs[i].enabled = false;
                    }

                    return;
                }
            }
        }
    }
}