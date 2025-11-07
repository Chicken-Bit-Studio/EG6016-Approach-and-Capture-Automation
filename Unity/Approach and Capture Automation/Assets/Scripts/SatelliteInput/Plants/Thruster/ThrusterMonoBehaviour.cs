using UnityEngine;
using static SignalDynamics;

[RequireComponent(typeof(LineRenderer))]
public class ThrusterMonoBehaviour : MonoBehaviour
{
    [Header("Thruster Properties")]
    [Tooltip("The maximum thrust the thruster can produce in newtons.")]
    [Range(1f, 200)]
    public float thrusterPower = 100f;

    [Header("Value Tracking")]
    [Tooltip("The input signal to control the object's thrust. 0.0-1.0")]
    [Range(0f, 1f)]
    public float input = 0;
    [Tooltip("The output of the thruster at the present time. 0.0-1.0")]
    [ReadOnly] public float output;// { get; private set; } = 0;

    [Header("Debugging Tools")]
    [Tooltip("If enabled, this thruster will report its state to the console in the FixedUpdate loop.")]
    public bool debuggingMode = false;

    // The RigidBody of the satellite.
    private Rigidbody rb;
    // The visual representation of the thruster's output via a simple LineRenderer.
    private LineRenderer lr;
    private float maxPlumeLength = 0.8f;

    void Start()
    {
        // Find the plume and rigidbody objects
        lr = GetComponent<LineRenderer>();
        rb = transform.root.GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        // Compute the thruster's output this physics frame
        output = SignalRCS.Response(input, output, Time.fixedDeltaTime);
        // If the output is anything other than zero, apply a force vector along the transform's z axis
        if (output != 0) { rb.AddForceAtPosition(output * thrusterPower * -transform.forward, transform.position, ForceMode.Force); }
        // Depict the thruster's activity as a plume
        lr.SetPosition(1, maxPlumeLength * output * Vector3.forward);
        // Report this thruster's state to console if ordered to do so
        if (debuggingMode) { Debug.Log($"input: {input}, output: {output}"); }
    }
}
