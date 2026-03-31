using Unity.Netcode;
using UnityEngine;

// Script By : George Chapple
// Summary   : 

public class POI_Portal : NetworkBehaviour
{
    [SerializeField] private Transform destination;


    private void Awake()
    {
        InitialiseComponents();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            enabled = false;
            return;
        }

        InitialiseComponents();
    }

    private void InitialiseComponents()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (destination != null && other.GetComponent<CC_CharacterPlayerController>())
        {
            other.GetComponent<Rigidbody>().position = destination.position;
        }
    }
}
