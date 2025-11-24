using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;

public static class RoboticsDataClasses
{
    /// <summary>
    /// Top-level static class containing the building blocks of a Model. An attempt has been made to keep these core C# friendly.
    /// </summary>
    public static class ModelElements
    {
        [System.Serializable]
        public class ModelProfile
        {
            public string modelName;
            public string description;
            public SegmentProfile rootSegment;
            public ModelProfile(string name, string description, SegmentProfile rootSegment)
            {
                this.modelName = name;
                this.description = description;
                this.rootSegment = rootSegment;
            }
        }

        [System.Serializable]
        public class SegmentProfile
        {
            public string segmentName;    //For locating the prefab in Assets folder.
            public AttachmentNode[] nodes;
            public SegmentProfile(string segmentName, AttachmentNode[] nodes)
            {
                this.segmentName = segmentName;
                this.nodes = nodes;
            }
        }

        [System.Serializable]
        public class AttachmentNode
        {
            public float[] transformationMatrix; //4x4 matrix (flattened) for the transform of the node in local space.                 
                                                 // stop Unity serializing these references (prevents Unity recursion)
            [System.NonSerialized] public Actuator actuator = null;
            [System.NonSerialized] public SegmentProfile child = null;
            public bool AddChild(SegmentProfile segment)
            {
                if (this.child == null) { this.child = segment; return true; }
                else { return false; }
            }
            public void RemoveChild()
            {
                this.child = null;
            }
            public AttachmentNode(float[] transformationMatrix, Actuator actuator = null, SegmentProfile child = null)
            {
                this.transformationMatrix = transformationMatrix;
                this.actuator = actuator;
                this.child = child;
            }
        }

        [System.Serializable]
        public class Actuator
        {
            public float rangeMin;  //degrees
            public float rangeMax;  //degrees
            public float torque;    //gcm^-1
            public float power;     //W
        }

        [System.Serializable]
        public class Thruster
        {
            public float force;
            //public float tau;
        }

        [System.Serializable]
        public class LiDAR
        {

        }

        [System.Serializable]
        public class GripperPad
        {

        }

        [System.Serializable]
        public class FuelTank
        {

        }
    }

    /// <summary>
    /// A class containing input and output interfaces for the given model instance. Tailored to Unity.
    /// </summary>
    [System.Serializable]
    public class IModel
    {
        public GameObject modelRoot;
        public ModelSensors sensors;
        public ModelPlants plants;

        // Note: perhaps simply holding an array for each thing in Sensors or Plants is simpler.
        // Unity Inspector shows array count anyway, so...? TODO: this

        public IModel(GameObject modelInScene)
        {
            // Assign the model's gameobject
            modelRoot = modelInScene;
            // Create the IO classes
            sensors = new();
            plants = new();
            // Poll ContainerClass instances in the scene and populate the IO classes
            RegisterIODevices(modelRoot);
        }
        [System.Serializable]
        public class ModelSensors
        {
            [Header("Model Input Device Resister")]
            [ReadOnly] public int sensorCount = 0;
            public LiDARs lidars = new();
            public GripperPads gripperPads = new();
            public FuelTanks fuelTanks = new();
            [System.Serializable]
            public class LiDARs
            {
                [ReadOnly] public int lidarCount = 0;
                [HideInInspector] public Interfaces.ISensors.ILiDAR[] array;
            }
            [System.Serializable]
            public class GripperPads
            {
                [ReadOnly] public int gripperPadCount = 0;
                [HideInInspector] public Interfaces.ISensors.IGripperPad[] array;
            }
            [System.Serializable]
            public class FuelTanks
            {
                [ReadOnly] public int fuelTankCount = 0;
                [HideInInspector] public Interfaces.ISensors.IFuelTank[] array;
            }
        }
        [System.Serializable]
        public class ModelPlants
        {
            [Header("Model Output Device Resister")]
            [ReadOnly] public int plantCount = 0;
            public Actuators actuators = new();
            [System.Serializable]
            public class Actuators
            {
                [ReadOnly] public int actuatorCount = 0;
                [HideInInspector] public Interfaces.IPlants.IActuator[] array;
            }
            public Thrusters thrusters = new();
            [System.Serializable]
            public class Thrusters
            {
                [ReadOnly] public int thrusterCount = 0;
                [HideInInspector] public Interfaces.IPlants.IThruster[] array;
            }
        }
        private void RegisterIODevices(GameObject model)
        {
            // Collect a list of all IModel sensors and plants leaf types
            Type[] sensorsAndPlantClasses = StaticUtilities.GetClassTreeLeafTypes(new Type[] { typeof(ModelSensors), typeof(ModelPlants) });
            // Iterate through each IO class, poll the gameobject for relevant monobehaviours and populate relevant fields
            foreach (Type ioClass in sensorsAndPlantClasses)
            {
                // IO classes have members:
                //  integer: The count of objects/interfaces of this type in the model
                //  interface class array: array of (populated) corresponding interface classes

                // First, collect a reference to this IModel's own instance of this ioClass so we can reassign to it later in this loop
                object ioClassInstance = null;
                // Look through this IModel's fields (sensors and plants)
                foreach (FieldInfo iModelField in this.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object parent = iModelField.GetValue(this);
                    if (parent == null) continue;

                    // Search inside the nested fields of that parent (like sensors.lidars or plants.thrusters)
                    FieldInfo nested = iModelField.FieldType
                        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(f => f.FieldType == ioClass);

                    if (nested != null)
                    {
                        ioClassInstance = nested.GetValue(parent);
                        break;
                    }
                }
                if (ioClassInstance == null)
                {
                    Debug.LogError($"Couldn't locate an instance of {ioClass.Name} inside IModel.");
                    continue;
                }

                // Collect all fields of this IModel IO class - public and private - except ones marked 'static'
                FieldInfo[] ioClassFields = ioClass.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                // Collect the integer member. Note: This doesn't catch other integer types like long or uint
                FieldInfo ioClassIntegerMember = ioClassFields.FirstOrDefault(f => f.FieldType == typeof(int));
                // Collect the array member
                FieldInfo ioClassArrayMember = ioClassFields.FirstOrDefault(f => f.FieldType.IsArray);
                // Check that the two important class members have been identified without error
                if (ioClassIntegerMember == null || ioClassArrayMember == null)
                {
                    Debug.LogError($"Missing expected fields on {ioClass.Name}: count or array field not found.");
                    continue;
                }
                // Collect Interfaces class that corresponds to this IO class
                Type interfaceType = ioClassArrayMember.FieldType.GetElementType();
                if (interfaceType == null)
                {
                    Debug.LogError($"Array field on {ioClass.Name} has no element type.");
                    continue;
                }
                // Get a constructor for the Interfaces class (must be public)
                ConstructorInfo interfaceConstructor = interfaceType
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault();
                if (interfaceConstructor == null)
                {
                    Debug.LogError($"No constructor found for {interfaceType.Name}.");
                    continue;
                }
                // Find the MonoBehaviour-typed field inside the interface type
                Type monoBehaviour = interfaceType
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(f => typeof(MonoBehaviour).IsAssignableFrom(f.FieldType)).FieldType;
                if (monoBehaviour == null)
                {
                    Debug.LogError($"No MonoBehaviour-derived field found inside {interfaceType.Name}.");
                    continue;
                }

                // This concludes the 'collection phase' of this method. Congratulations.

                // Create a temporary holding list for the Interface class instances
                List<object> interfaceList = new();
                // Poll the gameobject hierarchy 
                foreach (Component found in model.GetComponentsInChildren(monoBehaviour))
                {
                    // Instantite an interface for this component and add it to the proper array
                    interfaceList.Add(interfaceConstructor.Invoke(new object[] { found, null }));
                }

                // Create an array of these interface instances the long way (we can't use object-based list for end-value reassignment)
                var arr = Array.CreateInstance(interfaceType, interfaceList.Count);
                for (int i = 0; i < interfaceList.Count; i++) { arr.SetValue(interfaceList[i], i); }
                // Assign the end values to this instance of IModel. It's been a pleasure, goodbye.
                ioClassIntegerMember.SetValue(ioClassInstance, arr.Length);
                ioClassArrayMember.SetValue(ioClassInstance, arr);
            }
            // Begin counting up the total number of sensors and plant interfaces in this IModel object after all that ^
            int totalSensors = 0;
            int totalPlants = 0;
            // Count all populated arrays inside ModelSensors
            if (sensors != null)
            {
                foreach (FieldInfo f in typeof(ModelSensors)
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object fieldVal = f.GetValue(sensors);
                    if (fieldVal == null) continue;

                    // Look inside nested classes like LiDARs
                    foreach (FieldInfo nested in f.FieldType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (nested.FieldType.IsArray)
                        {
                            Array arr = nested.GetValue(fieldVal) as Array;
                            if (arr != null) totalSensors += arr.Length;
                        }
                    }
                }
                sensors.sensorCount = totalSensors;
            }
            // Count all populated arrays inside ModelPlants
            if (plants != null)
            {
                foreach (FieldInfo f in typeof(ModelPlants)
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object fieldVal = f.GetValue(plants);
                    if (fieldVal == null) continue;

                    foreach (FieldInfo nested in f.FieldType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (nested.FieldType.IsArray)
                        {
                            Array arr = nested.GetValue(fieldVal) as Array;
                            if (arr != null) totalPlants += arr.Length;
                        }
                    }
                }
                plants.plantCount = totalPlants;
            }
            // Report results in the Unity Console
            /*Debug.Log($"IO registration complete for {model.name}:\n" +
                      $"  Sensors detected: {totalSensors}\n" +
                      $"  Plants detected:  {totalPlants}");*/
        }
    }

    /// <summary>
    /// Interfaces are classes used to interact indirectly with the sensors and plants of a given model. Interface classes are denoted with the 'I' prefix.
    /// </summary>
    public static class Interfaces
    {
        /// <summary>
        /// Sensors are the model's eyes and ears. They include anything dedicated to data collection. Pressure pads, liDARs, cameras, and microphones are all considered 'sensors' in this regime.
        /// </summary>
        public static class ISensors
        {
            public class ILiDAR
            {
                public LiDARMonoBehaviour monoBehaviour;
                public ModelElements.LiDAR modelProfileObject;

                public ILiDAR(LiDARMonoBehaviour monoBehaviour, ModelElements.LiDAR modelProfileObject = null)
                {
                    this.monoBehaviour = monoBehaviour;
                    this.modelProfileObject = modelProfileObject;
                }
            }
            public class IGripperPad
            {
                public GripperPadMonoBehaviour monoBehaviour;
                public ModelElements.GripperPad modelProfileObject;

                public IGripperPad(GripperPadMonoBehaviour monoBehaviour, ModelElements.GripperPad modelProfileObject = null)
                {
                    this.monoBehaviour = monoBehaviour;
                    this.modelProfileObject = modelProfileObject;
                }
            }
            public class IFuelTank
            {
                public FuelTankMonoBehaviour monoBehaviour;
                public ModelElements.FuelTank modelProfileObject;

                public IFuelTank(FuelTankMonoBehaviour monoBehaviour, ModelElements.FuelTank modelProfileObject = null)
                {
                    this.monoBehaviour = monoBehaviour;
                    this.modelProfileObject = modelProfileObject;
                }
            }
        }
        /// <summary>
        /// Plants are the model's output devices. They include any outlet that changes the model's state. Thrusters, lights, speakers, angular and linear actuators are all considered 'plants' in this regime.
        /// </summary>
        public static class IPlants
        {
            public class IActuator
            {
                public ActuatorMonoBehaviour monoBehaviour;
                public ModelElements.Actuator modelProfileObject;

                public IActuator(ActuatorMonoBehaviour actuatorMonoBehaviour, ModelElements.Actuator actuatorModelProfileObject = null)
                {
                    monoBehaviour = actuatorMonoBehaviour;
                    modelProfileObject = actuatorModelProfileObject;
                }
            }
            public class IThruster
            {
                public ThrusterMonoBehaviour monoBehaviour;
                public ModelElements.Thruster modelProfileObject;

                public IThruster(ThrusterMonoBehaviour thrusterMonoBehaviour, ModelElements.Thruster thrusterModelProfileObject = null)
                {
                    monoBehaviour = thrusterMonoBehaviour;
                    modelProfileObject = thrusterModelProfileObject;
                }
            }
        }
    }

    /// <summary>
    /// Machines that learn?! Who would've thought it?
    /// </summary>
    public static class ReinforcementLearning
    {
        // With no two applications of reinforcement learning being the same, each deployment must have its own tailored Observations and Actions struct to suit the model being trained.
        public class ApproachAndCaptureProject
        {
            // Observations, Actions, and Rewards are short-lived structs and should only be used in the same instance they are created in, for sake of the lidatHitDistances reference.
            public struct Observations
            {
                public float3 relativePosition;             // The relative vector from the satellite's origin to that of the target (satellite -> target)
                public float3 relativeVelocity;             // The relative velocity of the satellite's origin to that of the target (satellite -> target)
                public float3 targetDirection;              // A unit vector (in the satellite's local reference frame) pointing from the satellite's origin toward the target's origin (satellite -> target)
                public float3 angularVelocity_satellite;    // The current angular velocity of the satellite in its own local space (satellite only)
                public float3 angularVelocity_target;       // The current angular velocity of the target in its own local space (target only)
                public float fuelTankData;                  // The current fuel level of the satellite's fuel tank(s) in kilograms (satellite only)
                public float[] actuatorData;                // A compiled array of returns the .GetMLObservation() method from each instance of ActuatorMonoBehavior in the model.
                public float[] gripperPadData;              // A compiled array of returns the .GetMLObservation() method from each instance of GripperPadMonoBehavior in the model.
                public float[] lidarData;                   // An array holding the APPROPRIATELY-SIZED LiDAR ray hit distances (satellite only after data collection)

                public const int MAX_LIDAR_SAMPLES = 256;   // Edit this to dynamically change the size of generated and fed LiDAR sample arrays

                public Observations(IModel modelInterface, Transform satellite, Rigidbody satelliteRb, Transform target, Rigidbody targetRb)
                {
                    // TODO: Rework to reference NativeArray in LiDARMonoBehaviour - not call .ToArray().
                    // Generic scene data
                    relativePosition = satellite.InverseTransformPoint(target.position);
                    relativeVelocity = (float3)satellite.InverseTransformDirection(satelliteRb.velocity - targetRb.velocity);
                    targetDirection = satellite.InverseTransformDirection((target.position - satellite.position).normalized);
                    angularVelocity_satellite = (float3)satellite.InverseTransformDirection(satelliteRb.angularVelocity);
                    angularVelocity_target = (float3)target.InverseTransformDirection(targetRb.angularVelocity);
                    // TODO: Note: This next part demonstrates the current need to shake up how ModelElements, arrays in IModel instances Interfaces need to be automatised. Adding GripperPads was confusing,
                    //             and I expect there is a way to do this dynamically with either file seaching + naming conventions or a single loopup dictionary-like object.
                    // TODO: Automate obs collection here. Repeating these lines for each new sensor/plant type is tedious and confusing.
                    
                    // Dynamic data initialization
                    (bool, float[]) output;
                    
                    // Dynamic satellite Fuel Tank data
                    output = modelInterface.sensors.fuelTanks.array[0].monoBehaviour.GetMLObservation();
                    if (!output.Item1) { fuelTankData = output.Item2[0]; }
                    else { Debug.LogError("FuelTank observation data invalid - check FuelTankMonoBehaviour.GetMLObservation()."); fuelTankData = 1f; }
                    
                    // Dynamic satellite Actuator data
                    List<float> tActuatorData = new();
                    foreach (Interfaces.IPlants.IActuator iface in modelInterface.plants.actuators.array)
                    {
                        output = iface.monoBehaviour.GetMLObservation();
                        if (!output.Item1) { tActuatorData.AddRange(output.Item2); }
                        else { Debug.LogError("Actuator observation data invalid - check ActuatorMonoBehaviour.GetMLObservation()."); }
                    }
                    actuatorData = tActuatorData.ToArray();
                    
                    // Dynamic satellite Gripper Pad data
                    List<float> tGripperPadData = new();
                    foreach (Interfaces.ISensors.IGripperPad iface in modelInterface.sensors.gripperPads.array)
                    {
                        output = iface.monoBehaviour.GetMLObservation();
                        if (!output.Item1) { tGripperPadData.AddRange(output.Item2); }
                        else { Debug.LogError("GripperPad observation data invalid - check GripperPadMonoBehaviour.GetMLObservation()."); }
                    }
                    gripperPadData = tGripperPadData.ToArray();
                    
                    // Dynamic satellite LiDAR data
                    output = modelInterface.sensors.lidars.array[0].monoBehaviour.GetMLObservation();
                    if (!output.Item1) { lidarData = output.Item2; }
                    else { Debug.LogError("LiDAR observation data invalid - check LiDARMonoBehaviour.GetMLObservation()."); lidarData = new float[MAX_LIDAR_SAMPLES]; }
                }
                public readonly float[] SendToFloatArray()
                {
                    // Create a new empty holder list
                    List<float> list = new();
                    // Generic scene data
                    AddFloat3ValuesToList(relativePosition);
                    AddFloat3ValuesToList(relativeVelocity);
                    AddFloat3ValuesToList(targetDirection);
                    AddFloat3ValuesToList(angularVelocity_satellite);
                    AddFloat3ValuesToList(angularVelocity_target);
                    // Dynamic data
                    list.Add(fuelTankData);
                    AddArrayValuesToList(actuatorData);
                    AddArrayValuesToList(gripperPadData);
                    AddArrayValuesToList(lidarData);

                    return list.ToArray();

                    void AddFloat3ValuesToList(float3 f3)
                    {
                        list.Add(f3[0]);
                        list.Add(f3[1]);
                        list.Add(f3[2]);
                    }
                    void AddArrayValuesToList(float[] subArr)
                    {
                        foreach (float f in subArr) list.Add(f);
                    }
                }
            }
            public struct Actions
            {

                IModel modelInterface;
                float[] receivedFloats;

                // For now, the model prefab is "locked-in" - it won't be changed. We can use a constant to validate action size now, but this isn't automatically scalable.
                // floats 0.0 - 1.0 for each:
                //   actuators: 16
                //   thrusters: 24
                //   Total:     40
                public const int FEED_SIZE = 40;

                // Expected actions float array layout:
                // [0]-[15]:    ActuatorMonoBehaviour.input     (one each)
                // [16]-[39]:   ThrusterMonoBehaviour.input     (one each)

                public Actions(IModel modelInterface, float[] receivedFloats)
                {
                    this.modelInterface = modelInterface;   // The corresponding ModelInterface onto which to apply the received float 'actions'
                    this.receivedFloats = receivedFloats;   // The array of floats generated by the neural network
                }
                public void AffectModel(bool debuggingMode = false)
                {
                    // Tracking integers for debugging mode
                    int a = 0, t = 0;
                    // Cry and halt if the input array and expected array size does not match
                    if (modelInterface.plants.plantCount != receivedFloats.Length)
                    {
                        Debug.LogError("Actions.AffectModel didn't execute because of a model-plant/input-array size mismatch.\n" +
                            $"Plants in model: {modelInterface.plants.plantCount}, Received array length: {receivedFloats.Length}");
                        return;
                    }
                    // Apply the inputs
                    int currentIndex = 0;
                    foreach (Interfaces.IPlants.IActuator iActuator in modelInterface.plants.actuators.array)
                    {
                        iActuator.monoBehaviour.input = Mathf.Clamp(receivedFloats[currentIndex], 0, 1);
                        currentIndex++;
                        a++;
                    }
                    foreach (Interfaces.IPlants.IThruster iThruster in modelInterface.plants.thrusters.array)
                    {
                        iThruster.monoBehaviour.input = Mathf.Clamp(receivedFloats[currentIndex], 0, 1);
                        currentIndex++;
                        t++;
                    }
                    // Final check
                    // (not FEED_SIZE - 1, as it gets incremented after the last assignment)
                    if (currentIndex != FEED_SIZE) { Debug.LogWarning($"Assigned an unexpected number of actions. Expected final index: {FEED_SIZE - 1}, Actual final index: {currentIndex - 1}"); }

                    // Review and report the applied actions if told to
                    if (debuggingMode)
                    {
                        string arrStr = "";
                        int index = 0;
                        arrStr += $"Actuators ({modelInterface.plants.actuators.actuatorCount}):\t";
                        for (int i = 0; i < modelInterface.plants.actuators.actuatorCount; i++) { arrStr += receivedFloats[index] + ", "; index++; }
                        arrStr += $"\nThrusters ({modelInterface.plants.thrusters.thrusterCount}):\t";
                        for (int i = 0; i < modelInterface.plants.thrusters.thrusterCount; i++) { arrStr += receivedFloats[index] + ", "; index++; }
                        arrStr += $"\nTotal ({index})" +
                            ((index == modelInterface.plants.actuators.actuatorCount + modelInterface.plants.thrusters.thrusterCount) ? "" :
                            $" [Debugging logic error: {index} != {modelInterface.plants.actuators.actuatorCount} + {modelInterface.plants.thrusters.thrusterCount}]");
                        Debug.Log(arrStr);
                    }
                }
            }
            public struct Rewards
            {
                //Actions theseActions;
                //Actions lastActions;
                Transform satTransform;
                Rigidbody satRigidbody;
                Transform tarTransform;
                Rigidbody tarRigidbody;
                //Collider[] satNonContactColliders;
                //Collider[] satCaptureColliders;

                public Rewards(
                    //Actions theseActions, Actions lastActions,
                    Transform satTransform, Rigidbody satRigidbody,
                    Transform tarTransform, Rigidbody tarRigidbody)
                //Collider[] satNonContactColliders, Collider[] satCaptureColliders)
                {
                    //this.theseActions = theseActions;
                    //this.lastActions = lastActions;
                    this.satTransform = satTransform;
                    this.satRigidbody = satRigidbody;
                    this.tarTransform = tarTransform;
                    this.tarRigidbody = tarRigidbody;
                    //this.satNonContactColliders = satNonContactColliders;
                    //this.satCaptureColliders = satCaptureColliders;
                }
                public float CalculateReward()
                {
                    // Proximity and closing speed rewards
                    float rwd_proximity;                    // +ve for being close to the satellite, up to a small limit where the model is penalised sharply for being too close (avoid collision with the satellite bus)
                    /*float rwd_relativeVelocity;             // Function of distance and approach speed (penalise going too fast when close to the target), +ve for zero relative velocity inside "capture zone"

                    // Angular velocity / heading rewards
                    float rwd_heading;                      // +ve for orienting the satellite's forward axis to the target's position origin
                    float rwd_axisOfRotationAlignment;      // Large +ve for aligning the satellite's forward axis with the satellite's axis of rotation
                    float rwd_axisOfRotationRelativeSpeed;  // +ve for matching the satellite's magnitude of angular velocity in the satellite's forward axis

                    // Contact and collision rewards
                    float rwd_satelliteBodyCollision;       // Sharp -ve for collisions between the satellite body and the target, scaled with speed
                    float rwd_gripperPadsInContact;         // +ve to encourage more gripper pads to contact the target
                    float rwd_gripperPadsAverageNormal;     // +ve for "flat" gripper pad contact with with the target's surface

                    
                    // Episode administration rewards
                    float rwd_successBonus;                 // Large +ve for meeting successful capture criteria. Penalty on failure or episode time-out
                    bool targetIsCaptured;                  // A bool representing confirmed target capture this step. If true for a specific number of steps, reward greatly and end the episode
                    */

                    // Gathering scene data
                    Vector3 displacement = satTransform.position - tarTransform.position;
                    Vector3 relativeVelocity = satRigidbody.velocity - tarRigidbody.velocity;

                    /////////// TEMP
                    // Goal: make the model "hover" at 1 unit of distance from the target
                    ///////////

                    float distance = displacement.magnitude;
                    rwd_proximity = (distance < 1) ? distance : 2 - distance;
                    return rwd_proximity;
                }
            }
        }
    }
}
