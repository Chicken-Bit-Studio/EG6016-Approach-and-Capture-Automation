using UnityEngine;
using static RoboticsDataClasses;

public class ManualActuatorControl : MonoBehaviour
{
    // Model interface class instance
    public IModel modelInterface;

    [Header("Actuation Settings")]
    public KeyCode extend = KeyCode.P;
    public KeyCode retrat = KeyCode.O;

    void Start()
    {
        // Collect a reference to the IModel instance for this gameobject
        CoLESLaWInterface coLESLaWInterface = GetComponent<CoLESLaWInterface>();
        if (coLESLaWInterface == null) { Debug.LogWarning("CoLESLaW Interface script not found!"); this.enabled = false; return; }
        modelInterface = coLESLaWInterface.modelInterface;
    }
    void Update()
    {
        if (Input.GetKey(extend))
        {
            foreach (Interfaces.IPlants.IActuator ac in modelInterface.plants.actuators.array)
            {
                ac.monoBehaviour.input_manual = 1;
                ac.monoBehaviour.input_manual_isUpdated = true;
            }
        }
        if (Input.GetKey(retrat))
        {
            foreach (Interfaces.IPlants.IActuator ac in modelInterface.plants.actuators.array)
            {
                ac.monoBehaviour.input_manual = -1;
                ac.monoBehaviour.input_manual_isUpdated = true;
            }
        }
    }
}
