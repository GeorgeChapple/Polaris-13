using UnityEngine;

public class UI_MenuManager : MonoBehaviour
{
    private void Awake()
    {
        Application.targetFrameRate = 60;
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}
