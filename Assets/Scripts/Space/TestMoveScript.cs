using UnityEngine;

public class TestMoveScript : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private Vector3 direction;
    void Update()
    {
        transform.position += direction * (Time.deltaTime * speed);
    }
}
