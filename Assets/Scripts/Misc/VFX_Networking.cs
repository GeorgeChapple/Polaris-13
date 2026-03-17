using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkedVFX : NetworkBehaviour
{
    [SerializeField] private float lifeTime = 2f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        StartCoroutine(DestroyAfterTime());

    }

    private IEnumerator DestroyAfterTime()
    {
        yield return new WaitForSeconds(lifeTime);

        if (GetComponent<NetworkObject>() != null && GetComponent<NetworkObject>().IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public static void SpawnVFX(GameObject vfxPrefab, Vector3 position, Quaternion rotation)
    {
        if (vfxPrefab == null)
        {
            return;
        }

        GameObject vfx = Instantiate(vfxPrefab, position, rotation);
        NetworkObject netObj = vfx.GetComponent<NetworkObject>();

        if (netObj != null && !netObj.IsSpawned)
        {
            netObj.Spawn();
        }
    }
}