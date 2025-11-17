using UnityEngine;

public class ManualThrusterControl : MonoBehaviour
{
    [Header("Power Settings")]
    [Tooltip("What is the maximum thrusters-worth of thrust that the manual controls can produce overall? Assumes identical thruster output across the model.")]
    public float maxManualPower = 2f;
    public float attitudeMultiplier = 0.5f;

    [Header("Key Mappings")]
    public ThrusterMonoBehaviour[] translateForwards;
    public ThrusterMonoBehaviour[] translateBackwards;
    public ThrusterMonoBehaviour[] translateUp;
    public ThrusterMonoBehaviour[] translateDown;
    public ThrusterMonoBehaviour[] translateLeft;
    public ThrusterMonoBehaviour[] translateRight;
    public ThrusterMonoBehaviour[] pitchUp;
    public ThrusterMonoBehaviour[] pitchDown;
    public ThrusterMonoBehaviour[] yawLeft;
    public ThrusterMonoBehaviour[] yawRight;
    public ThrusterMonoBehaviour[] rollCCW;
    public ThrusterMonoBehaviour[] rollCW;
    private (KeyCode, ThrusterMonoBehaviour[], float)[] keyMaps;

    public void Start()
    {
        // Assign keys to thruster groups and pre-calculate the output needed to achieve maxManualPower
        keyMaps = new (KeyCode, ThrusterMonoBehaviour[], float)[] {
            (KeyCode.H,  translateForwards,  maxManualPower/translateForwards.Length),
            (KeyCode.N,  translateBackwards, maxManualPower/translateBackwards.Length ),
            (KeyCode.I,  translateUp,        maxManualPower/translateUp.Length ),
            (KeyCode.K,  translateDown,      maxManualPower/translateDown.Length),
            (KeyCode.J,  translateLeft,      maxManualPower/translateLeft.Length ),
            (KeyCode.L,  translateRight,     maxManualPower/translateRight.Length),
            (KeyCode.Q,  rollCCW,            attitudeMultiplier*maxManualPower/rollCCW.Length),
            (KeyCode.E,  rollCW,             attitudeMultiplier*maxManualPower/rollCW.Length),
            (KeyCode.W,  pitchDown,          attitudeMultiplier*maxManualPower/pitchDown.Length),
            (KeyCode.S,  pitchUp,            attitudeMultiplier*maxManualPower/pitchUp.Length ),
            (KeyCode.A,  yawLeft,            attitudeMultiplier*maxManualPower/yawLeft.Length),
            (KeyCode.D,  yawRight,           attitudeMultiplier*maxManualPower/yawRight.Length )
        };
    }
    public void Update()
    {
        // Handle zero-fying and then non-zero signal application differently so each pass through keyMaps doesn't overwrite bits of the last
        // TODO: These actually don't integrate FixedUpdate at this time, which is a logic error.
        foreach ((KeyCode, ThrusterMonoBehaviour[], float) bind in keyMaps)
        {
            if (Input.GetKeyUp(bind.Item1)) { FireThrusters(bind.Item2, 0); }
        }
        foreach ((KeyCode, ThrusterMonoBehaviour[], float) bind in keyMaps)
        {
            if (Input.GetKey(bind.Item1)) { FireThrusters(bind.Item2, bind.Item3); }
        }
    }
    private void FireThrusters(ThrusterMonoBehaviour[] monos, float signal)
    {
        foreach (ThrusterMonoBehaviour thruster in monos)
        {
            thruster.input_manual = signal;
            thruster.input_manual_isUpdated = true;
        }
    }
}
