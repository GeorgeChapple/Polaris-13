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
        GetComponent<BoxCollider>().size = bounds;
        mainPortal.portalEntered.AddListener(AddPlayers);
        exitPortal.portalEntered.AddListener(SubtractPlayers);
    }

    private void Update()
    {
        if (mainPortal == null && players == 0)
        {
            Destroy(this.gameObject);
        }
    }

    private void OnTriggerExit(Collider col)
    {
        CC_Movement player = col.GetComponent<CC_Movement>();
        SP_SpaceJunk spaceObj = col.GetComponent<SP_SpaceJunk>();
        if (player != null && player.canTeleport)
        {
            SubtractPlayers();
            player.Body.position = GameObject.FindGameObjectsWithTag("SpawnPoint")[player.OwnerClientId].transform.position;
        }
        else if (spaceObj != null && spaceObj.canTeleport)
        {
            spaceObj.StartLerpScale(spaceObj.transform.localScale, Vector3.zero, true);
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
