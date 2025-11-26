using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor.VersionControl;
using UnityEngine;
using static RoboticsDataClasses;

/// <summary>
/// The EnvironmentController acts as the interface between Unity (the simulator)
/// and Python (the learning agent). It allows physics to be stepped manually,
/// actions to be applied, and observations/rewards to be collected.
/// The next steps for this script would be to seperate the universal processes here from the ApproachAndCapture project. Perhaps each project culd have its own interface script, similar to the currently-depreciated CoLESLaWInterface.cs.
/// </summary>
public class EnvironmentController : MonoBehaviour
{
    public References references = new();
    public EpisodeSettings episodeSettings = new();
    public EpisodeRandomisation episodeRandomisation = new();
    [HideInInspector] public PhysicsControl physicsControl = new();

    [Serializable]
    public class References
    {
        [Header("Active Character")]
        [Tooltip("The CoLESLaW-01 prefab in the scene.")]
        public GameObject satelliteGameObjectInScene;
        public GameObject satelliteGameObjectPrefab;
        [ReadOnly] public Rigidbody satelliteRigidbody;
        [ReadOnly] public IModel satelliteModelInterface;

        [Header("Target Object")]
        [Tooltip("The object being captured.")]
        public GameObject targetGameObjectInScene;
        public GameObject targetGameObjectPrefab;
        [ReadOnly] public Rigidbody targetRigidbody;
        [ReadOnly] public Collider targetCollider;

        // Manual start procedure. Cannot be called inside a constructor because Unity objects are not yet initialised then.
        public void Start_Manual()
        {
            // Collect runtime references for the first time
            // Note: This is immediately made redundant by the randomisation procedure called later in EnvironmentController.Start(),
            //  but it's useful for catching errors early and reporting the status of CoLESLaW's Model interface.
            RefreshRuntimeReferences();
            // Cache the first sate, non-randomised transform data of both objects
            CacheUnadulteratedTransformData();
            // Report initial findings to console
            Debug.Log($"IO registration complete for {satelliteGameObjectInScene.name}:\n" +
                      $"  Sensors detected: {satelliteModelInterface.sensors.sensorCount}\n" +
                      $"  Plants detected:  {satelliteModelInterface.plants.plantCount}");
        }

        // Refreshes the runtime references to rigidbodies, collider(s) and the IModel interface after the satellite and target prefabs have been reinstantiated in the scene.
        public void RefreshRuntimeReferences()
        {
            // Generate the satellite's IModel interface
            satelliteModelInterface = new IModel(satelliteGameObjectInScene);
            // Cache the rigidbody references
            if (satelliteGameObjectInScene == null || targetGameObjectInScene == null) { throw new NullReferenceException("Assign satellite and target gameobjects first!"); }
            satelliteRigidbody = satelliteGameObjectInScene.GetComponent<Rigidbody>();
            targetRigidbody = targetGameObjectInScene.GetComponent<Rigidbody>();
            targetCollider = targetGameObjectInScene.GetComponent<Collider>();
        }

        // Cached initial transform data
        [HideInInspector] public Vector3 satelliteStartingPosition;
        [HideInInspector] public Quaternion satelliteStartingRotation;
        [HideInInspector] public Vector3 targetStartingPosition;
        [HideInInspector] public Quaternion targetStartingRotation;
        public void CacheUnadulteratedTransformData()
        {
            satelliteStartingPosition = satelliteGameObjectInScene.transform.position;
            satelliteStartingRotation = satelliteGameObjectInScene.transform.rotation;
            targetStartingPosition = targetGameObjectInScene.transform.position;
            targetStartingRotation = targetGameObjectInScene.transform.rotation;
        }


    }
    [Serializable]
    public class EpisodeSettings
    {
        [Header("Episode Management")]
        [Tooltip("Seconds before episode timeout.")]
        public float maxEpisodeTime = 40f;
        [Tooltip("Time elapsed this episode.")]
        [ReadOnly] public float elapsedTime = 0f;
        [Tooltip("Steps taken this episode.")]
        [ReadOnly] public int stepsThisEpisode = 0;
        [Tooltip("Frames elapsed this episode.")]
        [ReadOnly] public int framesThisEpisode = 0;

        [Header("Episode State Tracking")]
        [ReadOnly] public float episodeReward = 0f;
        [ReadOnly] public bool reportOnEpisodeEnd = false;
        [HideInInspector] public bool episodeDone = false;
    }
    [Serializable]
    public class EpisodeRandomisation
    {
        [Tooltip("The maximum positional offset of the scene objects from their starting points after environment reset.")]
        public float maximumPositionalOffset = 1.5f;
        public float maximumStartingSpeed = 0.5f;
        [Tooltip("The maximum angular offset of the scene objects from their starting orientations after environment reset in degrees.")]
        public float maximumAngularOffset = 20f;
        public float maximumAngularSpeed = 15f;

        // Randomisation procedure
        public void RandomiseForEpisodeStart(References referencesClassInstance)
        {
            // Naming shunt
            References references = referencesClassInstance;

            // Destoy and recreate the satellite from prefab to ensure clean state
            DestroyImmediate(references.satelliteGameObjectInScene);
            references.satelliteGameObjectInScene = Instantiate(
                original: references.satelliteGameObjectPrefab,
                position: references.satelliteStartingPosition,
                rotation: references.satelliteStartingRotation);
            // Do the same for the target object
            DestroyImmediate(references.targetGameObjectInScene);
            references.targetGameObjectInScene = Instantiate(
                original: references.targetGameObjectPrefab,
                position: references.targetStartingPosition,
                rotation: references.targetStartingRotation);
            // Refresh runtime references to rigidbodies, colliders and IModel interface
            references.RefreshRuntimeReferences();

            // Reset the satellite
            RandomiseObjectVelocity(references.satelliteRigidbody);
            RandomiseObjectAngularVelocity(references.satelliteRigidbody);
            RandomiseObjectPosition(references.satelliteGameObjectInScene.transform);
            RandomiseObjectRotation(references.satelliteGameObjectInScene.transform, constained: true);
            //Reset the target
            RandomiseObjectAngularVelocity(references.targetRigidbody);
            RandomiseObjectRotation(references.targetGameObjectInScene.transform, constained: false);

            // Local randomisation methods
            void RandomiseObjectPosition(Transform objTransform)
            {
                objTransform.position = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0, maximumPositionalOffset) + objTransform.position;
            }
            void RandomiseObjectVelocity(Rigidbody rigidbody)
            {
                rigidbody.velocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0, maximumStartingSpeed);
            }
            void RandomiseObjectRotation(Transform objTransform, bool constained)
            {
                Vector3 randomAxis = UnityEngine.Random.onUnitSphere;
                float randomAngle = constained ?
                    UnityEngine.Random.Range(0, maximumAngularOffset) :
                    UnityEngine.Random.Range(-180f, 180f);
                Quaternion headingOffset = Quaternion.AngleAxis(randomAngle, randomAxis);
                objTransform.rotation = headingOffset * objTransform.rotation;
            }
            void RandomiseObjectAngularVelocity(Rigidbody rigidbody)
            {
                Vector3 spinAxis = UnityEngine.Random.onUnitSphere;
                float spinSpeed = UnityEngine.Random.Range(0, maximumAngularSpeed);
                rigidbody.angularVelocity = Mathf.Deg2Rad * spinSpeed * spinAxis;
            }
        }
    }
    public class PhysicsControl
    {
        // See: CustomInspector_EnvironmentController
        // This boolean is not supposed to change during runtime.
        public const bool useFixedUpdateOnStart = false;
        // The simulated time between each step.
        public const float PHYSICS_TIMESTEP = 0.02f; // 50Hz
        public const float PHYSICS_SETTLE_TIME = 0.001f;
        private List<IPhysicsSteppable> physicsObjects;

        public void Awake_Manual()
        {
            // Find all objects implementing IPhysicsSteppable in the scene for the first time
            CollectPhysicsObjectsInScene();
            Debug.Log($"Found {physicsObjects.Count} physics-steppable objects at the start of the scene.");
            // Turn off automatic physics if configured to do so on startup
            Physics.simulationMode = useFixedUpdateOnStart ? SimulationMode.FixedUpdate : SimulationMode.Script;
        }
        public void FixedUpdate_Manual()
        {
            if (Physics.simulationMode == SimulationMode.FixedUpdate) { StepPhysics(Time.fixedDeltaTime); }
        }
        public void CollectPhysicsObjectsInScene()
        {
            // Find all objects implementing IPhysicsSteppable in the scene
            physicsObjects = new List<IPhysicsSteppable>(FindObjectsOfType<MonoBehaviour>().OfType<IPhysicsSteppable>());
        }
        public void StepPhysics(float physicsDeltaTime = PHYSICS_TIMESTEP)
        {
            // Step each registered physics object
            foreach (var obj in physicsObjects) { obj.PhysicsStep(physicsDeltaTime); }
            if (Physics.simulationMode == SimulationMode.Script) { Physics.Simulate(physicsDeltaTime); }
        }
        public void DoPhysicsInFixedUpdate(bool useFixedUpdateLoopNow)
        {
            SimulationMode newSimulationMode = useFixedUpdateLoopNow ? SimulationMode.FixedUpdate : SimulationMode.Script;
            Physics.simulationMode = newSimulationMode;
        }
    }

    void Start()
    {
        // Initialise references
        references.Start_Manual();  // 6-10ms
        // Collect physics-steppable objects in the scene and turn off automatic physics if configured to do so on startup
        physicsControl.Awake_Manual();
        // Reset the scene once at start. Skip settling as to avoid calling Physics.StepPhysics before other scripts' Start() methods have run.
        ResetEnvironment(skipSettle: true);
    }

    void FixedUpdate()
    {
        // Let the PhysicsControl handle FixedUpdate physics stepping if enabled
        physicsControl.FixedUpdate_Manual();
    }

    void Update()
    {
        // Increment framesThisEpisode
        episodeSettings.framesThisEpisode += 1;
    }

    /// <summary>
    /// Resets the environment to a new random initial state.
    /// This is called at the beginning of each episode.
    /// This is preferred over reloading the Unity scene because it is quicker and foregoes the startup mumbo jumbo.
    /// </summary>
    public void ResetEnvironment(bool skipSettle = false)
    {
        // Wiping the episode tracking values
        episodeSettings.episodeDone = false;
        episodeSettings.episodeReward = 0f;
        episodeSettings.elapsedTime = 0f;
        episodeSettings.stepsThisEpisode = 0;
        episodeSettings.framesThisEpisode = 0;
        // Randomise the episode starting conditions
        episodeRandomisation.RandomiseForEpisodeStart(referencesClassInstance: references);
        // Recollect physics-steppable objects as they were destroyed/reinstantiated during randomisation
        physicsControl.CollectPhysicsObjectsInScene();
        // Force physics to sync with transform changes
        Physics.SyncTransforms();
        // Tiny micro-step to settle physics
        if (!skipSettle) { physicsControl.StepPhysics(PhysicsControl.PHYSICS_SETTLE_TIME); }
    }

    /// <summary>
    /// Steps the simulation forward by one physics frame.
    /// Called externally (later by Python) with an action vector.
    /// </summary>
    /// <param name="action">Float array of thruster/torque commands.</param>
    /// <returns>A tuple of (observation array, reward, done).</returns>
    public (float[], float, bool) Step(float[] action, bool isDebugStep = false)
    {
        // If the episode has finished, return a shortedned package early
        if (episodeSettings.episodeDone) { return (new float[1], 0f, true); }   // TODO: use calculated length
        // Increment the episode clock by one unit of deltaTime
        episodeSettings.elapsedTime += PhysicsControl.PHYSICS_TIMESTEP;
        // Apply actions to satellite
        ApplyAction(action, isDebugStep);
        // Advance physics by one 'step' (time-domain)
        physicsControl.StepPhysics(PhysicsControl.PHYSICS_TIMESTEP);
        // Compute and increment reward
        float reward = ComputeReward();
        episodeSettings.episodeReward += reward;
        // Check for episode termination criteria
        if (CheckDone()) { episodeSettings.episodeDone = true; }
        // Collect observations (time and report if debugging)
        float[] obs;
        if (isDebugStep)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            obs = CollectObservations();
            stopwatch.Stop();
            Debug.Log($"Manual call of CollectObservations took: {StaticUtilities.FormatStopwatchDuration(stopwatch)}");
        }
        else { obs = CollectObservations(); }
        // Increment stepsThisEpisode
        episodeSettings.stepsThisEpisode += 1;
        // Report back with current observations and rewards - return the full RL step package
        return (obs, reward, episodeSettings.episodeDone);
    }

    /// <summary>
    /// Simple action mapping: expects 6 floats [Fx, Fy, Fz, Tx, Ty, Tz]
    /// representing forces and torques in local body coordinates.
    /// </summary>
    private void ApplyAction(float[] action, bool debuggingMode = false)
    {
        // Create and apply the provided actions array with the struct found in RoboticsDataClasses.ReinforcementLearning.ApproachAndCaptureProject.Actions
        new ReinforcementLearning.ApproachAndCaptureProject.Actions(
            modelInterface: references.satelliteModelInterface,
            receivedFloats: action
        ).AffectModel(debuggingMode: debuggingMode);
    }

    /// <summary>
    /// Collects the current observation vector for the agent.
    /// </summary>
    private float[] CollectObservations()
    {
        // TODO: track this workflow back - it's not the most efficient wrt .ToArray() calls.
        return new ReinforcementLearning.ApproachAndCaptureProject.Observations(
            modelInterface: references.satelliteModelInterface,
            satellite: references.satelliteGameObjectInScene.transform,
            satelliteRb: references.satelliteRigidbody,
            target: references.targetGameObjectInScene.transform,
            targetRb: references.targetRigidbody
        ).SendToFloatArray();
    }

    /// <summary>
    /// Computes a simple reward function for now:
    /// negative distance to target + small penalty for speed.
    /// TODO: Further logic to be implemented.
    /// </summary>
    private float ComputeReward()
    {
        return new ReinforcementLearning.ApproachAndCaptureProject.Rewards(
            satTransform: references.satelliteGameObjectInScene.transform,
            satRigidbody: references.satelliteRigidbody,
            tarTransform: references.targetGameObjectInScene.transform,
            tarRigidbody: references.targetRigidbody
        ).CalculateReward();
    }

    /// <summary>
    /// Determines if the episode should end.
    /// </summary>
    private bool CheckDone()
    {
        float dist = Vector3.Distance(references.satelliteRigidbody.position, references.targetRigidbody.position);

        // Fail if time limit reached
        if (episodeSettings.elapsedTime >= episodeSettings.maxEpisodeTime)
        {
            if (episodeSettings.reportOnEpisodeEnd) { ReportReasonString($"Time limit reached ({episodeSettings.maxEpisodeTime:F2}s)"); }
            return true;
        }

        // Fail if satellite drifts too far
        if (dist > 30f)
        {
            if (episodeSettings.reportOnEpisodeEnd) { ReportReasonString($"Satellite drifted too far (dist={dist:F2}m)"); }
            return true;
        }

        /*/ Success if close enough
        if (dist < 0.1f)
            return true;*/

        // Otherwise, not done
        return false;

        void ReportReasonString(string reason)
        {
            Debug.Log($"Episode done: {reason}. " +
                $"Time elapsed: {episodeSettings.elapsedTime:F2}s. " +
                $"Frames elapsed: {episodeSettings.framesThisEpisode}. " +
                $"Steps taken: {episodeSettings.stepsThisEpisode}.");
        }
    }

    /// <summary>
    /// Returns the sizes of the observation and action arrays for the initial server handshake.
    /// See EnvironmentSocketServer for usage.
    /// </summary>
    public (int, int) GetObservationAndActionArraySizes()
    {
        // TODO: Review this for efficiency later
        int obsSize = CollectObservations().Length;
        int actSize = ReinforcementLearning.ApproachAndCaptureProject.Actions.FEED_SIZE;
        return (obsSize, actSize);
    }

    /// <summary>
    /// Called by a custom button in the Inspector window. See CustomInspector_EnvironmentController for more details.
    /// </summary>
    public void ManualStep()
    {
        float actuators = 1f;
        float thrusters = 0f;
        float[] manualActionArr = new float[references.satelliteModelInterface.plants.plantCount];
        for (int i = 0; i < manualActionArr.Length - 1; i++) { manualActionArr[i] = (i < references.satelliteModelInterface.plants.actuators.actuatorCount) ? actuators : thrusters; }
        var result = this.Step(manualActionArr, true);
        Debug.Log($"Step: reward={result.Item2:F3}, done={result.Item3}");
    }
}

/// <summary>
/// Interface for objects that require manual physics stepping.
/// </summary>
public interface IPhysicsSteppable
{
    void PhysicsStep(float deltaTime);
}
