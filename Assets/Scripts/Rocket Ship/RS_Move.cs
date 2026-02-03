using UnityEngine;

public class RS_Move : MonoBehaviour
{
    public Vector3 position;
    public Vector3 direction;
    public float speed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        position = Vector3.Lerp(position, direction * speed, Time.deltaTime);
    }
}
