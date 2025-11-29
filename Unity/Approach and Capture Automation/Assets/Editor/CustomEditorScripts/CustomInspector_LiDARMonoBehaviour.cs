using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LiDARMonoBehaviour))]
public class CustomInspector_LiDARMonoBehaviour : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector first
        DrawDefaultInspector();

        // Header
        EditorGUILayout.LabelField("Debugging Actions", EditorStyles.boldLabel);

        // Create hitDistances dump button
        if (GUILayout.Button(new GUIContent("Generate hitDistances[] dump", "Generates and saves a CSV file containing the current LiDAR hit distances.")))
        {
            // Get a reference to the script instance
            LiDARMonoBehaviour lidarScript = (LiDARMonoBehaviour)target;

            // Validate native array
            if (lidarScript.nativeArrays == null || !lidarScript.nativeArrays.hitDistances.IsCreated)
            {
                Debug.LogError("LiDARMonoBehaviour nativeArrays.hitDistances is not created or is null. Cannot generate dump. Ensure the application is running and the LiDAR is initialized.");
                return;
            }

            // Finish the LiDAR scan job
            lidarScript.nativeArrays.lastJobHandle.Complete();

            // Dump hit distances into a CSV file in the machines temp directory
            StaticUtilities.WriteFloatArrayToTempCsv(
                values: lidarScript.nativeArrays.hitDistances.ToArray(),
                rowWidth: lidarScript.nativeArrays.rootOfArraySize,
                fileName: "LiDAR_hitDistances_array"
            );
        }
    }
}
