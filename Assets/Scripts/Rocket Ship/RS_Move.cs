using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

// Made by: George Chapple, Jason Lodge
// Summary: we're not actually moving the ship, we're going to be moving all of the stuff inside the parent

public class RS_Move : NetworkBehaviour
{
    [Header("Global Values")]
    public Vector3 worldPosition;
    public NetworkVariable<Vector3> worldDirectionNetworked = new NetworkVariable<Vector3>();
    public Vector3 worldDirection;
    public Vector3 targetPosition;
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
    private Material[] metersMaterials = new Material[4];


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    private void Start()
    {
        health.Value = maxHealth;
        fuel.Value = maxFuel;
        oxygen.Value = maxOxygen;
        for (int i = 0; i < meters.Length; ++i)
        {
            metersMaterials[i] = meters[i].materials[1];
        }
    }

    void Update()
    {
        UpdateMeters();
        if (!IsServer)
        {
            return;
        }

        Debug.Log("Oxygen drain : " + oxygenDrainRate * NetworkManager.ConnectedClientsList.Count);
        Debug.Log("Fuel drain : " + fuelDrainRate * speed.Value);
        UpdateDirection();
        MoveShip();
        TickOxygen();
        TickFuel();
    }

    private void MoveShip()
    {
        if (fuel.Value > 0 && (targetPosition - worldPosition).magnitude > targetSpeed.Value)
        {
            speed.Value = Mathf.Lerp(speed.Value, targetSpeed.Value, Time.deltaTime * speedChangeRate);
        }
        else
        {
            speed.Value = Mathf.Lerp(speed.Value, 0, Time.deltaTime * speedChangeRate);
        }
        worldPosition = Vector3.Lerp(worldPosition, worldPosition + (worldDirection * speed.Value), Time.deltaTime);
    }

    private void UpdateDirection()
    {
        Vector3 targetDirection = (targetPosition - worldPosition).normalized;
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
    }
}