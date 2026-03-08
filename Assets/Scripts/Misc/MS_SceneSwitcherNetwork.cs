using Unity.Netcode;
using UnityEngine;

public class MS_SceneSwitcherNetwork : MonoBehaviour
{
    public void ChangeScene(string sceneName)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
