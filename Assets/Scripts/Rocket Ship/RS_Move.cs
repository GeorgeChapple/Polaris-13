using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using System;
using TMPro;

// Made by: George Chapple, Jason Lodge
// Summary: we're not actually moving the ship, we're going to be moving all of the stuff inside the parent

public class RS_Move : NetworkBehaviour
{
    [Header("Global Values")]
    public NetworkVariable<Vector3> worldPosition = new NetworkVariable<Vector3>();
    public NetworkVariable<Vector3> worldDirectionNetworked = new NetworkVariable<Vector3>();
    public Vector3 worldDirection;
    public NetworkVariable<Vector3> targetPosition = new NetworkVariable<Vector3>();
    public NetworkVariable<float> speed = new NetworkVariable<float>();
    public NetworkVariable<float> targetSpeed = new NetworkVariable<float>();
    public NetworkVariable<float> health = new NetworkVariable<float>();
    public NetworkVariable<float> fuel = new NetworkVariable<float>();
    public NetworkVariable<float> oxygen = new NetworkVariable<float>();

    [Header("Settings")]
    [SerializeField] private float speedChangeRate = 5;
    [SerializeField] private float fuelDrainRate = 1;
    [SerializeField] private float oxygenDrainRate = 1;
    [SerializeField] private float maxSpeed = 20;
    [SerializeField] private float maxHealth = 1000;
    [SerializeField] private float maxFuel = 5000;
    [SerializeField] private float maxOxygen = 5000;

    [Header("Meters")]
    [SerializeField] private Renderer[] meters;
    [SerializeField] private Vector2[] metersFills;
    private Material[] metersMaterials = new Material[7];

    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI positionText;
    [SerializeField] private TextMeshProUGUI targetText;
    [SerializeField] private TextMeshProUGUI jumpText;
    [SerializeField] private TextMeshProUGUI POI_Text;

    [Header("Screen References")]
    [SerializeField] private Renderer smallScreenL;
    [SerializeField] private Renderer smallScreenR;


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    private void Start()
    {
        if (IsServer)
        {
            health.Value = maxHealth;
            fuel.Value = maxFuel;
            oxygen.Value = maxOxygen;
            targetPosition.Value = new Vector3(0, 0, 100000);
            worldPosition.Value = Vector3.zero;
            targetSpeed.Value = maxSpeed / 4f;
        }
        for (int i = 0; i < meters.Length; ++i)
        {
            metersMaterials[i] = meters[i].materials[1];
        }
    }

    void Update()
    {
        UpdateMeters();
        UpdateText();
        UpdateScreens();
        if (!IsServer)
        {
            return;
        }
        UpdateDirection();
        MoveShip();
        TickOxygen();
        TickFuel();
    }

    [Rpc(SendTo.Server)]
    public void EmergencyStopRpc()
    {
        targetSpeed.Value = 0;
    }

    private void MoveShip()
    {
        if (fuel.Value > 0 && (targetPosition.Value - worldPosition.Value).magnitude > targetSpeed.Value)
        {
            speed.Value = Mathf.Lerp(speed.Value, targetSpeed.Value, Time.deltaTime * speedChangeRate);
        }
        else
        {
            speed.Value = Mathf.Lerp(speed.Value, 0, Time.deltaTime * speedChangeRate);
        }
        worldPosition.Value = Vector3.Lerp(worldPosition.Value, worldPosition.Value + (worldDirection * speed.Value), Time.deltaTime);
    }

    private void UpdateDirection()
    {
        Vector3 targetDirection = (targetPosition.Value - worldPosition.Value).normalized;
        worldDirection = Vector3.Slerp(worldDirection, targetDirection, Time.deltaTime);
        worldDirectionNetworked.Value = worldDirection;
    }

    private void TickOxygen()
    {
        if (oxygen.Value > 0)
        {
            oxygen.Value = Mathf.Lerp(oxygen.Value, oxygen.Value - oxygenDrainRate * NetworkManager.ConnectedClientsList.Count, Time.deltaTime);
        }
    }

    private void TickFuel()
    {
        if (fuel.Value > 0)
        {
            fuel.Value = Mathf.Lerp(fuel.Value, fuel.Value - fuelDrainRate * speed.Value, Time.deltaTime);
        }
    }

    private void UpdateMeters()
    {
        metersMaterials[0].SetFloat("_FillAmount", Mathf.Lerp(metersFills[0].x, metersFills[0].y, health.Value / maxHealth));
        metersMaterials[1].SetFloat("_FillAmount", Mathf.Lerp(metersFills[1].x, metersFills[1].y, speed.Value / maxSpeed));
        metersMaterials[2].SetFloat("_FillAmount", Mathf.Lerp(metersFills[2].x, metersFills[2].y, oxygen.Value / maxOxygen));
        metersMaterials[3].SetFloat("_FillAmount", Mathf.Lerp(metersFills[3].x, metersFills[3].y, fuel.Value / maxFuel));
        metersMaterials[4].SetFloat("_FillAmount", Mathf.Lerp(metersFills[4].x, metersFills[4].y, fuel.Value / maxFuel));
        metersMaterials[5].SetFloat("_FillAmount", Mathf.Lerp(metersFills[5].x, metersFills[5].y, fuel.Value / maxFuel));
        metersMaterials[6].SetFloat("_FillAmount", Mathf.Lerp(metersFills[6].x, metersFills[6].y, fuel.Value / maxFuel));
    }

    private void UpdateText()
    {
        speedText.text = "SPEED: " + Math.Truncate(Mathf.Lerp(0, 100, speed.Value / maxSpeed)).ToString() + "%";
        positionText.text = "X: " + Math.Truncate(worldPosition.Value.x) + " Y: " + Math.Truncate(worldPosition.Value.y) + " Z: " + Math.Truncate(worldPosition.Value.z);
        targetText.text = "X: " + Math.Truncate(targetPosition.Value.x) + " Y: " + Math.Truncate(targetPosition.Value.y) + " Z: " + Math.Truncate(targetPosition.Value.z);
    }

    private void UpdateScreens()
    {
        smallScreenL.material.SetVector("_Offset_Multiplier", new Vector4(0, Mathf.Lerp(0, 1, speed.Value / maxSpeed), 0, 0));
        smallScreenR.material.SetVector("_Offset", new Vector4(Mathf.Lerp(10, 1, health.Value / maxHealth), 0, 0, 0));
        smallScreenR.material.SetFloat("_NoiseScale", Mathf.Lerp(10, 0, oxygen.Value / maxOxygen));
        smallScreenR.material.SetFloat("_Amplitude", Mathf.Lerp(0, 0.35f, fuel.Value / maxFuel));
    }
}