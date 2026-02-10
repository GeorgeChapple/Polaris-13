using UnityEngine;

// Made by: George Chapple, Jason Lodge
// Summary: we're not actually moving the ship, we're going to be moving all of the stuff inside the parent

public class RS_Move : MonoBehaviour
{
    [Header("References")]
    public GameObject globalParent;

    [Header("Global Values")]
    public Vector3 worldPosition;
    public Vector3 worldDirection;
    public Vector3 targetPosition;
    [HideInInspector] public float speed;
    public float targetSpeed;

    public enum moveMode { Manual, Automatic, Deactivated }

    public moveMode mode = moveMode.Manual;

    [Header("Settings")]
    [SerializeField] private float speedChangeAmt = 5;
    [SerializeField] private float directionChangeSpeedMax = 5;
    [SerializeField] private float directionChangeSpeedMin = 5;
    [SerializeField] private float directionChangeSpeedAuto = 5;

    [SerializeField] private Vector3 anchorPoint = Vector3.zero;

    private Vector2 controllerDir = Vector2.zero; // max of 1 on both axis positive and negative
    

    void Update()
    {
        if (mode != moveMode.Deactivated)
        {
            UpdateDirection();
            MoveShip();
        }
    }

    void MoveShip()
    {
        if ((targetPosition - worldPosition).magnitude > targetSpeed)
        {
            speed = Mathf.Lerp(speed, targetSpeed, Time.deltaTime);
        }
        else
        {
            speed = Mathf.Lerp(speed, 0, Time.deltaTime);
        }
            worldPosition = Vector3.Lerp(worldPosition, worldPosition + (worldDirection * speed), Time.deltaTime);
        // lerp to target speed and clamp to target speed when close enough


        // move ship in direction using speed value


    }

    void UpdateDirection()
    {
        if (mode == moveMode.Manual)
        {
            // change world direction based on distance of controller v2 from zero

            // calculate percentage of vec2 controller from zero to max
            // add to world direction using calculated speed value with max and min clamp
        }
        else if (mode == moveMode.Automatic)
        {
            Vector3 targetDirection = (targetPosition - worldPosition).normalized;
            worldDirection = Vector3.Slerp(worldDirection, targetDirection, Time.deltaTime);
        }
    }

    void ChangeSpeed(bool upOrDown, float amount, bool emergencyStop)
    {
        if (emergencyStop) { targetSpeed = 0; return; }

        if (upOrDown) // up
        {
            targetSpeed += amount;
        }
        else // down
        {
            targetSpeed -= amount;
        }
        // intervals of 5 i think
    }
}