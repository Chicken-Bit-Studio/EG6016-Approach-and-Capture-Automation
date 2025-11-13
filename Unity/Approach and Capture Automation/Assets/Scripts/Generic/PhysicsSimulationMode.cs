using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsSimulationMode : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Physics.simulationMode = SimulationMode.FixedUpdate;
        Debug.Log("Changing to " + SimulationMode.FixedUpdate);
    }
}
