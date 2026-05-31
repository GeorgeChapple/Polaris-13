using Unity.Netcode;
using UnityEngine;

public class GravityToggler : NetworkBehaviour
{
    [SerializeField] private bool useGravity;
    [SerializeField] private bool shipField;
    [SerializeField] private bool planet;

    private void Start()
    {
        if (!IsSpawned)
        {
            GetComponent<NetworkObject>().Spawn();
        }
    }

    private void OnTriggerEnter(Collider col)
    {
        CustomGravityRigidbody gravityBody = col.GetComponent<CustomGravityRigidbody>();
        if (gravityBody != null && !gravityBody.triggered)
        {
            gravityBody.triggered = true;
            gravityBody.useGravity = useGravity;
            if (shipField)
            {
                ToggleShip(gravityBody);
            }
        }
    }

    private void OnTriggerExit(Collider col)
    {
        CustomGravityRigidbody gravityBody = col.GetComponent<CustomGravityRigidbody>();
        if (gravityBody != null)
        {
            gravityBody.triggered = false;
            if (planet)
            {
                gravityBody.useGravity = !useGravity; 
                if (shipField)
                {
                    ToggleShip(gravityBody);
                }
            }
        }
    }

    private void ToggleShip(CustomGravityRigidbody gravityBody)
    {
        if (shipField && IsServer)
        {
            SP_SpaceManager spaceManager = FindFirstObjectByType<SP_SpaceManager>();
            RS_Move rocket = FindFirstObjectByType<RS_Move>();
            CC_Movement player = gravityBody.GetComponent<CC_Movement>();
            SP_SpaceJunk debris = gravityBody.GetComponent<SP_SpaceJunk>();
            
            if (gravityBody.useGravity)
            {
                if (debris != null)
                {
                    debris.moveMult = 0;
                }
            }
            else
            {
                if (debris != null)
                {
                    debris.originDirection = rocket.worldDirectionNetworked.Value;
                    debris.moveMult = 1;
                }
            }
            //if (gravityBody.useGravity)
            //{
            //    if (player != null)
            //    {
            //        player.drift.Value = false;
            //    }
            //    else
            //    {
            //        if (spaceManager.debris.ContainsKey(gravityBody.gameObject))
            //        {
            //            spaceManager.debris.Remove(gravityBody.gameObject);
            //        }
            //    }
            //} 
            //else
            //{
            //    if (player != null)
            //    {
            //        player.drift.Value = true;
            //        player.referenceDirection.Value = rocket.worldDirectionNetworked.Value;
            //    }
            //    else
            //    {
            //        if (!spaceManager.debris.ContainsKey(gravityBody.gameObject))
            //        {
            //            spaceManager.debris.Add(gravityBody.gameObject, rocket.worldDirectionNetworked.Value);
            //        } 
            //    }
            //}
        }
    }
}
