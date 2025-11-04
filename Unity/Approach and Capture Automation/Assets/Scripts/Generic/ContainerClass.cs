using System;
using UnityEngine;
using static RoboticsDataClasses;
using System.Linq;

[ExecuteAlways]
public class ContainerClass : MonoBehaviour
{
    /*[Header("Use only one of the following:")]
    public ModelProfile modelProfile = null;
    public SegmentProfile segmentProfile = null;
    public AttachmentNode attachmentNode = null;
    public Thruster thruster = null;*/

    [Header("Object Overview")]
    [ReadOnly] public string thingType = "[unassigned]";

    [Header("Object Details")]
    public object thing = null;

    [Header("Type Override")]
    [Tooltip("Setting this to anything other than 'None' will overwrite this component's value with an empty instance of the corresponding class.")]
    public TypeOverrides typeOverride = TypeOverrides.None;
    public enum TypeOverrides { None, LiDAR, Actuator, Thruster };

    // A list of allowed types for the container class to receive
    private static readonly Type[] validTypes = {
        typeof(ModelElements.ModelProfile),
        typeof(ModelElements.SegmentProfile),
        typeof(ModelElements.AttachmentNode),
        typeof(ModelElements.Actuator),
        typeof(ModelElements.LiDAR),
        typeof(ModelElements.Thruster)
    };

    // Allow for empty class creation if an override has been set
    public void Start()
    {
        if (typeOverride != TypeOverrides.None)
        {
            switch (typeOverride)
            {
                case TypeOverrides.LiDAR:
                    thing = new ModelElements.LiDAR();
                    break;
                case TypeOverrides.Actuator:
                    thing = new ModelElements.Actuator();
                    break;
                case TypeOverrides.Thruster:
                    thing = new ModelElements.Thruster();
                    break;
                default:
                    Debug.LogError("Unrecognized MappingCurve enum value: " + Enum.GetName(typeof(TypeOverrides), typeOverride));
                    break;
            }
        }
    }

    public void SetValue(object input)
    {
        /*// Determine the class of object provided.
        switch (input)
        {
            case ModelElements.ModelProfile mp:
                //modelProfile = mp;
                thing = mp;
                break;
            case ModelElements.SegmentProfile sp:
                //segmentProfile = sp;
                thing = sp;
                break;
            case ModelElements.AttachmentNode an:
                //attachmentNode = an;
                thing = an;
                break;
            case ModelElements.Thruster tr:
                //thruster = tr;
                thing = tr;
                break;
            default:
                Debug.LogError($"Improper type passed to ContainerClass.SetValue: '{input.GetType().FullName}'");
                return;
        }
        // If the object is assigned without issue (did not hit default:return;), update the thing's type name in the Inspector
        thingType = input.GetType().FullName;*/

        // Check that the incoming object is of a valid type
        Type inputtedType = input.GetType();
        if (validTypes.Contains(inputtedType))
        {
            // Assign the new object and update the thing's type name in the Inspector
            thing = input;
            thingType = input.GetType().FullName;
        }
        else
        {
            // An incorrect type was passed. Report this to the console
            Debug.LogError($"Improper type passed to ContainerClass.SetValue: '{input.GetType().FullName}'");
        }
    }

    /*[ContextMenu("Write Object to Console")]
    private void WriteObjectToConsole()
    {
        string report = $"SP output from ContainerClass on {gameObject.name}:\n";
        bool isEmpty = segmentProfile == null;
        report += $"SegmentProfile is null: {isEmpty}";
        if (!isEmpty)
        {
            report += $"\n";
            report += $"Number of nodes: {segmentProfile.nodes.Length}\n";
            report += $"    Of node[0]:";
            report += $"        Has actuator: {segmentProfile.nodes[0].actuator != null}";
            report += $"        Has child: {segmentProfile.nodes[0].child != null}";
        }
        Debug.Log(report);

        // Running this currently writes "null" to console.
        JsonSerializerSettings settings = new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            MaxDepth = 6
        };
        Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(segmentProfile, Formatting.Indented, settings));
    }*/
}
