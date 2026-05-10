using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Made By: Jason Lodge
// Summary: Handles leaving a session.
// Shuts down session and loads a scene.
public class MS_SessionEnder : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string sceneName;

    [Header("Settings")]
    [SerializeField] private float loadDelay = 0.1f;
    [SerializeField] private bool enableCursor = true;

    private bool endingSession;

    public void EndSession()
    {
        if (endingSession) { return; }

        StartCoroutine(EndSessionRoutine());
    }

    private IEnumerator EndSessionRoutine()
    {
        endingSession = true;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown(true);
        }

        if (loadDelay > 0f)
        {
            yield return new WaitForSeconds(loadDelay);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}