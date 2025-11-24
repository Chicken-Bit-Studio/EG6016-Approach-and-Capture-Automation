using UnityEngine;
using static SignalDynamics;

[RequireComponent(typeof(LineRenderer))]
public class ThrusterMonoBehaviour : MonoBehaviour, IPhysicsSteppable
{
    [Header("Thruster Properties")]
    [Tooltip("The maximum thrust the thruster can produce in newtons.")]
    [Range(1f, 1000)]
    public float thrusterPower = 500f;
    [Tooltip("The specific impulse of the thruster in seconds.")]
    public float thrusterISP = 300f;

    [Header("Value Tracking")]
    [Tooltip("The input signal to control the object's thrust. 0.0-1.0")]
    [ReadOnly] public float input = 0;
    [HideInInspector] public float input_manual = 0;
    [HideInInspector] public bool input_manual_isUpdated = false;
    [Tooltip("The output of the thruster at the present time. 0.0-1.0")]
    [ReadOnly] public float output;// { get; private set; } = 0;

    [Header("Plume Dynamics")]
    [Tooltip("The LineRenderer will visually represent the plume dynamics of this thruster.")]
    public LineRenderer lr;
    public float maxPlumeLength = 0.8f;

    [Header("Debugging Tools")]
    [Tooltip("If enabled, this thruster will report its state to the console in the FixedUpdate loop.")]
    public bool debuggingMode = false;

    // The RigidBody and main fuel tank of the satellite.
    private Rigidbody rb;
    private FuelTankMonoBehaviour ft;

    void Start()
    {
        // Find Rigidbody in root object and the main FuelTankMonoBehaviour
        rb = transform.root.GetComponent<Rigidbody>();
        ft = transform.root.GetComponentInChildren<FuelTankMonoBehaviour>();
    }
    void IPhysicsSteppable.PhysicsStep(float physicsDeltaTime)
    {
        // Allow for manual control to take over for this physics frame
        if (input_manual_isUpdated) { input = input_manual; }
        // Compute the thruster's output this physics frame
        output = RCS.Response(input, output, physicsDeltaTime);
        // Prepare the LineRenderer's vector destination ahead of plume calculation
        Vector3 lrVector = Vector3.zero;
        // If the output is anything other than zero, apply a force vector along the transform's z axis and consume fuel
        if (output != 0)
        {
            // Tsiolkovsky rocket equation <3
            // Isp = Thrust / (weight flow rate) = F / (ṁ * g₀)
            float thrustGenerated = thrusterPower * output; // N
            float fuelConsumptionRate = thrustGenerated / (thrusterISP * 9.80665f); // kg/s
            float fuelConsumed = fuelConsumptionRate * physicsDeltaTime; // kg
            if (ft.ConsumeFuel(fuelConsumed))
            {
                // Apply the thrust force at the thruster's position
                rb.AddForceAtPosition(thrustGenerated * -transform.forward, transform.position, ForceMode.Force);
                // Depict the thruster's activity as a plume
                lrVector = maxPlumeLength * output * Vector3.forward;
            }
        }
        // Update the LineRenderer to show the plume
        lr.SetPosition(1, lrVector);
        // Report this thruster's state to console if ordered to do so
        if (debuggingMode) { Debug.Log($"input: {input}, output: {output}"); }
        // Reset the manual control order for the next physics frame
        input_manual_isUpdated = false;
    }
}
