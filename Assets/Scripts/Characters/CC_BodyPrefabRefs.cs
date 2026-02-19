using Unity.Cinemachine;
using UnityEngine;

// Made by: Jason Lodge
// Summary: Reference holder for the spawned body prefab.
// Notes:
// - Put this on the BodyCapsule prefab root and wire the fields in inspector.

public class CC_BodyPrefabRefs : MonoBehaviour
{
    [Header("Core")]
    public CapsuleCollider bodyCapsule;

    [Header("Transforms")]
    public Transform cameraRoot;
    public Transform groundedCheck;
    public Transform dropItemTransform;

    [Header("Camera")]
    public Camera mainCamera;

    [Tooltip("Cinemachine camera component (the one you tweak lens etc on).")]
    public CinemachineCamera cCam;

    [HideInInspector] public bool ready = false;

    private void Awake()
    {
        bodyCapsule = GetComponent<CapsuleCollider>();

        cameraRoot = transform.Find("Camera Root");
        groundedCheck = transform.Find("GroundedCheck");
        dropItemTransform = transform.Find("DropItemTransform");

        mainCamera = cameraRoot.Find("Main Camera").GetComponent<Camera>();
        cCam = cameraRoot.Find("PlayerFollowCamera").GetComponent<CinemachineCamera>();

        ready = true;
    }
}