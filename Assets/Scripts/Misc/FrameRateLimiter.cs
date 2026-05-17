using UnityEngine;

public class FrameRateLimiter : MonoBehaviour
{
    [SerializeField] private int targetFps = 60;

    private void Awake()
    {
        Application.targetFrameRate = targetFps;
    }
}