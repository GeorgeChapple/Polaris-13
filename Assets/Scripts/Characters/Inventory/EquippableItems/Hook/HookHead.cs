using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class HookHead : NetworkBehaviour
{
    private enum HookState
    {
        Idle = 0,
        Launching = 1,
        Deployed = 2,
        Returning = 3
    }

    private static readonly List<HookHead> activeHooks = new List<HookHead>();

    [Header("Refs")]
    [SerializeField] private Rigidbody rb;

    [Header("Dragging")]
    [SerializeField] private LayerMask draggableLayers;
    [SerializeField] private float draggedObjectPullSpeed = 14f;
    [SerializeField] private float returnSpeedMultiplier = 1.15f;

    [Header("Return")]
    [SerializeField] private float returnStopDistance = 0.75f;

    [Header("Anchor")]
    [Tooltip("If true, the hook will anchor the shooter when it deploys into the world.")]
    [SerializeField] private bool anchorShooterOnDeploy = true;

    [Tooltip("Maximum distance the shooter can move away from the hook while anchored.")]
    [SerializeField] private float anchorMaxDistance = 8f;

    [Tooltip("Extra distance allowed before hard clamping back to max rope length.")]
    [SerializeField] private float anchorDistancePadding = 0.1f;

    [Tooltip("How strongly the hook pulls inward when outside rope length. Higher = snappier.")]
    [SerializeField] private float anchorPullLerpStrength = 0.1f;

    [Tooltip("Maximum inward correction speed applied by the rope.")]
    [SerializeField] private float anchorMaxPullSpeed = 1f;

    [Tooltip("How much outward rope velocity gets removed when fully extended. 1 = fully remove.")]
    [SerializeField, Range(0f, 1f)] private float anchorOutwardVelocityDamping = 1f;

    private readonly NetworkVariable<ulong> shooterNetworkObjectId = new NetworkVariable<ulong>
    (
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> currentState = new NetworkVariable<int>
    (
        (int)HookState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Transform returnTarget;
    private Vector3 fireStartPosition;
    private float throwPower;
    private float maxDistance;
    private Rigidbody draggedBody;
    private INV_ItemDrop draggedItemDrop;

    private GameObject hitObject;
    private Vector3 offsetFromHitObjectCenter;
    private Quaternion rotationFromHitObject;

    public ulong ShooterNetworkObjectId => shooterNetworkObjectId.Value;

    private void Awake()
    {
        if (rb == null) { rb = GetComponent<Rigidbody>(); }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!activeHooks.Contains(this)) { activeHooks.Add(this); }
    }

    public override void OnNetworkDespawn()
    {
        ClearShooterAnchor();
        activeHooks.Remove(this);
        base.OnNetworkDespawn();
    }

    private void FixedUpdate()
    {
        if (!IsServer) { return; }

        HookState state = (HookState)currentState.Value;

        switch (state)
        {
            case HookState.Launching:
                TickLaunching();
                break;

            case HookState.Deployed:
                TickDeployed();
                break;

            case HookState.Returning:
                TickReturning();
                break;
        }
    }

    public void Fire(ulong shooterId, Transform muzzle, float newThrowPower, float distance)
    {
        if (!IsServer || muzzle == null) { return; }

        shooterNetworkObjectId.Value = shooterId;
        returnTarget = muzzle;
        throwPower = Mathf.Max(0f, newThrowPower);
        maxDistance = Mathf.Max(0f, distance);
        fireStartPosition = muzzle.position;

        draggedBody = null;
        draggedItemDrop = null;
        hitObject = null;
        offsetFromHitObjectCenter = Vector3.zero;
        rotationFromHitObject = Quaternion.identity;

        ClearShooterAnchor();

        transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.WakeUp();
            rb.linearVelocity = muzzle.forward * throwPower;
            rb.angularVelocity = Vector3.zero;
        }

        currentState.Value = (int)HookState.Launching;
    }

    public void BeginReturn(Transform muzzle)
    {
        if (!IsServer || muzzle == null) { return; }

        ClearShooterAnchor();

        returnTarget = muzzle;
        currentState.Value = (int)HookState.Returning;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.WakeUp();
        }
    }

    private void TickLaunching()
    {
        float distanceTravelled = Vector3.Distance(fireStartPosition, transform.position);

        // if we didnt hit anything by max distance, just return
        if (distanceTravelled >= maxDistance) { BeginReturn(returnTarget); }
    }

    private void TickDeployed()
    {
        if (hitObject == null)
        {
            BeginReturn(returnTarget);
            return;
        }

        Vector3 targetPosition = hitObject.transform.TransformPoint(offsetFromHitObjectCenter);
        Quaternion targetRotation = hitObject.transform.rotation * rotationFromHitObject;

        if (rb != null)
        {
            rb.MovePosition(targetPosition);
            rb.MoveRotation(targetRotation);
        }
        else
        {
            transform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        TickShooterAnchor();
    }

    private void TickReturning()
    {
        if (returnTarget == null)
        {
            DespawnHook();
            return;
        }

        Vector3 toTarget = returnTarget.position - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= returnStopDistance)
        {
            TryHandleDraggedItemPickup();
            DespawnHook();
            return;
        }

        Vector3 direction = toTarget.normalized;
        float returnSpeed = Mathf.Max(throwPower * returnSpeedMultiplier, draggedObjectPullSpeed);

        if (rb != null) { rb.linearVelocity = direction * returnSpeed; }
        else { transform.position += direction * returnSpeed * Time.fixedDeltaTime; }

        if (draggedBody != null) { draggedBody.linearVelocity = direction * draggedObjectPullSpeed; }
    }

    private void StopAndDeploy()
    {
        currentState.Value = (int)HookState.Deployed;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (!rb.isKinematic) { rb.isKinematic = true; }
        }

        TryStartShooterAnchor();
    }

    private void TryStartShooterAnchor()
    {
        if (!IsServer) { return; }
        if (!anchorShooterOnDeploy) { return; }
        if (draggedBody != null) { return; }

        NetworkObject shooterNetObj = GetShooterNetworkObject();
        if (shooterNetObj == null) { return; }

        CC_Movement movement = shooterNetObj.GetComponent<CC_Movement>();
        if (movement == null) { return; }

        movement.SetHookMoveForOwner
        (
            transform.position,
            anchorMaxDistance,
            anchorPullLerpStrength,
            anchorMaxPullSpeed,
            anchorOutwardVelocityDamping
        );
    }

    private void TickShooterAnchor()
    {
        if (!IsServer) { return; }
        if (!anchorShooterOnDeploy) { return; }
        if (draggedBody != null) { return; }

        NetworkObject shooterNetObj = GetShooterNetworkObject();
        if (shooterNetObj == null) { return; }

        CC_Movement movement = shooterNetObj.GetComponent<CC_Movement>();
        if (movement == null) { return; }

        movement.SetHookMoveForOwner
        (
            transform.position,
            anchorMaxDistance,
            anchorPullLerpStrength,
            anchorMaxPullSpeed,
            anchorOutwardVelocityDamping
        );
    }

    private void ClearShooterAnchor()
    {
        if (!IsServer) { return; }

        NetworkObject shooterNetObj = GetShooterNetworkObject();
        if (shooterNetObj == null) { return; }

        CC_Movement movement = shooterNetObj.GetComponent<CC_Movement>();
        if (movement == null) { return; }

        movement.ClearHookMoveForOwner();
    }

    private void DespawnHook()
    {
        ClearShooterAnchor();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None;
        }

        if (draggedBody != null) { draggedBody.linearVelocity = Vector3.zero; }

        if (NetworkObject != null && NetworkObject.IsSpawned) { NetworkObject.Despawn(true); }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) { return; }

        HookState state = (HookState)currentState.Value;
        if (state != HookState.Launching) { return; }

        if (collision == null || collision.transform == null) { return; }
        if (IsCollisionWithShooter(collision)) { return; }

        Rigidbody otherRb = collision.rigidbody;
        hitObject = otherRb != null ? otherRb.gameObject : collision.collider != null ? collision.collider.gameObject : collision.gameObject;

        // store where the hook is in the hit objects local space so it follows like a child
        offsetFromHitObjectCenter = hitObject.transform.InverseTransformPoint(transform.position);
        rotationFromHitObject = Quaternion.Inverse(hitObject.transform.rotation) * transform.rotation;

        if (otherRb != null && IsOnDraggableLayer(hitObject.layer))
        {
            draggedBody = otherRb;
            draggedItemDrop = otherRb.GetComponent<INV_ItemDrop>();

            BeginReturn(returnTarget);
            return;
        }

        StopAndDeploy();
    }

    private bool IsCollisionWithShooter(Collision collision)
    {
        NetworkObject otherNetObj = collision.transform.GetComponentInParent<NetworkObject>();
        if (otherNetObj == null) { return false; }

        return otherNetObj.NetworkObjectId == shooterNetworkObjectId.Value;
    }

    private bool IsOnDraggableLayer(int layer)
    {
        return (draggableLayers.value & (1 << layer)) != 0;
    }

    private void TryHandleDraggedItemPickup()
    {
        if (!IsServer) { return; }
        if (draggedItemDrop == null) { return; }

        NetworkObject shooterNetObj = GetShooterNetworkObject();
        if (shooterNetObj == null) { return; }

        // if the dragged object is an item drop, try to add it straight to inventory
        if (draggedItemDrop != null) { draggedItemDrop.TryAddToInventory(shooterNetObj.gameObject); }
    }

    private NetworkObject GetShooterNetworkObject()
    {
        if (NetworkManager == null || NetworkManager.SpawnManager == null) { return null; }

        NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(shooterNetworkObjectId.Value, out NetworkObject shooterNetObj);
        return shooterNetObj;
    }

    public static HookHead FindActiveHookForShooter(ulong shooterId)
    {
        for (int i = 0; i < activeHooks.Count; i++)
        {
            HookHead hook = activeHooks[i];
            if (hook == null) { continue; }
            if (!hook.IsSpawned) { continue; }
            if (hook.ShooterNetworkObjectId != shooterId) { continue; }

            return hook;
        }

        return null;
    }
}