using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnvironmentController))]
public class CustomInspector_EnvironmentController : Editor
{
    // Track the toggle value
    private bool toggleState;
    private bool initialStateRead = false;

    public override void OnInspectorGUI()
    {
        // Draw the default inspector first
        DrawDefaultInspector();

        // Get a reference to the script instance
        EnvironmentController script = (EnvironmentController)target;

        // Header
        EditorGUILayout.LabelField("Debugging Actions", EditorStyles.boldLabel);

        // Sync the toggle status
        if (!initialStateRead)
        {
            toggleState = script.debugging.useFixedUpdateOnStart;
            initialStateRead = true;
        }
        // Create a toggle in the Inspector
        bool newToggleState = GUILayout.Toggle(toggleState, "Use FixedUpdate Loop");
        // If the toggle changed, call the relevant method in the script and update the boolean.
        if (newToggleState != toggleState)
        {
            script.debugging.DoPhysicsInFixedUpdate(newToggleState);
            toggleState = newToggleState;
        }

        // Create a manual step button in the Inspector, but disable it when the 'use fixed update' toggle is set to 'true'.
        EditorGUI.BeginDisabledGroup(newToggleState);
        if (GUILayout.Button(new GUIContent("Manual Episode Step", "Orders the Step() method to be called manually and with meaningless action parameters.")))
        {
            script.ManualStep();
        }
        EditorGUI.EndDisabledGroup();

        // Reset episode button
        if (GUILayout.Button(new GUIContent("Manual Episode Reset", "Ends the current episode and instigates a new one.")))
        {
            script.ResetEnvironment();
        }
    }
}