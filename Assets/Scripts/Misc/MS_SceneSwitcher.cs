using UnityEngine;
using UnityEngine.SceneManagement;

public class MS_SceneSwitcher : MonoBehaviour
{
    public void ChangeScene(string sceneNmae)
    {
        Scene s = SceneManager.GetSceneByName(sceneNmae);
        SceneManager.SetActiveScene(s);
    }
}
