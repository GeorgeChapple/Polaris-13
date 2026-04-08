using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class HookLineRenderer : MonoBehaviour
{
    private LineRenderer line;
    private List<Transform> ropePoints = new List<Transform>();

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        ropePoints.Add(transform);
        Transform parent = transform;
        while (true)
        {
            if (parent.GetChild(0).CompareTag("ROPE"))
            {
                parent = parent.GetChild(0);
                ropePoints.Add(parent);
            }
            else
            {
                break;
            }
        }
        line.positionCount = ropePoints.Count;
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < ropePoints.Count; i++)
        {
            line.SetPosition(i, ropePoints[i].position);
        }
    }
}
