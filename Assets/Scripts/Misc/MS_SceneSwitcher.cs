using UnityEngine;
using UnityEngine.SceneManagement;

public class MS_SceneSwitcher : MonoBehaviour
{
    public void ChangeScene(int index)
    {
        SceneManager.LoadScene(index);
    }
}
