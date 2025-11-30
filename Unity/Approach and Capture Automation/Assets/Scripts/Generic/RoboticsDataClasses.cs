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
                public float CalculateReward()
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
