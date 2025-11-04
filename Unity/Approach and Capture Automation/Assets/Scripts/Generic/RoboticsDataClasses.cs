using System.Collections.Generic;
using UnityEngine;

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
    }

    /// <summary>
    /// A class containing input and output interfaces for the given model instance. Tailored to Unity.
    /// </summary>
    [System.Serializable]
    public class IModel
    {
        public GameObject model;
        public Sensors sensors;
        public Plants plants;

        public IModel(GameObject modelInScene)
        {
            // Assign the model's gameobject
            model = modelInScene;
            // Create the IO classes
            sensors = new();
            plants = new();
            // Poll ContainerClass instances in the scene and populate the IO classes
            RegisterIODevices(model);
        }
        [System.Serializable]
        public class Sensors
        {
            [Header("Model Input Device Resister")]
            [ReadOnly] public int lidarCount = 0;
            [HideInInspector] public Interfaces.Sensors.ILiDAR[] lidars;
        }
        [System.Serializable]
        public class Plants
        {
            [Header("Model Output Device Resister")]
            [ReadOnly] public int actuatorCount = 0;
            [HideInInspector] public Interfaces.Plants.IActuator[] actuators;
            [ReadOnly] public int thrusterCount = 0;
            [HideInInspector] public Interfaces.Plants.IThruster[] thrusters;
        }
        private void RegisterIODevices(GameObject model)
        {
            // Find all of the ContainerClass instances on the current gameobject
            // Model input classes (sensors)
            List<Interfaces.Sensors.ILiDAR> lList = new();
            // Model output classes (plants)
            List<Interfaces.Plants.IActuator> aList = new();
            List<Interfaces.Plants.IThruster> tList = new();
            foreach (ContainerClass cc in model.GetComponentsInChildren<ContainerClass>())
            {
                // Add the output classes we find to a temporary list. Ignore other classes.
                switch (cc.thing)
                {
                    // Model input classes (sensors)
                    case ModelElements.LiDAR l:                             // For each applicable object found in a container class:
                        lList.Add(new Interfaces.Sensors.ILiDAR(            // Create a new instance of the associated Interface class and add it to the associated list.
                            cc.GetComponent<LiDARMonoBehaviour>(),
                            l
                        ));
                        break;
                    // Model output classes (plants)
                    case ModelElements.Actuator a:
                        aList.Add(new Interfaces.Plants.IActuator(
                            cc.GetComponent<ActuatorMonoBehaviour>(),
                            a
                        ));
                        break;
                    case ModelElements.Thruster t:
                        tList.Add(new Interfaces.Plants.IThruster(
                            cc.GetComponent<ThrusterMonoBehaviour>(),
                            t
                        ));
                        break;
                }
            }
            // Re-cast the lists into pernament array objects and report their counts
            // Model input classes (sensors)
            sensors.lidars = lList.ToArray();
            sensors.lidarCount = sensors.lidars.Length;
            // Model output classes (plants)
            plants.actuators = aList.ToArray();
            plants.actuatorCount = plants.actuators.Length;
            plants.thrusters = tList.ToArray();
            plants.thrusterCount = plants.thrusters.Length;
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
        public static class Sensors
        {
            public class ILiDAR
            {
                public LiDARMonoBehaviour monoBehaviour;
                public ModelElements.LiDAR modelProfileObject;

                public ILiDAR(LiDARMonoBehaviour lidarMonoBehaviour, ModelElements.LiDAR lidarModelProfileObject = null)
                {
                    monoBehaviour = lidarMonoBehaviour;
                    modelProfileObject = lidarModelProfileObject;
                }
            }
        }
        /// <summary>
        /// Plants are the model's output devices. They include any outlet that changes the model's state. Thrusters, lights, speakers, angular and linear actuators are all considered 'plants' in this regime.
        /// </summary>
        public static class Plants
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
}
