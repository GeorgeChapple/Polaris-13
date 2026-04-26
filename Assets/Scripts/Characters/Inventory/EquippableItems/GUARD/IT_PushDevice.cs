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

    private float pushForce = 0f;
    private float chargingTimer = 0f;
    private float cooldownTimer = 0f;

    private float pushForceLocal = 0f;
    private float chargingTimerLocal = 0f;
    private float cooldownTimerLocal = 0f;

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

        if (cooldownTimer <= 0f)
        {
            chargingTimerLocal += Time.deltaTime;
            chargingTimerLocal = Mathf.Clamp(chargingTimerLocal, 0f, Mathf.Max(0.001f, chargeTime));

            float safeChargeTime = Mathf.Max(0.001f, chargeTime);
            pushForceLocal = Mathf.Lerp(0f, maxPushForce, Mathf.Clamp(chargingTimerLocal, 0f, safeChargeTime) / safeChargeTime);
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

    public override void OnUseReleased(NetworkObjectReference netObjRef) {
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
        pushForce = 0;
        chargingTimer = 0;
    }

    public override void OnUseReleasedLocally(NetworkObjectReference netObjRef)
    {
        cooldownTimerLocal = cooldown;
        pushForceLocal = 0;
        chargingTimerLocal = 0;
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
        float chargetimerPercentage = Mathf.Max(minimumFill, chargingTimer) / chargeTime;
        uIChargeImage.fillAmount = chargetimerPercentage / (1 - minimumFill);
        uIChargeText.SetText($"{Mathf.Round(chargingTimer * 100) / 100}/{chargeTime}");

        if (!NetworkManager.Singleton.IsHost)
        {
            cooldownTimerLocal -= Time.deltaTime;
            chargetimerPercentage = Mathf.Max(minimumFill, chargingTimerLocal) / chargeTime;
            uIChargeImage.fillAmount = chargetimerPercentage / (1 - minimumFill);
            uIChargeText.SetText($"{Mathf.Round(chargingTimerLocal * 100) / 100}/{chargeTime}");
        }
    }

    private void OnDrawGizmos()
    {
        CustomGizmos.DrawBox(muzzlePoint.transform.position + (pushVolumeForwardOffset + pushVolumeBounds.z / 2) * muzzlePoint.forward, Quaternion.LookRotation(muzzlePoint.forward), pushVolumeBounds, Color.cyan);
    }
}
