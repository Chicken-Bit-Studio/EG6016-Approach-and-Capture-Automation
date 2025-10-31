using UnityEngine;
using Newtonsoft.Json;
using static RoboticsDataClasses;

public class ContainerClass : MonoBehaviour
{
    [Header("Use only one of the following:")]
    public ModelProfile modelProfile = null;
    public SegmentProfile segmentProfile = null;
    public AttachmentNode attachmentNode = null;
    public void SetValue(object input)
    {
        // Determine the class of object provided.
        switch (input)
        {
            case ModelProfile mp:
                modelProfile = mp;
                break;
            case SegmentProfile sp:
                segmentProfile = sp;
                break;
            case AttachmentNode an:
                attachmentNode = an;
                break;
            default:
                Debug.LogError($"Improper type passed to ContainerClass.SetValue: '{input.GetType().FullName}'");
                break;
        }
    }

    [ContextMenu("Write Object to Console")]
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
        /*
        // Running this currently writes "null" to console.
        JsonSerializerSettings settings = new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            MaxDepth = 6
        };
        Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(segmentProfile, Formatting.Indented, settings));
        */
    }
}
