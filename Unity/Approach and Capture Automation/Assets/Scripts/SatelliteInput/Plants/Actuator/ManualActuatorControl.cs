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
        // TODO: this is now a flimsy reference. The modelInterface reference is easy to take. Try to ensure only one is made per scene, through.
        // Collect a reference to the IModel instance for this gameobjec
        modelInterface = FindAnyObjectByType<EnvironmentController>().references.satelliteModelInterface;
    }
    void Update()
    {
        if (Input.GetKeyUp(extend) || Input.GetKeyUp(retrat))
        {
            foreach (Interfaces.IPlants.IActuator ac in modelInterface.plants.actuators.array)
            {
                ac.monoBehaviour.input_manual = 0;
                ac.monoBehaviour.input_manual_isUpdated = true;
            }
        }
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
