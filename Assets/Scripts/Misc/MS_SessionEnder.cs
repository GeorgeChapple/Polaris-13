using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Made By: Jason Lodge
// Summary: Handles leaving a session.
// Host shuts down session.
// Clients use built in LeaveSession button.
// Listens for the current session ending and loads a scene.
public class MS_SessionEnder : NetworkBehaviour
{
    [Header("Scene")]
    [SerializeField] private string sceneName;

    [Header("Buttons")]
    [Tooltip("Button gameobject used by the host to end the session.")]
    [SerializeField] private GameObject hostEndSessionButton;

    [Tooltip("Button gameobject used by clients to leave the session using the built in LeaveSession component.")]
    [SerializeField] private GameObject clientLeaveSessionButton;

    [Header("Settings")]
    [SerializeField] private bool enableCursor = true;

    private bool endingSession;

    public override void OnNetworkSpawn()
    {
        SetupButtons();
        SubscribeToNetworkEvents();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void Start()
    {
        SetupButtons();
        SubscribeToNetworkEvents();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        UnsubscribeFromNetworkEvents();
    }

    private void SetupButtons()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (!NetworkManager.Singleton.IsListening)
        {
            return;
        }

        if (NetworkManager.Singleton.IsHost)
        {
            if (clientLeaveSessionButton != null)
            {
                Destroy(clientLeaveSessionButton);
            }

            return;
        }

        if (hostEndSessionButton != null)
        {
            Destroy(hostEndSessionButton);
        }
    }

    private void SubscribeToNetworkEvents()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        NetworkManager.Singleton.OnServerStopped += OnServerStopped;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
    }

    public void EndSession()
    {
        if (endingSession) { return; }

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost)
        {
            return;
        }

        endingSession = true;

        ResetSessionData();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown(true);
        }

        ExitToScene();
    }

    public void FinishSessionExit()
    {
        if (endingSession) { return; }

        endingSession = true;

        ResetSessionData();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown(true);
        }

        ExitToScene();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (endingSession) { return; }
        if (NetworkManager.Singleton == null) { return; }

        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            return;
        }

        endingSession = true;

        ResetSessionData();
        ExitToScene();
    }

    private void OnServerStopped(bool hostWasRunning)
    {
        if (endingSession) { return; }

        endingSession = true;

        ResetSessionData();
        ExitToScene();
    }

    private void ResetSessionData()
    {
        if (INV_ItemDatabase.Instance != null)
        {
            INV_ItemDatabase.Instance.ResetHidden();
            INV_ItemDatabase.Instance.ResetItemsUnlocked();
        }
    }

    private void ExitToScene()
    {
        if (enableCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}