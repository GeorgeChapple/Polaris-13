using UnityEngine;

// Made by: George Chapple, Jason Lodge

public class RS_Move : MonoBehaviour
{
    public Vector3 position;
    public Vector3 direction;
    public float speed;

    public enum moveMode { Manual, Automatic }

    public moveMode mode = moveMode.Manual;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (mode == moveMode.Manual)
        {
            MoveShip(position, direction); //actually should only need direction
        }
        else
        {
            MoveShip(position);
        }
    }

    void MoveShip(Vector3 posToMoveTo)
    {
        position = Vector3.Lerp(position, direction * speed, Time.deltaTime);
    }

    void MoveShip(Vector3 posToMoveTo, Vector3 direction)
    {
        position = Vector3.Lerp(position, direction * speed, Time.deltaTime);
    }
}
