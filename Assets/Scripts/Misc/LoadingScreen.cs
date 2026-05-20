using System.Collections;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject loadingPanel;
    public Image progressBar;
    public TextMeshProUGUI progressText;

    string loadingScene;
    bool registered = false;

    private void Start()
    {
        //LoadingScreen[] objs = FindObjectsByType<LoadingScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        //if (objs.Length > 1 && objs.Contains(this))
        //{
        //    Destroy(this);
        //}
        //else { DontDestroyOnLoad(this); }
    }

    private void OnDestroy()
    {
        //if (NetworkManager.Singleton != null)
        //{
        //    NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        //}
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Load:
                ShowLoadingUI();
                break;

            case SceneEventType.LoadComplete:
                HideLoadingUI();
                break;
        }
    }

    private void ShowLoadingUI()
    {
        loadingPanel.SetActive(true);
        StartCoroutine(UpdateProgress());
    }

    private void HideLoadingUI()
    {
        loadingPanel.SetActive(false);
        StopAllCoroutines();
    }

    private IEnumerator UpdateProgress()
    {
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(loadingScene);
        asyncOp.allowSceneActivation = false;

        while (!asyncOp.isDone)
        {
            progressBar.fillAmount = Mathf.Clamp01(asyncOp.progress / 0.9f);
            progressText.text = Mathf.RoundToInt(asyncOp.progress * 100) + "%";
            Debug.Log("Loading: " + Mathf.RoundToInt(asyncOp.progress * 100) + "%");
            yield return null;
        }
    }

    public void HostLoadScene(string sceneName)
    {
        //loadingScene = sceneName;
        //if (NetworkManager.Singleton.IsServer)
        //{
        //    if (!registered)
        //    {
        //        // subscribe to scene events
        //        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        //        registered = true;
        //    }
        //    NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        //}
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
