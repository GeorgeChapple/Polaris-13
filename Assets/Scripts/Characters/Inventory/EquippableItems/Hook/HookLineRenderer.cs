using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System.Collections;

public class HookLineRenderer : NetworkBehaviour
{
    [SerializeField] private GameObject ropePrefab;
    [SerializeField] private GameObject hookPrefab;
    private IT_Hook hookManager;
    private HookHead hookHead;
    private LineRenderer line;
    private List<Transform> ropePoints = new List<Transform>();
    private HookState state = HookState.ready;
    private Transform muzzle;

    private enum HookState
    {
        ready,
        launching,
        deployed,
        reeling
    }

    private void Start()
    {
        muzzle = transform.parent;
        line = GetComponent<LineRenderer>();
        hookManager = transform.root.GetComponent<IT_Hook>();
        ropePoints.Add(transform);
        Transform parent = transform;
        NetworkObject netObj;
        for (int i = 1; i < hookManager.segmentCount; i++)
        {
            GameObject newSegment = Instantiate(ropePrefab);
            newSegment.transform.position = muzzle.position;
            newSegment.transform.rotation = muzzle.rotation;
            newSegment.transform.SetParent(muzzle, true);
            newSegment.GetComponent<HingeJoint>().connectedBody = parent.GetComponent<Rigidbody>();
            netObj = newSegment.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
            parent = newSegment.transform;
            ropePoints.Add(parent);
        }
        GameObject newHook = Instantiate(hookPrefab);
        newHook.transform.position = muzzle.position;
        newHook.transform.rotation = muzzle.rotation;
        newHook.transform.SetParent(muzzle, true);
        newHook.GetComponent<HingeJoint>().connectedBody = parent.GetComponent<Rigidbody>();
        netObj = null;
        netObj = newHook.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
        hookHead = newHook.GetComponent<HookHead>();
        line.positionCount = ropePoints.Count;
        SetKinematic(true);
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < ropePoints.Count; i++)
        {
            line.SetPosition(i, ropePoints[i].position);
        }
    }

    public void TriggerHook()
    {
        switch (state)
        {
            case 0:
                StartCoroutine(FireHook()); 
                break;
            default:
                break;
        }
    }

    private void SetKinematic(bool isKinematic)
    { 
        for (int i = 1;  i < ropePoints.Count; i++)
        {
            ropePoints[i].GetComponent<Rigidbody>().isKinematic = isKinematic;
        }
        hookHead.GetComponent<Rigidbody>().isKinematic = isKinematic;
    }

    private IEnumerator FireHook()
    {
        state = HookState.launching;
        Vector3 forward = muzzle.transform.forward;
        Vector3 destination  = muzzle.transform.position + forward * hookManager.length;
        while ((hookHead.transform.position - destination).magnitude > 0.1f && state == HookState.launching)
        {
            hookHead.transform.position = Vector3.Lerp(hookHead.transform.position, hookHead.transform.position + forward * hookManager.shootSpeed, Time.deltaTime);
            for (int i = 0; i < ropePoints.Count; i++)
            {
                float t = i / (ropePoints.Count - 1);
                ropePoints[i].position = Vector3.Lerp(transform.position, destination, t);
            }
            yield return null;
        }
        SetKinematic(false);
        state = HookState.deployed;
    }
}
