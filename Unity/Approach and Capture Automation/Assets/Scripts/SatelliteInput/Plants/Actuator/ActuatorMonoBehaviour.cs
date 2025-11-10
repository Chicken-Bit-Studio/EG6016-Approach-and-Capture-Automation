using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static SignalDynamics;

public class ActuatorMonoBehaviour : MonoBehaviour
{
    // Description: This script controls the articulation of a given model segment at a node.
    // Use:         Place this script on the Node gameobject where articulation is wanted. A goal of this script is to be fully automatic,
    //                  letting the user place this script on any Node in a model either manually or automatically (eg. via prefab regeneration as in
    //                  SolidworksMacroResultProcessor) and the functionality should be the same.
    // TODO:        Have this script take angular limits, maximum torque, etc. from the associated ModelElements.Actuator object.

    [Header("Data Class Container")]
    [ReadOnly] public RoboticsDataClasses.ModelElements.Actuator actuator = null;

    [Header("Actuator Properties")]
    [Tooltip("The maximum torque the actuator can produce in newton-meters.")]
    [Range(1f, 20)]
    public float actuatorTorque = 10f;  // Eventually this will be dictated by the ModelElements.Actuator value
    [Tooltip("The maximum speed at which the actuator can mode in degrees-per-second.")]
    public float actuatorMaxSpeed = 30f;// Eventually this will be dictated by the ModelElements.Actuator value
    public float angleMin = 0f;
    public float angleMax = 90f;

    [Header("Value Tracking")]
    [Tooltip("The input signal to control the motor's torque. -1.0-1.0")]
    [ReadOnly] public float input = 0;
    [HideInInspector] public float input_manual = 0;
    [HideInInspector] public bool input_manual_isUpdated = false;
    [Tooltip("The output of the motor at the present time. -1.0-1.0")]
    [ReadOnly] public float output;

    [Header("Component References")]
    public Parent parent = new();
    public Child child = new();

    [Header("Interface Settings")]
    [Tooltip("Should collisions between the child and parent gameobjects be ignored?")]
    public bool ignoreParentChildCollisions = false;

    [Header("Debugging Tools")]
    [Tooltip("If enabled, this actuator will report its state to the console in the FixedUpdate loop.")]
    public bool debuggingMode = false;

    [System.Serializable]
    public class Parent
    {
        [ReadOnly] public Transform transform;
        [ReadOnly] public Collider collider;
        [ReadOnly] public Rigidbody rigidbody;
        public Rigidbody rigidbodyOverride;
    }
    [System.Serializable]
    public class Child
    {
        [ReadOnly] public Transform transform;
        [ReadOnly] public Collider collider;
        [ReadOnly] public Rigidbody rigidbody;
        [ReadOnly] public HingeJoint hingeJoint;
    }

    void Start()
    {
        // Validate and collect Tranform references - catch cases where this segment might be a root or leaf transform.
        if (transform.childCount == 0 || transform.parent == null)
        {
            this.enabled = false;
            return;
        }
        parent.transform = transform.parent;
        child.transform = transform.GetChild(0);

        // Idbiagd chabfbasuhd da,sndo ,jbdg jdb adliauhajbib kajbb khdgo abx iauhdb!
        parent.collider = parent.transform.GetComponent<Collider>();
        parent.rigidbody = (parent.rigidbodyOverride != null) ? parent.rigidbodyOverride : parent.transform.GetComponent<Rigidbody>();
        child.collider = child.transform.GetComponent<Collider>();
        child.rigidbody = child.transform.GetComponent<Rigidbody>();
        if (child.rigidbody == null || parent.rigidbody == null)
        {
            Debug.LogWarning($"No Rigidbidy component found on either parent or child of Node on \"{parent.transform.name}\". Check components or consider using parent.rigidbodyOverride.");
            this.enabled = false;
            return;
        }

        // If needed, order the physics engine to ignore collisions between the parent and child gameobjects.
        // This helps in cases where parts are deliberately clipped into eachother instead of having tight interface geometry between segments.
        if (ignoreParentChildCollisions && parent.collider != null && child.collider != null) { Physics.IgnoreCollision(parent.collider, child.collider); }

        // Checking and preamble is complete, setup the HingeJoint component
        child.hingeJoint = child.transform.AddComponent<HingeJoint>();
        child.hingeJoint.anchor = Vector3.zero;
        child.hingeJoint.axis = Vector3.forward;
        child.hingeJoint.connectedBody = parent.rigidbody;
        child.hingeJoint.connectedAnchor = transform.localPosition; // This Node's position in its parent's local space
        child.hingeJoint.useMotor = true;
        child.hingeJoint.useLimits = true;
        child.hingeJoint.limits = new()
        {
            min = angleMin,
            max = angleMax
        };
    }
    void FixedUpdate()
    {
        // Allow for manual control to take over for this physics frame
        if (input_manual_isUpdated) { input = input_manual; }
        else { input = 0; }
        // Compute the hinge motor's output this physics frame
        output = ClawActuator.Response(input, output, Time.fixedDeltaTime);
        // Always be updating the HingeJoint's motor instructions. TODO: Can be checked for changes if time permits.
        // JointMotor is a little strange. It's a struct (not a component), and needs to be assigned anew after each and every edit.
        child.hingeJoint.motor = new()
        {
            freeSpin = false,
            force = actuatorTorque,
            targetVelocity = output * actuatorMaxSpeed
        };
        // Report this actuator's state to console if ordered to do so
        if (debuggingMode) { Debug.Log($"input: {input}, output: {output}, angle: {child.hingeJoint.angle}deg"); }
        // Reset the manual control order for the next physics frame
        input_manual_isUpdated = false;
    }
}
