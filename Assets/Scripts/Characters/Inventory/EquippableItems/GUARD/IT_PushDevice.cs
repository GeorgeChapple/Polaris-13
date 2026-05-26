using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class IT_PushDevice : CC_INV_UsableItems
{
    private string itemId;

    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Vector3 pushVolumeBounds = new Vector3(5f, 5f, 10f);
    [SerializeField] private float pushVolumeForwardOffset = 0f;
    [SerializeField] private float maxPushForce = 10f;
    [SerializeField] private float torqueForce = 1f;
    [SerializeField] private float chargeTime = 2f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private bool ignoreSelf = true;

    [SerializeField] private Image uIChargeImage;
    [SerializeField] private TextMeshProUGUI uIChargeText;
    [SerializeField] private float minimumFill;
    [SerializeField] private AUD_SFX sfx;
    [SerializeField] private AUD_SFX sfxChime;

    private float pushForce = 0f;
    private float chargingTimer = 0f;
    private float cooldownTimer = 0f;

    private float pushForceLocal = 0f;
    private float chargingTimerLocal = 0f;
    private float cooldownTimerLocal = 0f;

    private bool hasPlayedChargeSound = false;
    private bool hasPlayedChimeSound = false;

    private NetworkObject playerWhoSent;

    public override void SendItemId(string ItemId)
    {
        itemId = ItemId;
    }

    public override void SendSender(NetworkObjectReference netObj)
    {
        playerWhoSent = netObj;
    }

    public override void OnUseHeldLocally(NetworkObjectReference netObjRef)
    {
        if (muzzlePoint == null)
        {
            return;
        }

        if (cooldownTimerLocal <= 0f)
        {
            if (!hasPlayedChargeSound)
            {
                if (sfx != null)
                {
                    sfx.PlaySound("Charge");
                }

                hasPlayedChargeSound = true;
            }

            chargingTimerLocal += Time.deltaTime;
            chargingTimerLocal = Mathf.Clamp(chargingTimerLocal, 0f, Mathf.Max(0.001f, chargeTime));

            if (chargingTimerLocal >= chargeTime && !hasPlayedChimeSound)
            {
                if (sfxChime != null)
                {
                    sfxChime.PlaySound(0);
                }

                hasPlayedChimeSound = true;
            }

            float safeChargeTime = Mathf.Max(0.001f, chargeTime);
            pushForceLocal = Mathf.Lerp(0f, maxPushForce, Mathf.Clamp(chargingTimerLocal, 0f, safeChargeTime) / safeChargeTime);
        }
        else
        {
            hasPlayedChargeSound = false;
        }
    }

    public override void OnUseHeld(NetworkObjectReference netObjRef)
    {
        if (muzzlePoint == null)
        {
            return;
        }

        if (cooldownTimer <= 0f)
        {
            chargingTimer += Time.deltaTime;
            chargingTimer = Mathf.Clamp(chargingTimer, 0f, Mathf.Max(0.001f, chargeTime));

            float safeChargeTime = Mathf.Max(0.001f, chargeTime);
            pushForce = Mathf.Lerp(0f, maxPushForce, Mathf.Clamp(chargingTimer, 0f, safeChargeTime) / safeChargeTime);
        }
    }

    public override void OnUseReleased(NetworkObjectReference netObjRef)
    {
        if (cooldownTimer > 0f)
        {
            return;
        }

        cooldownTimer = cooldown;

        Collider[] colliders = Physics.OverlapBox(muzzlePoint.transform.position + (pushVolumeForwardOffset + pushVolumeBounds.z / 2) * muzzlePoint.forward, pushVolumeBounds / 2, Quaternion.LookRotation(muzzlePoint.forward));

        foreach (Collider collider in colliders)
        {
            if (collider.GetComponent<CC_Movement>())
            {
                PushPlayerRpc(collider, playerWhoSent);
            }
            else
            {
                PushObject(collider);
            }
        }

        pushForce = 0f;
        chargingTimer = 0f;
    }

    public override void OnUseReleasedLocally(NetworkObjectReference netObjRef)
    {
        if (cooldownTimerLocal > 0f)
        {
            return;
        }

        if (sfx != null)
        {
            sfx.PlaySound("Blast");
        }

        cooldownTimerLocal = cooldown;
        pushForceLocal = 0f;
        chargingTimerLocal = 0f;
        hasPlayedChargeSound = false;
        hasPlayedChimeSound = false;
    }

    private void PushObject(Collider col)
    {
        Rigidbody rb = col.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.AddForce(muzzlePoint.forward * pushForce);
            rb.AddTorque(Vector3.one * Random.Range(-torqueForce, torqueForce));
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PushPlayerRpc(Collider col, NetworkObjectReference us)
    {
        if (us.TryGet(out NetworkObject usNetObj) && (col.GetComponent<CC_Movement>() != usNetObj.GetComponent<CC_Movement>() || !ignoreSelf))
        {
            CC_Movement player = col.GetComponent<CC_Movement>();

            if (player != null)
            {
                player.PushSelf(muzzlePoint.forward * pushForce);
            }
        }
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;
        cooldownTimerLocal -= Time.deltaTime;

        cooldownTimer = Mathf.Max(0f, cooldownTimer);
        cooldownTimerLocal = Mathf.Max(0f, cooldownTimerLocal);

        UpdateChargeUI(chargingTimer, cooldownTimer);

        if (!NetworkManager.Singleton.IsHost)
        {
            UpdateChargeUI(chargingTimerLocal, cooldownTimerLocal);
        }
    }

    private void UpdateChargeUI(float currentChargingTimer, float currentCooldownTimer)
    {
        if (uIChargeImage == null) { return; }
        if (uIChargeText == null) { return; }

        uIChargeImage.fillAmount = GetChargeFill(currentChargingTimer, currentCooldownTimer);

        if (currentCooldownTimer > 0f)
        {
            uIChargeText.SetText("Cooling");
            return;
        }

        if (currentChargingTimer >= chargeTime)
        {
            uIChargeText.SetText("Charged");
            return;
        }

        if (currentCooldownTimer <= 0f && currentChargingTimer <= 0f)
        {
            uIChargeText.SetText("Ready");
            return;
        }

        float roundedChargeTimer = Mathf.Round(currentChargingTimer * 100f) / 100f;
        uIChargeText.SetText($"{roundedChargeTimer}/{chargeTime}");
    }

    private float GetChargeFill(float currentChargingTimer, float currentCooldownTimer)
    {
        float safeMinimumFill = Mathf.Clamp01(minimumFill);
        float safeChargeTime = Mathf.Max(0.001f, chargeTime);
        float safeCooldown = Mathf.Max(0.001f, cooldown);

        if (currentCooldownTimer > 0f)
        {
            float cooldownPercentage = Mathf.Clamp01(currentCooldownTimer / safeCooldown);
            return Mathf.Lerp(safeMinimumFill, 1f, cooldownPercentage);
        }

        float chargePercentage = Mathf.Clamp01(currentChargingTimer / safeChargeTime);
        return Mathf.Lerp(safeMinimumFill, 1f, chargePercentage);
    }

    private void OnDrawGizmos()
    {
        CustomGizmos.DrawBox(muzzlePoint.transform.position + (pushVolumeForwardOffset + pushVolumeBounds.z / 2) * muzzlePoint.forward, Quaternion.LookRotation(muzzlePoint.forward), pushVolumeBounds, Color.cyan);
    }
}