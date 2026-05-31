using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: Loading screen controller.
public class LoadingScreen : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject loadingPanel;
    public Image progressBar;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI progressStateText;

    private Coroutine progressCoroutine;
    private bool registered = false;

    private void Start()
    {
        TryRegisterSceneEvents();
        if (loadingPanel != null) { loadingPanel.SetActive(false); }
    }

    private void OnDestroy()
    {
        if (registered && NetworkManager.Singleton == null && NetworkManager.Singleton.SceneManager == null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    private void TryRegisterSceneEvents()
    {
        if (registered) { return; }
        if (NetworkManager.Singleton == null && NetworkManager.Singleton.SceneManager == null) { return; }
        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        registered = true;
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Load:
                ShowLoadingUI(sceneEvent);
                break;

            case SceneEventType.LoadComplete:
            case SceneEventType.LoadEventCompleted:
                //HideLoadingUI();
                break;
        }
    }

    private void ShowLoadingUI(SceneEvent sceneEvent)
    {
        if (loadingPanel != null) { loadingPanel.SetActive(true); }
        if (progressCoroutine != null) { StopCoroutine(progressCoroutine); }

        progressCoroutine = StartCoroutine(UpdateProgress(sceneEvent));
    }

    //private void HideLoadingUI()
    //{
    //    if (progressCoroutine != null)
    //    {
    //        StopCoroutine(progressCoroutine);
    //        progressCoroutine = null;
    //    }

    //    if (loadingPanel != null) { loadingPanel.SetActive(false); }
    //}

    private IEnumerator UpdateProgress(SceneEvent sceneEvent)
    {
        if (sceneEvent.AsyncOperation == null)
        {
            SetProgress(0f, sceneEvent.SceneEventType);
            yield break;
        }

        while (!sceneEvent.AsyncOperation.isDone)
        {
            float progress = Mathf.Clamp01(sceneEvent.AsyncOperation.progress / 0.9f);
            SetProgress(progress, sceneEvent.SceneEventType);

            yield return null;
        }

        SetProgress(1f, sceneEvent.SceneEventType);
    }

    private void SetProgress(float progress, SceneEventType sceneEventType)
    {
        if (progressBar != null) { progressBar.fillAmount = progress; }
        if (progressText != null) { progressText.SetText(Mathf.RoundToInt(progress * 100f) + "%"); }
        if (progressStateText != null) { progressStateText.SetText(sceneEventType.ToString()); }
    }

    public void HostLoadScene(string sceneName)
    {
        TryRegisterSceneEvents();
        if (NetworkManager.Singleton == null) { return; }
        if (!NetworkManager.Singleton.IsServer) { return; }
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}