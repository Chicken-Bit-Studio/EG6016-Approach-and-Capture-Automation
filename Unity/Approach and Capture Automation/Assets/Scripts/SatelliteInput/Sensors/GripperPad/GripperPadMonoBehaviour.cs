using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GripperPadMonoBehaviour : MonoBehaviour
{
    //[Header("Target settings")]
    //[Tooltip("Assign the capture target transform (usually the debris/target object).")]
    //public Transform target;

    [Header("Normalization / tuning")]
    [Tooltip("Max contact force (N) used to normalize contact force magnitude.")]
    public float maxContactForce = 200f;

    [Tooltip("Probe radius (m) used to normalize contact depth. If pad center is within this distance from the target surface, depth approaches 1.")]
    public float contactProbeRadius = 0.05f;

    [Tooltip("Max relative speed (m/s) used to normalize relative velocity along normal.")]
    public float maxRelativeVelocity = 2f;

    [Tooltip("Max pad-to-target distance (m) used to normalize distance when there is no contact.")]
    public float maxPadTargetDistance = 2f;

    // Internal runtime values updated from collision callbacks
    private bool isContacting = false;
    private Vector3 avgContactNormalWorld = Vector3.zero;
    private Vector3 avgContactPointWorld = Vector3.zero;
    private float summedImpulseMagnitude = 0f; // sum of magnitudes of contact impulses observed this fixed update
    private float lastContactForce = 0f; // N (approx)
    private float lastContactDepth = 0f; // 0..1
    private float lastRelVelAlongNormal = 0f; // -inf..inf (will be normalized later)
    private float lastPadToTargetDistance = 0f; // meters (will be normalized later)

    // Keep track of contact points in the current step
    private List<ContactPoint> contactPoints = new List<ContactPoint>();

    // Cached references
    private Rigidbody padRb;
    private EnvironmentController environmentController;
    //private Rigidbody targetRb;
    //private Collider targetCollider;

    void Start()
    {
        padRb = GetComponent<Rigidbody>();
        environmentController = FindAnyObjectByType<EnvironmentController>();
        if (environmentController == null) { Debug.LogError("EnvironmentController not dound in scene!"); }
    }

    void OnDisable()
    {
        ResetContactState();
    }

    private void ResetContactState()
    {
        isContacting = false;
        avgContactNormalWorld = Vector3.zero;
        avgContactPointWorld = Vector3.zero;
        summedImpulseMagnitude = 0f;
        lastContactForce = 0f;
        lastContactDepth = 0f;
        lastRelVelAlongNormal = 0f;
        contactPoints.Clear();
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        HandleCollision(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.collider == environmentController.references.targetCollider)
        {
            ResetContactState();
        }
    }

    private void HandleCollision(Collision collision)
    {
        if (collision == null) { return; }

        // Accept collisions only with the configured target transform (or any child of it)
        if (!IsCollisionWithTarget(collision)) { return; }

        isContacting = true;
        contactPoints.Clear();

        Vector3 normalSum = Vector3.zero;
        Vector3 pointSum = Vector3.zero;
        int count = 0;
        foreach (ContactPoint cp in collision.contacts)
        {
            normalSum += cp.normal;
            pointSum += cp.point;
            contactPoints.Add(cp);
            count++;
        }
        if (count == 0) { return; }

        avgContactNormalWorld = (normalSum / count).normalized;
        avgContactPointWorld = pointSum / count;

        // Collision.impulse is the total impulse applied to this contact pair during the last physics step (approx).
        // Use its magnitude as an approximation of contact impulse; convert to force by dividing by deltaTime.
        // Sum magnitudes if multiple contacts reported.
        Vector3 impulse = collision.impulse;
        summedImpulseMagnitude = impulse.magnitude;
        if (Time.fixedDeltaTime > 0f) { lastContactForce = summedImpulseMagnitude / Time.fixedDeltaTime; }  // approximate average force (N)
        else { lastContactForce = summedImpulseMagnitude; }

        // Pad to target distance (closest point)
        Vector3 closest = environmentController.references.targetCollider.ClosestPoint(transform.position); // point on target surface closest to pad pivot
        float padToSurface = Vector3.Distance(transform.position, closest);
        lastPadToTargetDistance = padToSurface;
        // Estimate depth: if pad is overlapping target collider ClosestPoint returns a point on surface and distance may be small;
        // Use contactProbeRadius to convert distance -> depth: depth = clamp01((contactProbeRadius - d)/contactProbeRadius)
        float penetrationEstimate = Mathf.Max(0f, contactProbeRadius - padToSurface);
        lastContactDepth = Mathf.Clamp01(penetrationEstimate / contactProbeRadius);

        // Relative velocity along normal at average contact point
        Vector3 padVel = Vector3.zero;
        Vector3 tgtVel = Vector3.zero;
        padVel = padRb.GetPointVelocity(avgContactPointWorld);
        tgtVel = environmentController.references.targetRigidbody.GetPointVelocity(avgContactPointWorld);
        Vector3 relVel = padVel - tgtVel;
        lastRelVelAlongNormal = Vector3.Dot(relVel, avgContactNormalWorld);
    }

    private bool IsCollisionWithTarget(Collision collision)
    {
        Transform t = collision.transform;
        if (t == environmentController.references.targetGameObject.transform) { return true; }
        // Check parent chain
        while (t != null)
        {
            if (t == environmentController.references.targetGameObject.transform) { return true; }
            t = t.parent;
        }
        return false;
    }

    public (bool, float[]) GetMLObservation()
    {
        // Return structure:
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Index:   Quantity:                           Range:                  Note:
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // [0]      Contact flag                        x = 0.0 | x = 1.0       Boolean for "is the pad touching the target?"
        // [1]      Contact normal x                    -1.0 <= x <= 1.0        } Direction of pad’s contact force, expressed in pad local frame
        // [2]      Contact normal y                    -1.0 <= x <= 1.0        }
        // [3]      Contact normal z                    -1.0 <= x <= 1.0        }
        // [4]      Contact force magnitude normalised  0.0  <= x <= 1.0        Gives grip strength cue
        // [5]      Contact depth                       0.0  <= x <= 1.0        How deep the contact is (normalized)
        // [6]      Relative velocity along normal      -1.0 <= x <= 1.0        How fast the pad is sliding/compressing (helps with stick vs slip)
        // [7]      Pad position relative to target     0.0  <= x <= 1.0        Distance of pad center to target surface (if no contact yet)
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        // Validate configuration
        if (environmentController.references.targetGameObject == null)
        {
            // Not configured - caller can decide how to handle this
            return (false, null);
        }

        float[] obs = new float[8];

        // [0] Contact flag
        obs[0] = isContacting ? 1f : 0f;

        // [1..3] Contact normal in pad local space
        Vector3 normalLocal = Vector3.zero;
        if (isContacting)
        {
            normalLocal = transform.InverseTransformDirection(avgContactNormalWorld).normalized;
        }
        // clamp values to -1..1 to be safe
        obs[1] = Mathf.Clamp(normalLocal.x, -1f, 1f);
        obs[2] = Mathf.Clamp(normalLocal.y, -1f, 1f);
        obs[3] = Mathf.Clamp(normalLocal.z, -1f, 1f);

        // [4] Contact force magnitude normalized 0..1
        float normForce = 0f;
        if (isContacting)
        {
            normForce = lastContactForce / Mathf.Max(1e-6f, maxContactForce);
            normForce = Mathf.Clamp01(normForce);
        }
        obs[4] = normForce;

        // [5] Contact depth normalized 0..1
        obs[5] = Mathf.Clamp01(lastContactDepth);

        // [6] Relative velocity along normal normalized -1..1
        float normRelVel = 0f;
        if (isContacting)
        {
            normRelVel = lastRelVelAlongNormal / Mathf.Max(1e-6f, maxRelativeVelocity);
            normRelVel = Mathf.Clamp(normRelVel, -1f, 1f);
        }
        obs[6] = normRelVel;

        // [7] Pad position relative to target (normalized distance)
        // Use lastPadToTargetDistance normalized by maxPadTargetDistance (configured).
        float normPadDist = lastPadToTargetDistance / Mathf.Max(1e-6f, maxPadTargetDistance);
        normPadDist = Mathf.Clamp01(normPadDist);
        obs[7] = normPadDist;

        return (true, obs);
    }
}
