using UnityEngine;

// Made by: Jason Lodge
// Summary: Stabilises a visual body child to the gravity up axis and rotates it around a body pivot like a joint.

public class CC_BodyVisualStabiliser : MonoBehaviour
{
    [Header("References")]
    public CC_Movement movement;

    [Tooltip("Pivot/body transform the visual rotates around.")]
    public Transform bodyPivot;

    [Tooltip("Visual root to move and rotate. If null, uses this transform.")]
    public Transform visualRoot;

    [Header("Rotation")]
    [Tooltip("How quickly the visual body rotates while stabilising.")]
    public float visualRotateSharpness = 12f;

    [Tooltip("If alignment is in snap zone, snap visual instantly.")]
    public bool snapWhenFullyAligned = true;

    [Tooltip("If true, visual body follows camera forward while in space.")]
    public bool followCameraForwardInSpace = true;

    Quaternion visualRotation = Quaternion.identity;
    Vector3 initialPivotLocalOffset;
    bool initialised;

    void Awake()
    {
        Initialise();
    }

    void Initialise()
    {
        if (initialised) { return; }
        initialised = true;

        if (movement == null) { movement = GetComponentInParent<CC_Movement>(); }
        if (visualRoot == null) { visualRoot = transform; }
        if (bodyPivot == null && movement != null && movement.Body != null) { bodyPivot = movement.Body.transform; }
        if (bodyPivot == null) { bodyPivot = transform.parent; }

        if (visualRoot != null)
        {
            visualRotation = visualRoot.rotation;
        }

        if (visualRoot != null && bodyPivot != null)
        {
            initialPivotLocalOffset = Quaternion.Inverse(bodyPivot.rotation) * (visualRoot.position - bodyPivot.position);
        }
    }

    void LateUpdate()
    {
        if (movement == null || visualRoot == null || bodyPivot == null) { return; }

        // only the local owner should drive the live visual body.
        // remote instances should use the replicated pose instead.
        if (!movement.IsLocallyControlled())
        {
            return;
        }

        UpdateVisualRotation();
    }

    void UpdateVisualRotation()
    {
        Vector3 upAxis = movement.UpAxis;
        bool hasUsableGravity = movement.HasUsableGravity();
        float alignStrength = movement.GetGravityAlignmentSharpness();

        Vector3 targetForward;

        if (!hasUsableGravity && followCameraForwardInSpace && movement.CameraTarget != null)
        {
            targetForward = movement.CameraTarget.forward;
        }
        else
        {
            targetForward = Vector3.ProjectOnPlane(bodyPivot.forward, upAxis);

            if (movement.CameraTarget != null)
            {
                Vector3 cameraPlanarForward = Vector3.ProjectOnPlane(movement.CameraTarget.forward, upAxis);
                if (cameraPlanarForward.sqrMagnitude > 0.0001f)
                {
                    targetForward = cameraPlanarForward;
                }
            }
        }

        if (targetForward.sqrMagnitude < 0.0001f)
        {
            targetForward = Vector3.ProjectOnPlane(visualRoot.forward, upAxis);
        }

        if (targetForward.sqrMagnitude < 0.0001f)
        {
            targetForward = Vector3.forward;
        }

        targetForward.Normalize();

        Quaternion targetRotation;

        if (hasUsableGravity)
        {
            targetRotation = Quaternion.LookRotation(targetForward, upAxis);
        }
        else
        {
            Vector3 targetUp = movement.CameraTarget != null ? movement.CameraTarget.up : visualRoot.up;
            targetRotation = Quaternion.LookRotation(targetForward, targetUp);
        }

        if (hasUsableGravity && snapWhenFullyAligned && alignStrength < 0f)
        {
            visualRotation = targetRotation;
            ApplyVisualAroundPivot();
            return;
        }

        float sharpness = hasUsableGravity
            ? (alignStrength > 0f ? Mathf.Max(visualRotateSharpness, alignStrength) : visualRotateSharpness)
            : visualRotateSharpness;

        float t = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
        visualRotation = Quaternion.Slerp(visualRotation, targetRotation, t);
        visualRotation = Quaternion.Normalize(visualRotation);

        ApplyVisualAroundPivot();
    }

    void ApplyVisualAroundPivot()
    {
        // rotate the visual around the body pivot like a joint, preserving the original offset
        Vector3 worldOffset = visualRotation * initialPivotLocalOffset;

        visualRoot.position = bodyPivot.position + worldOffset;
        visualRoot.rotation = visualRotation;
    }
}