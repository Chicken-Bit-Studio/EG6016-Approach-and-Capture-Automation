public static class RoboticsDataClasses
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


}
