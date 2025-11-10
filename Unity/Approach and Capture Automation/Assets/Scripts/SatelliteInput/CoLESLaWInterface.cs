using UnityEngine;
using static RoboticsDataClasses;

public class CoLESLaWInterface : MonoBehaviour
{
    // Model interface class instance
    public IModel modelInterface;

    void Start()
    {
        // Create a new ModelIO instance to interface with this gameobject and its features.
        modelInterface = new IModel(this.gameObject);

        // Now do cool stuff
    }

    void Update()
    {
        
    }
}
