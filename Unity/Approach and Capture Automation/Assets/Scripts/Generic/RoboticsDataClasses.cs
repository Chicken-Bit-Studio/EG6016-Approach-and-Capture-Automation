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

                public Observations(ref IModel modelInterface, Transform satellite, Rigidbody satelliteRb, Transform target, Rigidbody targetRb)
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
                    if (output.Item1) { fuelTankData = output.Item2[0]; }
                    else { Debug.LogError("FuelTank observation data invalid - check FuelTankMonoBehaviour.GetMLObservation()."); fuelTankData = 1f; }

                    // Dynamic satellite Actuator data
                    List<float> tActuatorData = new();
                    foreach (Interfaces.IPlants.IActuator iface in modelInterface.plants.actuators.array)
                    {
                        output = iface.monoBehaviour.GetMLObservation();
                        if (output.Item1) { tActuatorData.AddRange(output.Item2); }
                        else { Debug.LogError("Actuator observation data invalid - check ActuatorMonoBehaviour.GetMLObservation()."); }
                    }
                    actuatorData = tActuatorData.ToArray();

                    // Dynamic satellite Gripper Pad data
                    List<float> tGripperPadData = new();
                    foreach (Interfaces.ISensors.IGripperPad iface in modelInterface.sensors.gripperPads.array)
                    {
                        output = iface.monoBehaviour.GetMLObservation();
                        if (output.Item1) { tGripperPadData.AddRange(output.Item2); }
                        else { Debug.LogError("GripperPad observation data invalid - check GripperPadMonoBehaviour.GetMLObservation()."); }
                    }
                    gripperPadData = tGripperPadData.ToArray();

                    // Dynamic satellite LiDAR data
                    output = modelInterface.sensors.lidars.array[0].monoBehaviour.GetMLObservation();
                    if (output.Item1) { lidarData = output.Item2; }
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

                // TODO: For now, the model prefab is "locked-in" - it won't be changed. We can use a constant to validate action size now, but this isn't automatically scalable.
                // floats 0.0 - 1.0 for each:
                //   actuators: 16
                //   thrusters: 24
                //   Total:     40
                public const int FEED_SIZE = 40;

                // Expected actions float array layout:
                // [0]-[15]:    ActuatorMonoBehaviour.input     (one each)
                // [16]-[39]:   ThrusterMonoBehaviour.input     (one each)

                public Actions(ref IModel modelInterface, float[] receivedFloats)
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
                        arrStr += $"Actuators ({modelInterface.plants.actuators.actuatorCount}):\t\t";
                        for (int i = 0; i < modelInterface.plants.actuators.actuatorCount; i++) { arrStr += receivedFloats[index] + ", "; index++; }
                        arrStr += $"\nThrusters ({modelInterface.plants.thrusters.thrusterCount}):\t\t\t";
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
                IModel modelInterface;
                CircularBuffer<Actions> actionHistory;
                Transform satTransform;
                Rigidbody satRigidbody;
                Transform tarTransform;
                Rigidbody tarRigidbody;

                public Rewards(
                    ref IModel modelInterface,
                    CircularBuffer<Actions> actionHistory,
                    Transform satTransform, Rigidbody satRigidbody,
                    Transform tarTransform, Rigidbody tarRigidbody)
                {
                    this.modelInterface = modelInterface;
                    this.actionHistory = actionHistory;
                    this.satTransform = satTransform;
                    this.satRigidbody = satRigidbody;
                    this.tarTransform = tarTransform;
                    this.tarRigidbody = tarRigidbody;
                }
                /*public float CalculateReward()
                {
                    // =============================================================================
                    // REWARD FUNCTION: Hover at 2.0 units with +Y alignment
                    // =============================================================================
                    // Goal: Train satellite to maintain stable station-keeping at 2.0 units from
                    //       target with +Y axis pointing toward target, matching target velocity,
                    //       with minimal tumble, stowed arms, and efficient fuel usage.
                    // Claude Sonnet 4.5
                    // =============================================================================

                    // -------------------------------------------------------------------------
                    // COMPONENT 1: Distance Reward (Gaussian peak at 2.0 units)
                    // -------------------------------------------------------------------------
                    // Gaussian reward peaked at targetDistance with sigma controlling width
                    float targetDistance = 2.0f;
                    float distanceSigma = 0.5f; // Controls tolerance (68% within ±0.5 units)

                    Vector3 positionDelta = tarTransform.position - satTransform.position;
                    float currentDistance = positionDelta.magnitude;
                    float distanceError = currentDistance - targetDistance;

                    // Gaussian: exp(-0.5 * (error/sigma)^2), scaled to [-1, 1] range
                    float distanceReward = Mathf.Exp(-0.5f * Mathf.Pow(distanceError / distanceSigma, 2.0f));
                    // Penalize being too close (collision risk) or too far (mission failure)
                    if (currentDistance < 0.5f) distanceReward = -1.0f;
                    if (currentDistance > 10.0f) distanceReward = -1.0f;

                    // -------------------------------------------------------------------------
                    // COMPONENT 2: Orientation Reward (+Y axis alignment with target)
                    // -------------------------------------------------------------------------
                    // Satellite's +Y axis should point directly at target
                    Vector3 satelliteUpWorld = satTransform.up; // +Y axis in world space
                    Vector3 directionToTarget = positionDelta.normalized;

                    // Dot product: 1.0 when perfectly aligned, -1.0 when opposite
                    float alignmentDot = Vector3.Dot(satelliteUpWorld, directionToTarget);
                    // Map [-1, 1] to [0, 1] with exponential emphasis on good alignment
                    float orientationReward = Mathf.Pow((alignmentDot + 1.0f) / 2.0f, 2.0f);

                    // -------------------------------------------------------------------------
                    // COMPONENT 3: Velocity Matching Reward
                    // -------------------------------------------------------------------------
                    // Satellite should match target's linear velocity for station-keeping
                    Vector3 relativeVelocity = satRigidbody.velocity - tarRigidbody.velocity;
                    float relativeSpeed = relativeVelocity.magnitude;

                    // Exponential decay: reward approaches 1.0 as relative speed approaches 0
                    float velocityDecayRate = 2.0f; // Decay constant (higher = stricter)
                    float velocityReward = Mathf.Exp(-velocityDecayRate * relativeSpeed);

                    // -------------------------------------------------------------------------
                    // COMPONENT 4: Stability Reward (minimize satellite angular velocity)
                    // -------------------------------------------------------------------------
                    // Satellite should minimize unproductive tumbling during operation
                    float satelliteAngularSpeed = satRigidbody.angularVelocity.magnitude; // rad/s

                    // Exponential decay: reward approaches 1.0 as tumble approaches 0
                    float stabilityDecayRate = 1.5f;
                    float stabilityReward = Mathf.Exp(-stabilityDecayRate * satelliteAngularSpeed);

                    // -------------------------------------------------------------------------
                    // COMPONENT 5: Arm Stowing Reward
                    // -------------------------------------------------------------------------
                    // Robotic arms should remain in neutral/stowed position
                    float armStowingReward = 1.0f; // Default: perfect stowing
                    float totalArmDeviation = 0.0f;
                    int actuatorCount = modelInterface.plants.actuators.actuatorCount;

                    if (actuatorCount > 0)
                    {
                        foreach (var actuator in modelInterface.plants.actuators.array)
                        {
                            // Get normalized joint angle observation (index [2] from GetMLObservation)
                            var obs = actuator.monoBehaviour.GetMLObservation();
                            if (obs.Item1 && obs.Item2.Length > 2)
                            {
                                float normalizedAngle = obs.Item2[2]; // [-1, 1] range
                                // Penalize deviation from center (0.0 = neutral position)
                                totalArmDeviation += Mathf.Abs(normalizedAngle);
                            }
                        }
                        // Average deviation per actuator, mapped to [0, 1] reward
                        float avgDeviation = totalArmDeviation / actuatorCount;
                        armStowingReward = Mathf.Exp(-2.0f * avgDeviation);
                    }

                    // -------------------------------------------------------------------------
                    // COMPONENT 6: Fuel Efficiency Penalty
                    // -------------------------------------------------------------------------
                    // Penalize wasteful thruster usage to encourage smooth, efficient control
                    float fuelEfficiencyReward = 1.0f; // Default: perfect efficiency

                    if (actionHistory.Count > 0)
                    {
                        // Calculate aggregate thruster usage (actions [16] through [39])
                        float totalThrusterUsage = 0.0f;
                        int thrusterCount = modelInterface.plants.thrusters.thrusterCount;

                        // Access the action values through the modelInterface plants
                        for (int i = 0; i < thrusterCount; i++)
                        {
                            float thrusterInput = modelInterface.plants.thrusters.array[i].monoBehaviour.input;
                            totalThrusterUsage += thrusterInput; // Sum of all thruster inputs [0, 1]
                        }

                        // Normalize by thruster count: avgUsage in [0, 1]
                        float avgThrusterUsage = totalThrusterUsage / thrusterCount;

                        // Quadratic penalty: small usage OK, high usage penalized
                        // Penalty increases with square of average usage
                        float efficiencyPenalty = Mathf.Pow(avgThrusterUsage, 2.0f);
                        fuelEfficiencyReward = 1.0f - 0.5f * efficiencyPenalty; // Max penalty: -0.5
                    }

                    // -------------------------------------------------------------------------
                    // COMPONENT 7: Success Bonus
                    // -------------------------------------------------------------------------
                    // Large bonus when all primary criteria are met simultaneously
                    float successBonus = 0.0f;

                    // Define success thresholds
                    bool distanceGood = Mathf.Abs(distanceError) < 0.3f; // Within 0.3 units of target
                    bool orientationGood = alignmentDot > 0.95f; // ~18° tolerance
                    bool velocityGood = relativeSpeed < 0.2f; // < 0.2 units/s relative speed
                    bool stabilityGood = satelliteAngularSpeed < 0.3f; // < 0.3 rad/s tumble

                    if (distanceGood && orientationGood && velocityGood && stabilityGood)
                    {
                        successBonus = 5.0f; // Substantial bonus for achieving all goals
                    }

                    // =============================================================================
                    // FINAL REWARD AGGREGATION
                    // =============================================================================
                    // Weighted sum of all components
                    // Weights are tuned to prioritize primary mission objectives

                    float reward = 0.0f;
                    reward += 2.0f * distanceReward;        // Weight: 2.0 (critical)
                    reward += 2.0f * orientationReward;     // Weight: 2.0 (critical)
                    reward += 1.5f * velocityReward;        // Weight: 1.5 (important)
                    reward += 1.0f * stabilityReward;       // Weight: 1.0 (important)
                    reward += 0.5f * armStowingReward;      // Weight: 0.5 (secondary)
                    reward += 0.3f * fuelEfficiencyReward;  // Weight: 0.3 (secondary)
                    reward += successBonus;                 // Additive bonus

                    // Expected range without bonus: ~[0, 7.3] in ideal conditions
                    // With success bonus: up to ~12.3
                    // In poor conditions (all components = 0): ~0.0
                    // Catastrophic failure: negative values from distance/efficiency penalties

                    return reward;
                }*/

                /// <summary>
                /// PROPOSED REWARD FUNCTION: Multi-Phase Debris Capture Mission
                ///
                /// =============================================================================
                /// DESIGN PHILOSOPHY & JUSTIFICATION
                /// =============================================================================
                ///
                /// This reward function is designed to guide a satellite agent through the complete
                /// debris capture mission lifecycle, from initial approach through to stable grasping.
                /// The design prioritizes:
                ///
                /// 1. PROGRESSIVE SKILL DEVELOPMENT: Rewards structured to first teach approach,
                ///    then hovering, then alignment, and finally grasping - mimicking curriculum
                ///    learning without explicit phase switching.
                ///
                /// 2. SAFETY-FIRST CONSTRAINTS: Heavy penalties for collision-risk behaviors
                ///    (excessive closing speed, wrong orientation during approach, fuel depletion).
                ///
                /// 3. FUEL CONSCIOUSNESS: Continuous efficiency incentives throughout all phases
                ///    to develop smooth, economical control strategies.
                ///
                /// 4. ROBUSTNESS TO INITIAL CONDITIONS: Works across the full randomization range
                ///    specified in EpisodeRandomisation (positions, velocities, orientations).
                ///
                /// 5. SPARSE BONUS STRUCTURE: Large bonuses only when demonstrating competency
                ///    in multiple simultaneous criteria, encouraging holistic skill mastery.
                ///
                /// =============================================================================
                /// MISSION PHASES & REWARD STRATEGY
                /// =============================================================================
                ///
                /// PHASE 1 - APPROACH (Distance > 3.0 units)
                /// ----------------------------------------
                /// Goal: Navigate toward target from random starting position while maintaining
                ///       safe approach velocity and conserving fuel.
                ///
                /// Key Rewards:
                /// - Distance reduction reward (getting closer is good)
                /// - Safe approach velocity reward (not too fast, prevents collision)
                /// - Coarse alignment reward (satellite generally facing target)
                /// - Fuel efficiency reward (learn economical trajectories early)
                ///
                /// Training Expectation: Agent learns to use thrusters to close distance while
                /// keeping relative velocity reasonable and avoiding wild tumbling.
                ///
                ///
                /// PHASE 2 - STATION-KEEPING (1.5 - 3.0 units)
                /// -------------------------------------------
                /// Goal: Maintain stable hover at optimal capture distance (~2.0 units) with
                ///       precise orientation alignment (+Y axis toward target).
                ///
                /// Key Rewards:
                /// - Gaussian distance reward (peaked at 2.0 units)
                /// - Precise orientation reward (+Y alignment with target)
                /// - Velocity matching reward (minimize relative drift)
                /// - Stability reward (minimize angular velocity / tumbling)
                /// - Arm stowing reward (keep manipulators retracted during approach)
                ///
                /// Training Expectation: Agent learns fine position control, develops steady
                /// hover behavior, and maintains proper orientation for eventual capture.
                ///
                ///
                /// PHASE 3 - GRASPING (Distance < 1.5 units, good alignment)
                /// ----------------------------------------------------------
                /// Goal: Deploy manipulator arms and establish firm contact with target using
                ///       gripper pads while maintaining position and stability.
                ///
                /// Key Rewards:
                /// - Gripper contact rewards (each pad touching target)
                /// - Contact quality rewards (good grip force, appropriate depth)
                /// - Multi-point contact bonus (≥2 pads gripping simultaneously)
                /// - Stable grip reward (maintaining contact over time)
                /// - Continued position/orientation maintenance during grip
                ///
                /// Training Expectation: Agent learns to extend arms, make contact, and
                /// maintain stable multi-point grasp while compensating for contact forces.
                ///
                /// =============================================================================
                /// REWARD COMPONENT BREAKDOWN
                /// =============================================================================
                ///
                /// The reward function consists of 12 primary components, each serving a
                /// specific training objective. Components are weighted based on mission
                /// criticality and desired learning progression.
                ///
                /// CRITICAL COMPONENTS (Weight: 2.0-3.0):
                /// - Distance management: Core mission objective
                /// - Orientation alignment: Required for safe capture
                /// - Approach safety: Prevents catastrophic collisions
                ///
                /// IMPORTANT COMPONENTS (Weight: 1.0-1.5):
                /// - Velocity matching: Necessary for stable station-keeping
                /// - Stability control: Prevents uncontrolled tumbling
                /// - Gripper contact: Direct capture mechanism
                ///
                /// SECONDARY COMPONENTS (Weight: 0.3-0.7):
                /// - Fuel efficiency: Long-term operational constraint
                /// - Arm management: Proper manipulator usage
                /// - Contact quality: Refinement of grasping technique
                ///
                /// BONUS COMPONENTS (Additive, 5.0-20.0):
                /// - Phase completion bonuses: Large rewards for achieving key milestones
                /// - Multi-criteria success: Substantial bonus for simultaneous goal achievement
                ///
                /// =============================================================================
                /// IMPLEMENTATION NOTES
                /// =============================================================================
                ///
                /// 1. REWARD SCALING: Base continuous rewards sum to ~0-12 range in ideal
                ///    conditions. Bonuses can add 5-20 additional reward, making episodes
                ///    with milestone achievement clearly superior.
                ///
                /// 2. NORMALIZATION: All sensor inputs are pre-normalized by their respective
                ///    MonoBehaviour classes (see GetMLObservation() methods). This reward
                ///    function works with these normalized values.
                ///
                /// 3. EXPONENTIAL SHAPING: Many components use exponential decay (e^-kx) to
                ///    provide smooth gradients that guide learning while emphasizing excellence.
                ///
                /// 4. CATASTROPHIC PENALTIES: Certain failure modes (collision risk, fuel
                ///    depletion, excessive drift) incur heavy penalties to strongly discourage
                ///    these behaviors during early training.
                ///
                /// 5. TEMPORAL CONSISTENCY: The CircularBuffer of action history allows for
                ///    temporal reward components (e.g., penalizing thruster oscillation) but
                ///    is not heavily used to maintain reward function clarity.
                ///
                /// 6. HYPERPARAMETER TUNING: The constants defined below (sigma values, decay
                ///    rates, thresholds, weights) were chosen based on the physical scale of
                ///    the simulation (Unity units) and typical velocity/force magnitudes. These
                ///    may require tuning based on training progress.
                ///
                /// =============================================================================
                /// EXPECTED TRAINING PROGRESSION
                /// =============================================================================
                ///
                /// EARLY TRAINING (0-200k steps):
                /// - Agent learns basic thruster control
                /// - Random exploration discovers that approaching target yields reward
                /// - Begins to reduce distance while learning to avoid catastrophic failures
                /// - Episode reward: -5 to +3 (mostly negative due to exploration)
                ///
                /// MID TRAINING (200k-600k steps):
                /// - Consistent approach behavior established
                /// - Begins achieving station-keeping at ~2 unit distance
                /// - Learns orientation control (+Y alignment)
                /// - Occasional bonuses for simultaneous goal achievement
                /// - Episode reward: +3 to +10 (positive, occasional spikes to +15)
                ///
                /// LATE TRAINING (600k-1M+ steps):
                /// - Reliable hover and orientation maintenance
                /// - Begins experimenting with arm deployment
                /// - Achieves intermittent gripper contact
                /// - Learns to maintain stable multi-point grasps
                /// - Episode reward: +8 to +20+ (consistent high performance with bonuses)
                ///
                /// MASTERY (1M+ steps):
                /// - Smooth, fuel-efficient approach from any starting condition
                /// - Stable hover with precise orientation within seconds
                /// - Reliable multi-point grasping with good contact quality
                /// - Consistent episode completion with large cumulative rewards
                /// - Episode reward: +15 to +30+ (mission success on most episodes)
                ///
                /// =============================================================================
                /// </summary>
                /// <returns>Scalar reward value for the current timestep.</returns>
                public float CalculateReward()
                {
                    // =========================================================================
                    // PHASE DETECTION & CONTEXTUAL VARIABLES
                    // =========================================================================

                    Vector3 positionDelta = tarTransform.position - satTransform.position;
                    float currentDistance = positionDelta.magnitude;
                    Vector3 directionToTarget = positionDelta.normalized;

                    // Define mission phase based on distance and alignment state
                    bool isApproachPhase = currentDistance > 3.0f;
                    bool isStationKeepingPhase = currentDistance >= 1.5f && currentDistance <= 3.0f;
                    bool isGraspingPhase = currentDistance < 1.5f;


                    // =========================================================================
                    // COMPONENT 1: DISTANCE REWARD
                    // =========================================================================
                    // Weight: 2.5 (CRITICAL - primary mission objective)
                    //
                    // Justification:
                    // Distance management is the foundation of the entire mission. This component
                    // uses a dual-strategy approach:
                    //
                    // APPROACH PHASE: Rewards getting closer (exponential decay from maximum distance)
                    // - Encourages aggressive approach when far away
                    // - Gradient guides agent toward target from random starting positions
                    // - Decays smoothly as distance decreases to avoid sudden reward changes
                    //
                    // STATION-KEEPING PHASE: Gaussian reward peaked at optimal distance (2.0 units)
                    // - Creates attractive potential well at ideal capture distance
                    // - Penalizes both too-close (collision risk) and too-far (inefficient capture)
                    // - Sigma of 0.5 provides ~68% reward within ±0.5 units (acceptable tolerance)
                    //
                    // CATASTROPHIC PENALTIES:
                    // - Distance < 0.5 units: -2.0 (collision imminent)
                    // - Distance > 15 units: -2.0 (mission failure / excessive drift)
                    //
                    // Expected range: [-2.0, 1.0]
                    // =========================================================================

                    float distanceReward = 0.0f;
                    float targetHoverDistance = 2.0f;
                    float distanceSigma = 0.5f;

                    // Catastrophic failure penalties
                    if (currentDistance < 0.5f)
                    {
                        distanceReward = -2.0f; // Too close - collision risk!
                    }
                    else if (currentDistance > 15.0f)
                    {
                        distanceReward = -2.0f; // Too far - mission failure
                    }
                    else if (isApproachPhase)
                    {
                        // Approach phase: reward getting closer (exponential decay from max distance)
                        // At 15 units: reward ≈ 0.0, At 3 units: reward ≈ 0.8
                        float approachDecayRate = 0.15f;
                        distanceReward = Mathf.Exp(-approachDecayRate * currentDistance);
                    }
                    else
                    {
                        // Station-keeping & grasping: Gaussian peaked at target hover distance
                        float distanceError = currentDistance - targetHoverDistance;
                        distanceReward = Mathf.Exp(-0.5f * Mathf.Pow(distanceError / distanceSigma, 2.0f));
                    }


                    // =========================================================================
                    // COMPONENT 2: ORIENTATION ALIGNMENT REWARD
                    // =========================================================================
                    // Weight: 2.0 (CRITICAL - required for safe capture)
                    //
                    // Justification:
                    // Proper orientation is essential for successful debris capture. The satellite's
                    // +Y axis must point toward the target to position manipulator arms correctly.
                    // Poor alignment during approach creates collision risk and makes grasping
                    // geometrically impossible.
                    //
                    // APPROACH PHASE: Coarse alignment acceptable (dot > 0.5, ≈60° tolerance)
                    // - Allows agent to focus on gross navigation
                    // - Prevents excessive tumbling penalties during initial approach
                    // - Uses linear mapping for broad gradient
                    //
                    // STATION-KEEPING & GRASPING: Precise alignment required (dot > 0.95, ≈18° tolerance)
                    // - Exponential emphasis on good alignment (squared term)
                    // - Necessary for manipulator deployment and contact
                    // - Small misalignments heavily penalized to enforce precision
                    //
                    // Expected range: [0.0, 1.0]
                    // =========================================================================

                    Vector3 satelliteUpWorld = satTransform.up; // +Y axis in world space
                    float alignmentDot = Vector3.Dot(satelliteUpWorld, directionToTarget);
                    float orientationReward = 0.0f;

                    if (isApproachPhase)
                    {
                        // Approach: coarse alignment is acceptable (>60° = 0.5 dot product)
                        // Map [-1, 1] -> [0, 1] linearly for approach phase
                        orientationReward = (alignmentDot + 1.0f) / 2.0f;
                    }
                    else
                    {
                        // Station-keeping & grasping: precise alignment required
                        // Exponential emphasis: (dot+1)/2 mapped to [0,1], then squared
                        orientationReward = Mathf.Pow((alignmentDot + 1.0f) / 2.0f, 2.0f);
                    }


                    // =========================================================================
                    // COMPONENT 3: APPROACH SAFETY REWARD (Closing Speed Management)
                    // =========================================================================
                    // Weight: 2.5 (CRITICAL - prevents catastrophic collisions)
                    //
                    // Justification:
                    // Uncontrolled high-speed approach is the most common catastrophic failure
                    // mode in debris capture missions. This component enforces safe approach
                    // velocities that scale appropriately with distance.
                    //
                    // DYNAMIC SAFE SPEED: Scales with distance (closer = slower required)
                    // - Far away (10+ units): 2.0 m/s closing speed acceptable
                    // - Close range (2 units): 0.4 m/s maximum safe speed
                    // - Prevents "kamikaze" approaches that yield short-term distance rewards
                    //
                    // PENALTY STRUCTURE:
                    // - Below safe speed: Full reward (1.0)
                    // - Moderately over: Exponential decay penalty
                    // - Severely over (2x safe speed): Heavy penalty (-1.0)
                    //
                    // This is one of the highest-weighted components because collision avoidance
                    // is a hard constraint in real space operations.
                    //
                    // Expected range: [-1.0, 1.0]
                    // =========================================================================

                    Vector3 relativeVelocity = satRigidbody.velocity - tarRigidbody.velocity;
                    float closingSpeed = -Vector3.Dot(relativeVelocity, directionToTarget); // Negative = approaching

                    // Define safe approach speed based on distance (closer = slower)
                    // At 10 units: 2.0 m/s, At 2 units: 0.4 m/s, At 1 unit: 0.3 m/s
                    float safeApproachSpeed = Mathf.Lerp(0.3f, 2.0f, Mathf.Clamp01((currentDistance - 1.0f) / 9.0f));

                    float approachSafetyReward = 0.0f;

                    if (closingSpeed <= safeApproachSpeed)
                    {
                        // Safe approach speed - full reward
                        approachSafetyReward = 1.0f;
                    }
                    else
                    {
                        // Exceeding safe speed - exponential penalty
                        float speedExcess = closingSpeed - safeApproachSpeed;
                        float excessRatio = speedExcess / safeApproachSpeed;

                        if (excessRatio > 2.0f)
                        {
                            // Dangerously fast - severe penalty
                            approachSafetyReward = -1.0f;
                        }
                        else
                        {
                            // Moderately over safe speed - exponential decay
                            approachSafetyReward = Mathf.Exp(-2.0f * excessRatio);
                        }
                    }


                    // =========================================================================
                    // COMPONENT 4: VELOCITY MATCHING REWARD
                    // =========================================================================
                    // Weight: 1.5 (IMPORTANT - necessary for stable station-keeping)
                    //
                    // Justification:
                    // Once at the target distance, the satellite must match the target's velocity
                    // to maintain relative position (station-keeping). This is less critical
                    // during approach but essential during hovering and grasping.
                    //
                    // APPROACH PHASE: Lower weight (0.5x) - some relative motion acceptable
                    // - Agent should focus on closing distance, not perfect velocity match
                    // - Still provides gentle gradient toward velocity matching
                    //
                    // STATION-KEEPING & GRASPING: Full weight - precision required
                    // - Exponential decay emphasizes minimizing relative velocity
                    // - Decay rate of 2.0 means 0.5 m/s relative speed → ~0.37 reward
                    // - Essential for maintaining stable position during arm deployment
                    //
                    // Expected range: [0.0, 1.0]
                    // =========================================================================

                    float relativeSpeed = relativeVelocity.magnitude;
                    float velocityDecayRate = 2.0f;
                    float velocityReward = Mathf.Exp(-velocityDecayRate * relativeSpeed);

                    // Reduce velocity matching importance during approach
                    if (isApproachPhase)
                    {
                        velocityReward *= 0.5f; // Half weight during approach
                    }


                    // =========================================================================
                    // COMPONENT 5: SATELLITE STABILITY REWARD (Angular Velocity Control)
                    // =========================================================================
                    // Weight: 1.2 (IMPORTANT - prevents uncontrolled tumbling)
                    //
                    // Justification:
                    // Excessive satellite rotation creates multiple problems:
                    // - Makes orientation alignment impossible to maintain
                    // - Causes thruster firings to be inefficient (constantly changing direction)
                    // - Prevents successful gripper contact (arms spinning past target)
                    // - Realistic operational constraint (attitude control propellant cost)
                    //
                    // The exponential decay with rate 1.5 means:
                    /// - 0.1 rad/s (~6°/s): ~0.86 reward (acceptable slow drift)
                    /// - 0.5 rad/s (~29°/s): ~0.47 reward (moderate tumble, penalized)
                    /// - 1.0 rad/s (~57°/s): ~0.22 reward (severe tumble, heavily penalized)
                    ///
                    /// Increased to weight 1.2 (from typical 1.0) because stability is critical
                    /// for both the station-keeping and grasping phases.
                    ///
                    /// Expected range: [0.0, 1.0]
                    /// =========================================================================

                    float satelliteAngularSpeed = satRigidbody.angularVelocity.magnitude;
                    float stabilityDecayRate = 1.5f;
                    float stabilityReward = Mathf.Exp(-stabilityDecayRate * satelliteAngularSpeed);


                    // =========================================================================
                    // COMPONENT 6: ARM POSITIONING REWARD
                    // =========================================================================
                    // Weight: 0.6 (SECONDARY - proper manipulator management)
                    //
                    // Justification:
                    // The robotic arms should be managed appropriately based on mission phase:
                    //
                    // APPROACH & STATION-KEEPING: Arms stowed (neutral position)
                    // - Reduces collision cross-section during approach
                    // - Minimizes drag from residual atmosphere (if simulated)
                    // - Prevents accidental contact before ready
                    // - Normalized angle 0.0 = neutral, ±1.0 = at joint limits
                    ///
                    /// GRASPING PHASE: Arms deployed (deviation from neutral expected and acceptable)
                    /// - Agent should extend arms to make contact
                    /// - Penalizing arm movement during grasping would prevent capture
                    /// - Reward becomes ~0.0 during grasping (neutral, neither reward nor penalty)
                    ///
                    /// The exponential decay (rate 2.0) heavily penalizes large deviations during
                    /// approach, encouraging the agent to keep arms retracted until close.
                    ///
                    /// Expected range: [0.0, 1.0]
                    /// =========================================================================

                    float armPositioningReward = 1.0f;
                    float totalArmDeviation = 0.0f;
                    int actuatorCount = modelInterface.plants.actuators.actuatorCount;

                    if (actuatorCount > 0)
                    {
                        foreach (var actuator in modelInterface.plants.actuators.array)
                        {
                            var obs = actuator.monoBehaviour.GetMLObservation();
                            if (obs.Item1 && obs.Item2.Length > 2)
                            {
                                float normalizedAngle = obs.Item2[2]; // [-1, 1] range from GetMLObservation
                                totalArmDeviation += Mathf.Abs(normalizedAngle);
                            }
                        }

                        float avgDeviation = totalArmDeviation / actuatorCount;

                        if (isGraspingPhase)
                        {
                            // During grasping, don't penalize arm deployment (it's necessary)
                            armPositioningReward = 0.5f; // Neutral reward during grasping
                        }
                        else
                        {
                            // During approach/station-keeping, reward stowed arms
                            armPositioningReward = Mathf.Exp(-2.0f * avgDeviation);
                        }
                    }


                    // =========================================================================
                    // COMPONENT 7: GRIPPER CONTACT REWARD
                    // =========================================================================
                    // Weight: 3.0 (CRITICAL during grasping phase - direct capture mechanism)
                    //
                    // Justification:
                    // Gripper contact is the ultimate mission objective - the agent must learn
                    // to make and maintain physical contact with the target using gripper pads.
                    //
                    // This component rewards:
                    // - Any gripper pad making contact (+0.5 per pad)
                    // - Multiple simultaneous contacts (additive)
                    // - Maintained contact over time (persistent reward signal)
                    //
                    // PHASE GATING: Only active during grasping phase (distance < 1.5 units)
                    /// - Prevents agent from trying to grasp during approach (unsafe)
                    /// - Focuses learning on appropriate behaviors for each phase
                    ///
                    /// With 4 gripper pads, maximum reward is 2.0 (all pads contacting).
                    /// Each pad contributes 0.5, providing clear gradient for learning to
                    /// deploy additional arms and achieve multi-point grasps.
                    ///
                    /// Expected range: [0.0, 2.0]
                    /// =========================================================================

                    float gripperContactReward = 0.0f;
                    int contactingPads = 0;

                    if (isGraspingPhase && modelInterface.sensors.gripperPads.gripperPadCount > 0)
                    {
                        foreach (var pad in modelInterface.sensors.gripperPads.array)
                        {
                            var obs = pad.monoBehaviour.GetMLObservation();
                            if (obs.Item1 && obs.Item2.Length > 0)
                            {
                                float contactFlag = obs.Item2[0]; // [0] = contact flag (0.0 or 1.0)

                                if (contactFlag > 0.5f) // Pad is in contact
                                {
                                    contactingPads++;
                                    gripperContactReward += 0.5f; // +0.5 per contacting pad
                                }
                            }
                        }
                    }


                    // =========================================================================
                    // COMPONENT 8: CONTACT QUALITY REWARD
                    // =========================================================================
                    // Weight: 0.7 (SECONDARY - refinement of grasping technique)
                    //
                    // Justification:
                    // Not all contact is equal - the agent should learn to establish firm,
                    // stable grips rather than just brushing against the target. This component
                    // analyzes the quality of gripper contact using detailed sensor data.
                    ///
                    /// For each contacting pad, quality is assessed based on:
                    ///
                    /// 1. CONTACT FORCE (obs[4]): Normalized grip strength
                    ///    - Stronger grip indicates better hold
                    ///    - Target: 0.4-0.8 range (firm but not crushing)
                    ///    - Too weak: poor hold security
                    ///    - Too strong: risk of damaging target or pad
                    ///
                    /// 2. CONTACT DEPTH (obs[5]): Penetration/engagement
                    ///    - Deeper contact indicates stable engagement
                    ///    - Target: 0.3-0.7 range (good surface engagement)
                    ///    - Too shallow: easily broken contact
                    ///    - Too deep: potential mechanical interference
                    ///
                    /// 3. RELATIVE VELOCITY (obs[6]): Slip/slide indicator
                    ///    - Low relative velocity indicates stable grip
                    ///    - Target: near 0.0 (static friction, no sliding)
                    ///    - High velocity: slipping grasp, poor hold
                    ///
                    /// Quality score uses Gaussian-like rewards peaked at ideal values,
                    /// encouraging the agent to not just make contact but maintain high-quality
                    /// grasps. This is secondary to making contact at all (Component 7).
                    ///
                    /// Expected range: [0.0, 1.0] (averaged across contacting pads)
                    /// =========================================================================

                    float contactQualityReward = 0.0f;

                    if (contactingPads > 0)
                    {
                        float totalQuality = 0.0f;

                        foreach (var pad in modelInterface.sensors.gripperPads.array)
                        {
                            var obs = pad.monoBehaviour.GetMLObservation();
                            if (obs.Item1 && obs.Item2.Length >= 7)
                            {
                                float contactFlag = obs.Item2[0];

                                if (contactFlag > 0.5f) // Only evaluate quality for contacting pads
                                {
                                    float contactForce = obs.Item2[4];     // [0, 1] normalized
                                    float contactDepth = obs.Item2[5];     // [0, 1] normalized
                                    float relativeVelAlongNormal = obs.Item2[6]; // [-1, 1] normalized

                                    // Quality Component 1: Contact force in ideal range [0.4, 0.8]
                                    float forceQuality = 0.0f;
                                    if (contactForce >= 0.4f && contactForce <= 0.8f)
                                    {
                                        forceQuality = 1.0f; // Ideal grip strength
                                    }
                                    else
                                    {
                                        // Gaussian falloff from ideal range
                                        float forceTarget = 0.6f; // Center of ideal range
                                        float forceSigma = 0.3f;
                                        forceQuality = Mathf.Exp(-0.5f * Mathf.Pow((contactForce - forceTarget) / forceSigma, 2.0f));
                                    }

                                    // Quality Component 2: Contact depth in ideal range [0.3, 0.7]
                                    float depthQuality = 0.0f;
                                    if (contactDepth >= 0.3f && contactDepth <= 0.7f)
                                    {
                                        depthQuality = 1.0f; // Ideal engagement depth
                                    }
                                    else
                                    {
                                        float depthTarget = 0.5f;
                                        float depthSigma = 0.3f;
                                        depthQuality = Mathf.Exp(-0.5f * Mathf.Pow((contactDepth - depthTarget) / depthSigma, 2.0f));
                                    }

                                    // Quality Component 3: Low relative velocity (stable grip)
                                    float velQuality = Mathf.Exp(-3.0f * Mathf.Abs(relativeVelAlongNormal));

                                    // Average quality across three metrics for this pad
                                    float padQuality = (forceQuality + depthQuality + velQuality) / 3.0f;
                                    totalQuality += padQuality;
                                }
                            }
                        }

                        // Average quality across all contacting pads
                        contactQualityReward = totalQuality / contactingPads;
                    }


                    // =========================================================================
                    // COMPONENT 9: MULTI-POINT CONTACT REWARD
                    // =========================================================================
                    // Weight: 1.0 (IMPORTANT - stable grasp requires multiple contact points)
                    //
                    // Justification:
                    // A single-point contact is inherently unstable for grasping tumbling debris.
                    /// Multiple simultaneous contact points provide:
                    /// - Geometric constraint (prevents target from rotating)
                    /// - Force distribution (more stable, less likely to break)
                    /// - Redundancy (if one pad loses contact, others maintain grip)
                    ///
                    /// This component provides escalating rewards for achieving multiple contacts:
                    /// - 0 pads: 0.0 reward
                    /// - 1 pad:  0.3 reward (marginal grasp)
                    /// - 2 pads: 0.7 reward (good two-point grip)
                    /// - 3 pads: 0.9 reward (excellent stability)
                    /// - 4 pads: 1.0 reward (perfect full coverage)
                    ///
                    /// Non-linear scaling (square root-like) makes achieving first 2-3 contacts
                    /// more rewarding than going from 3→4, which matches the physical reality
                    /// that the stability benefit diminishes with additional contact points.
                    ///
                    /// Expected range: [0.0, 1.0]
                    /// =========================================================================

                    float multiPointContactReward = 0.0f;

                    if (isGraspingPhase)
                    {
                        int maxPads = modelInterface.sensors.gripperPads.gripperPadCount;
                        if (maxPads > 0)
                        {
                            // Non-linear reward scaling for multiple simultaneous contacts
                            switch (contactingPads)
                            {
                                case 0:
                                    multiPointContactReward = 0.0f;
                                    break;
                                case 1:
                                    multiPointContactReward = 0.3f; // Single point - marginal
                                    break;
                                case 2:
                                    multiPointContactReward = 0.7f; // Two points - good!
                                    break;
                                case 3:
                                    multiPointContactReward = 0.9f; // Three points - excellent!
                                    break;
                                default: // 4 or more
                                    multiPointContactReward = 1.0f; // Full coverage - perfect!
                                    break;
                            }
                        }
                    }


                    // =========================================================================
                    // COMPONENT 10: FUEL EFFICIENCY REWARD
                    // =========================================================================
                    // Weight: 0.4 (SECONDARY - long-term operational constraint)
                    //
                    // Justification:
                    // Fuel is a finite resource in real space operations. The agent should learn
                    // smooth, economical control strategies rather than wasteful bang-bang control
                    /// or unnecessary thruster oscillations.
                    ///
                    /// This component penalizes excessive thruster usage through quadratic penalty:
                    /// - Average thruster usage 0.0-0.3: minimal penalty (efficient control)
                    /// - Average thruster usage 0.3-0.6: moderate penalty (acceptable)
                    /// - Average thruster usage 0.6-1.0: heavy penalty (wasteful)
                    ///
                    /// The quadratic term (squared) means high usage is disproportionately penalized:
                    /// - 50% average usage → 0.125 penalty
                    /// - 100% average usage → 0.5 penalty
                    ///
                    /// CRITICAL FAILURE: If fuel tank below 10% capacity, severe penalty (-0.5)
                    /// This encourages the agent to complete the mission before fuel depletion.
                    ///
                    /// Weight is relatively low (0.4) because fuel efficiency is secondary to
                    /// mission success, but present throughout training to shape economical behaviors.
                    ///
                    /// Expected range: [-0.5, 1.0]
                    /// =========================================================================

                    float fuelEfficiencyReward = 1.0f;

                    // Check fuel level from fuel tank sensor
                    if (modelInterface.sensors.fuelTanks.fuelTankCount > 0)
                    {
                        var fuelObs = modelInterface.sensors.fuelTanks.array[0].monoBehaviour.GetMLObservation();
                        if (fuelObs.Item1)
                        {
                            float fuelLevel = fuelObs.Item2[0]; // [0, 1] normalized

                            // Critical low fuel penalty
                            if (fuelLevel < 0.1f)
                            {
                                fuelEfficiencyReward = -0.5f; // Severe penalty for fuel depletion
                            }
                        }
                    }

                    // Calculate thruster usage efficiency
                    if (fuelEfficiencyReward > 0.0f) // Only if not already penalized for low fuel
                    {
                        float totalThrusterUsage = 0.0f;
                        int thrusterCount = modelInterface.plants.thrusters.thrusterCount;

                        if (thrusterCount > 0)
                        {
                            foreach (var thruster in modelInterface.plants.thrusters.array)
                            {
                                totalThrusterUsage += thruster.monoBehaviour.input; // [0, 1] per thruster
                            }

                            float avgThrusterUsage = totalThrusterUsage / thrusterCount;

                            // Quadratic penalty for excessive thruster usage
                            float usagePenalty = Mathf.Pow(avgThrusterUsage, 2.0f);
                            fuelEfficiencyReward = 1.0f - (0.5f * usagePenalty);
                        }
                    }


                    // =========================================================================
                    // COMPONENT 11: ANGULAR VELOCITY MATCHING REWARD
                    // =========================================================================
                    // Weight: 0.8 (SECONDARY - advanced station-keeping technique)
                    //
                    // Justification:
                    // For truly stable relative motion with a tumbling target, the satellite
                    // should not only match linear velocity but also angular velocity. This
                    /// creates a rotating reference frame where the target appears stationary.
                    ///
                    /// This is an advanced technique that becomes important during grasping:
                    /// - If target is spinning and satellite is not, relative motion causes
                    ///   gripper pads to slide across target surface
                    /// - Matching angular velocity creates stable relative orientation
                    /// - Allows for sustained multi-point contact without slippage
                    ///
                    /// PHASE GATING: Only active during grasping phase
                    /// - Not important during approach (would be distracting)
                    /// - Becomes relevant when trying to maintain contact
                    ///
                    /// The exponential decay (rate 1.0) is gentler than other stability rewards
                    /// because perfect angular velocity matching is difficult and not always
                    /// necessary - the agent just needs to be "close enough" to maintain grip.
                    ///
                    /// Expected range: [0.0, 1.0]
                    /// =========================================================================

                    float angularVelocityMatchingReward = 0.0f;

                    if (isGraspingPhase)
                    {
                        // Get angular velocities in world frame
                        Vector3 satAngularVel = satRigidbody.angularVelocity;
                        Vector3 tarAngularVel = tarRigidbody.angularVelocity;
                        Vector3 relativeAngularVelocity = satAngularVel - tarAngularVel;
                        float relativeAngularSpeed = relativeAngularVelocity.magnitude;

                        // Exponential decay reward for matching target's rotation
                        float angularMatchDecayRate = 1.0f;
                        angularVelocityMatchingReward = Mathf.Exp(-angularMatchDecayRate * relativeAngularSpeed);
                    }


                    // =========================================================================
                    // COMPONENT 12: TARGET ANGULAR VELOCITY AWARENESS
                    // =========================================================================
                    // Weight: 0.3 (TERTIARY - provides context about target behavior)
                    //
                    // Justification:
                    // This is a subtle reward component that provides the agent with implicit
                    /// feedback about the difficulty of the current episode. Faster-tumbling
                    /// targets are inherently harder to capture.
                    ///
                    /// The reward gently encourages the agent to:
                    /// - Recognize that slower-tumbling targets are "easier" opportunities
                    /// - Adjust strategy based on target motion (faster tumble → more careful approach)
                    ///
                    /// This is the lowest-weighted component because it doesn't directly incentivize
                    /// any specific agent behavior - the agent cannot control target motion. However,
                    /// it provides valuable context that can help the agent learn adaptive strategies.
                    ///
                    /// Exponential decay (rate 0.8) means:
                    /// - 0.0 rad/s (stationary): 1.0 reward (easiest case)
                    /// - 0.5 rad/s (moderate tumble): 0.67 reward
                    /// - 1.5 rad/s (fast tumble): 0.30 reward (challenging case)
                    ///
                    /// Expected range: [0.0, 1.0]
                    /// =========================================================================

                    float targetAngularSpeed = tarRigidbody.angularVelocity.magnitude;
                    float targetMotionDecayRate = 0.8f;
                    float targetMotionReward = Mathf.Exp(-targetMotionDecayRate * targetAngularSpeed);


                    // =========================================================================
                    // BONUS COMPONENTS: MILESTONE ACHIEVEMENTS
                    // =========================================================================
                    // These are large, sparse rewards for achieving significant mission milestones.
                    // They provide strong learning signals for major accomplishments.
                    // =========================================================================

                    float successBonus = 0.0f;


                    // -------------------------------------------------------------------------
                    // BONUS 1: Station-Keeping Achievement
                    // -------------------------------------------------------------------------
                    // Value: +5.0
                    //
                    // Awarded when the agent successfully maintains stable hover at the target
                    // distance with good orientation and velocity matching simultaneously.
                    //
                    // Criteria:
                    // - Distance: Within 0.4 units of 2.0 unit target (1.6-2.4 range)
                    // - Orientation: +Y axis within 22.5° of target direction (dot > 0.92)
                    // - Velocity: Relative speed < 0.25 m/s
                    // - Stability: Angular velocity < 0.4 rad/s (< 23°/s tumble)
                    //
                    // This bonus marks the completion of the "approach and hover" phase and
                    // provides a strong signal that the agent has mastered basic station-keeping.
                    // -------------------------------------------------------------------------

                    bool stationKeepingAchieved =
                        Mathf.Abs(currentDistance - targetHoverDistance) < 0.4f &&  // Distance good
                        alignmentDot > 0.92f &&                                      // Orientation good (≈22.5° tolerance)
                        relativeSpeed < 0.25f &&                                     // Velocity matched
                        satelliteAngularSpeed < 0.4f;                                // Stable (not tumbling)

                    if (stationKeepingAchieved)
                    {
                        successBonus += 5.0f;
                    }


                    // -------------------------------------------------------------------------
                    // BONUS 2: First Contact Achievement
                    // -------------------------------------------------------------------------
                    // Value: +3.0
                    //
                    // Awarded when the agent achieves first gripper contact with the target
                    // while maintaining reasonable position and stability.
                    //
                    // Criteria:
                    // - At least one gripper pad in contact with target
                    // - Distance: < 2.5 units (close enough for arms to reach)
                    // - Stability: Angular velocity < 0.6 rad/s (not wildly tumbling)
                    //
                    // This bonus marks the transition into the grasping phase and encourages
                    // the agent to extend arms and make contact once in position.
                    // -------------------------------------------------------------------------

                    bool firstContactAchieved =
                        contactingPads >= 1 &&
                        currentDistance < 2.5f &&
                        satelliteAngularSpeed < 0.6f;

                    if (firstContactAchieved)
                    {
                        successBonus += 3.0f;
                    }


                    // -------------------------------------------------------------------------
                    // BONUS 3: Stable Multi-Point Grasp Achievement
                    // -------------------------------------------------------------------------
                    // Value: +10.0 (MAJOR MILESTONE)
                    //
                    // Awarded when the agent achieves a stable, high-quality multi-point grasp
                    // of the target - the ultimate mission objective.
                    //
                    // Criteria:
                    // - At least 2 gripper pads in simultaneous contact
                    // - Average contact quality > 0.6 (firm, stable grips)
                    // - Distance: Close range (< 2.0 units)
                    // - Orientation: Good alignment maintained (dot > 0.85)
                    // - Stability: Low tumble rate (< 0.5 rad/s)
                    /// - Velocity: Low relative motion (< 0.3 m/s)
                    ///
                    /// This is the largest single bonus and represents successful capture.
                    /// Episodes achieving this bonus should be considered mission success.
                    /// -------------------------------------------------------------------------

                    bool stableGraspAchieved =
                        contactingPads >= 2 &&
                        contactQualityReward > 0.6f &&
                        currentDistance < 2.0f &&
                        alignmentDot > 0.85f &&
                        satelliteAngularSpeed < 0.5f &&
                        relativeSpeed < 0.3f;

                    if (stableGraspAchieved)
                    {
                        successBonus += 10.0f;
                    }


                    // -------------------------------------------------------------------------
                    // BONUS 4: Perfect Capture Achievement
                    // -------------------------------------------------------------------------
                    // Value: +20.0 (EXCEPTIONAL PERFORMANCE)
                    //
                    // Awarded for achieving near-perfect capture conditions - all mission
                    // parameters simultaneously excellent. This should be rare and marks
                    // truly exceptional agent performance.
                    ///
                    /// Criteria:
                    /// - At least 3 gripper pads in simultaneous contact
                    /// - Excellent contact quality (average > 0.8)
                    /// - Precise distance control (within 0.3 units of target)
                    /// - Excellent orientation (dot > 0.95, ≈18° tolerance)
                    /// - Excellent stability (< 0.3 rad/s tumble)
                    /// - Excellent velocity matching (< 0.15 m/s relative)
                    /// - Angular velocity matching (< 0.3 rad/s relative angular velocity)
                    ///
                    /// Achieving this bonus should be the ultimate training goal and represents
                    /// performance that would be acceptable for real debris removal operations.
                    /// -------------------------------------------------------------------------

                    Vector3 relAngVel = satRigidbody.angularVelocity - tarRigidbody.angularVelocity;

                    bool perfectCaptureAchieved =
                        contactingPads >= 3 &&
                        contactQualityReward > 0.8f &&
                        Mathf.Abs(currentDistance - targetHoverDistance) < 0.3f &&
                        alignmentDot > 0.95f &&
                        satelliteAngularSpeed < 0.3f &&
                        relativeSpeed < 0.15f &&
                        relAngVel.magnitude < 0.3f;

                    if (perfectCaptureAchieved)
                    {
                        successBonus += 20.0f;
                    }


                    // =========================================================================
                    // FINAL REWARD AGGREGATION
                    // =========================================================================
                    //
                    // The final reward is a weighted sum of all continuous components plus
                    // any achieved milestone bonuses. Weights have been carefully chosen to
                    // prioritize mission-critical behaviors while still providing gradients
                    // for secondary objectives.
                    //
                    // WEIGHT JUSTIFICATION SUMMARY:
                    //
                    // CRITICAL (2.0-3.0): Distance, Orientation, Approach Safety, Gripper Contact
                    // - These are the core mission requirements
                    // - Highest weights ensure they dominate the learning signal
                    // - Failure in these areas should significantly impact reward
                    //
                    // IMPORTANT (1.0-1.5): Velocity Matching, Stability, Multi-Point Contact
                    // - Necessary for mission success but secondary to critical components
                    /// - Provide refinement once basic behaviors are learned
                    /// - Still substantial contribution to total reward
                    ///
                    /// SECONDARY (0.3-0.8): Fuel Efficiency, Arm Positioning, Contact Quality,
                    ///                      Angular Velocity Matching, Target Motion Awareness
                    /// - Optimization and refinement objectives
                    /// - Lower weights prevent them from overwhelming critical learning
                    /// - Still provide useful gradients for policy improvement
                    ///
                    /// EXPECTED REWARD RANGES BY TRAINING STAGE:
                    ///
                    /// EARLY TRAINING: [-5, +5]
                    /// - Base continuous rewards: -2 to +8 (poor to mediocre performance)
                    /// - Bonuses: 0 (none achieved during random exploration)
                    /// - Dominated by catastrophic penalties and approach rewards
                    ///
                    /// MID TRAINING: [+3, +15]
                    /// - Base continuous rewards: +3 to +10 (decent performance)
                    /// - Bonuses: 0 to +5 (occasional station-keeping achievement)
                    /// - Consistent approach and hover, beginning alignment
                    ///
                    /// LATE TRAINING: [+8, +25]
                    /// - Base continuous rewards: +8 to +12 (good performance)
                    /// - Bonuses: +3 to +13 (station-keeping + contact bonuses)
                    /// - Reliable hover and initial grasping
                    ///
                    /// MASTERY: [+15, +45+]
                    /// - Base continuous rewards: +10 to +14 (excellent performance)
                    /// - Bonuses: +5 to +31 (multiple milestone bonuses per episode)
                    /// - Consistent mission success with high-quality captures
                    ///
                    /// =========================================================================

                    float totalReward = 0.0f;

                    // Critical components (highest priority)
                    totalReward += 2.5f * distanceReward;              // Weight: 2.5
                    totalReward += 2.0f * orientationReward;           // Weight: 2.0
                    totalReward += 2.5f * approachSafetyReward;        // Weight: 2.5
                    totalReward += 3.0f * gripperContactReward;        // Weight: 3.0

                    // Important components (secondary priority)
                    totalReward += 1.5f * velocityReward;              // Weight: 1.5
                    totalReward += 1.2f * stabilityReward;             // Weight: 1.2
                    totalReward += 1.0f * multiPointContactReward;     // Weight: 1.0

                    // Secondary components (refinement)
                    totalReward += 0.6f * armPositioningReward;        // Weight: 0.6
                    totalReward += 0.7f * contactQualityReward;        // Weight: 0.7
                    totalReward += 0.4f * fuelEfficiencyReward;        // Weight: 0.4
                    totalReward += 0.8f * angularVelocityMatchingReward; // Weight: 0.8
                    totalReward += 0.3f * targetMotionReward;          // Weight: 0.3

                    // Milestone bonuses (additive)
                    totalReward += successBonus;

                    // Maximum possible base reward (without bonuses): ~16.0
                    // Maximum possible bonus reward: +38.0 (all bonuses)
                    // Theoretical maximum single-step reward: ~54.0
                    // Typical mastery-level step reward: 12-18 (base) + occasional bonuses

                    return totalReward;
                }
            }
        }
    }

    /// <summary>
    /// Generic custom data structures
    /// </summary>
    public class CircularBuffer<T>
    {
        private readonly T[] buffer;
        private int index = 0;
        private bool filled = false;

        public CircularBuffer(int capacity)
        {
            buffer = new T[capacity];
        }

        public void Add(T item)
        {
            buffer[index] = item;
            index = (index + 1) % buffer.Length;
            if (index == 0) filled = true;
        }

        public int Count => filled ? buffer.Length : index;

        // Access newest: Get(0), previous: Get(1), etc.
        public T Get(int offsetFromNewest)
        {
            if (offsetFromNewest < 0 || offsetFromNewest >= Count)
                throw new ArgumentOutOfRangeException();

            int pos = (index - 1 - offsetFromNewest + buffer.Length) % buffer.Length;
            return buffer[pos];
        }
    }
}
