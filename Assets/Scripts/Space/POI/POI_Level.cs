using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Script By : George Chapple
// Summary   : 

[RequireComponent(typeof(BoxCollider))]
public class POI_Level : NetworkBehaviour
{
    public Vector3 bounds = new Vector3(100, 100, 100); 
    [HideInInspector] public POI_Portal mainPortal;
    [SerializeField] private Transform startPosition;
    [SerializeField] private POI_Portal exitPortal;
    private int players = 0;
    private SP_SpaceManager spaceManager;

    // Getters
    public int Players => players;
    public Transform StartPosition => startPosition;
    public POI_Portal ExitPortal => exitPortal;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        spaceManager = FindFirstObjectByType<SP_SpaceManager>();
        GetComponent<BoxCollider>().size = bounds;
        mainPortal.portalEntered.AddListener(AddPlayers);
        exitPortal.portalEntered.AddListener(SubtractPlayers);
    }

    private void Update()
    {
        if (mainPortal == null && players == 0)
        {
            if (IsServer)
            {
                GetComponent<NetworkObject>().Despawn(true);
            }
            Destroy(this.gameObject);
        }
    }

    private void OnTriggerExit(Collider col)
    {
        StartCoroutine(WaitTriggerExit(col));
    }

    private IEnumerator WaitTriggerExit(Collider col)
    {
        float t = 0;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        NetworkObject netObj = col.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            if (netObj.GetComponent<CC_Movement>() && !spaceManager.cannotTeleport.Contains(col))
            {
                PlayerExitSpacePOIRpc(netObj);
            }
            else
            {
                ObjectExitSpacePOIRpc(netObj);
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void ObjectExitSpacePOIRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        {
            SP_SpaceJunk spaceObj = netObj.GetComponent<SP_SpaceJunk>();
            if (spaceObj != null)
            {
                spaceObj.StartLerpScale(spaceObj.transform.localScale, Vector3.zero, true);
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PlayerExitSpacePOIRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        {
            CC_Movement player = netObj.GetComponent<CC_Movement>();
            if (player != null)
            {
                player.Body.linearVelocity = Vector3.zero;
                if (exitPortal.destination != null)
                {
                    player.Body.position = exitPortal.destination.position;
                }
                else
                { 
                    player.Body.position = GameObject.FindGameObjectsWithTag("SpawnPoint")[player.OwnerClientId].transform.position;
                }
            }
        }
    }

    private void AddPlayers()
    {
        players++;
    }

    private void SubtractPlayers()
    {
        players--;
    }

    private void OnDrawGizmos()
    {
        DrawBox(transform.position, transform.rotation, bounds, Color.red);
    }

    public void DrawBox(Vector3 pos, Quaternion rot, Vector3 scale, Color c)
    {
        Matrix4x4 m = new Matrix4x4();
        m.SetTRS(pos, rot, scale);

        var point1 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, 0.5f));
        var point2 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, 0.5f));
        var point3 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, -0.5f));
        var point4 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, -0.5f));

        var point5 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, 0.5f));
        var point6 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, 0.5f));
        var point7 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, -0.5f));
        var point8 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, -0.5f));

        Debug.DrawLine(point1, point2, c);
        Debug.DrawLine(point2, point3, c);
        Debug.DrawLine(point3, point4, c);
        Debug.DrawLine(point4, point1, c);

        Debug.DrawLine(point5, point6, c);
        Debug.DrawLine(point6, point7, c);
        Debug.DrawLine(point7, point8, c);
        Debug.DrawLine(point8, point5, c);

        Debug.DrawLine(point1, point5, c);
        Debug.DrawLine(point2, point6, c);
        Debug.DrawLine(point3, point7, c);
        Debug.DrawLine(point4, point8, c);
    }
}
