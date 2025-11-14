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
            Debug.Log($"IO registration complete for {model.name}:\n" +
                      $"  Sensors detected: {totalSensors}\n" +
                      $"  Plants detected:  {totalPlants}");
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
            // Observations and Actions are short-lived structs and should only be used in the same instance they are created in, for sake of the lidatHitDistances reference.
            public struct Observations
            {
                public float3 relativePosition;             // The relative vector from the satellite's origin to that of the target (satellite -> target)
                public float3 relativeVelocity;             // The relative velocity of the satellite's origin to that of the target (satellite -> target)
                public float3 targetDirection;              // A unit vector (in the satellite's local reference frame) pointing from the satellite's origin toward the target's origin (satellite -> target)
                public float3 angularVelocity_satellite;    // The current angular velocity of the satellite in its own local space (satellite only)
                public float3 angularVelocity_target;       // The current angular velocity of the target in its own local space (target only)
                public float[] actuatorData;                // A compiled array of returns the .GetMLObservation() method from each instance of ActuatorMonoBehavior in the model.
                public float[] gripperPadData;              // A compiled array of returns the .GetMLObservation() method from each instance of GripperPadMonoBehavior in the model.
                public float[] lidarData;                   // An array holding the APPROPRIATELY-SIZED LiDAR ray hit distances (satellite only after data collection)

                public const int MAX_LIDAR_SAMPLES = 256;   // Edit this to dynamically change the size of generated and fed LiDAR sample arrays
                public const int FEED_SIZE = MAX_LIDAR_SAMPLES + 15;

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
                    // Dynamic satellite Actuator data
                    List<float> tActuatorData = new();
                    foreach(Interfaces.IPlants.IActuator iface in modelInterface.plants.actuators.array)
                    {
                        // (bool isValidData, float[] values)
                        (bool, float[]) output = iface.monoBehaviour.GetMLObservation();
                        if (!output.Item1) { tActuatorData.AddRange(output.Item2); }
                    }
                    actuatorData = tActuatorData.ToArray();
                    // Dynamic satellite Gripper Pad data
                    List<float> tGripperPadData = new();
                    foreach(Interfaces.ISensors.IGripperPad iface in modelInterface.sensors.gripperPads.array)
                    {
                        // (bool isValidData, float[] values)
                        (bool, float[]) output = iface.monoBehaviour.GetMLObservation();
                        if (!output.Item1) { tGripperPadData.AddRange(output.Item2); } 
                    }
                    gripperPadData = tGripperPadData.ToArray();
                    // Dynamic satellite LiDAR data
                    lidarData = modelInterface.sensors.lidars.array[0].monoBehaviour.GetMLObservation().Item2;
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
            [Serializable]
            public struct Actions
            {
                public const int FEED_SIZE = 0;
                // To be implemented...
            }
        }
    }
}
