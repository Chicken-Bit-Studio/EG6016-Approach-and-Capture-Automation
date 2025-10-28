using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs;
using Unity.Collections;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Class for collecting LiDAR data in a Unity scene.
/// </summary>
public class LiDARDataCollection : MonoBehaviour
{
    [Header("LiDAR Initialisation Settings")]
    [Tooltip("Transform of the LiDAR emitter. Rays are cast in this transform's +y direction.")]
    public Transform emitter;
    [Tooltip("Should the LiDAR raycasts ignore colliders on parent objects of the emitter?")]
    public bool ignoreParentColliders = true;
    [Tooltip("The minimum acceptable fps of the simulation. Used to advance frames in the PerformLiDARScan coroutine.")]
    [Range(8f, 60f)]
    public float minimumSimulationFPS = 20f;    //is it actually minimum fps or exact fps?

    [Header("LiDAR Scan Settings")]
    [Tooltip("Half of the vertical field of view in degrees.")]
    [Range(10f, 120f)]
    public float verticalHalfFOV = 35f;
    [Tooltip("The number of rays to cast per degree in the sensor's field of view.")]
    [Range(0.1f, 10f)]
    public float raysPerDegree = 5f;
    [Tooltip("Maximum distance for each LiDAR raycast.")]
    [Range(0.5f, 50f)]
    public float maxDistance = 20f;
    [ReadOnly]
    public float recalibrationProgress = 0f; // Value from 0 to 1 indicating the progress of the LiDAR setup phase

    [Header("LiDAR image generation Settings")]
    public bool generateLiDARImage = true;
    [Tooltip("Refresh rate of the displayed LiDAR image in Hz.")]// (seperate from scanFrequency).")]
    public float imageRefreshRate = 5f;
    public enum ImageSizeSettings { Size512x512, Size1024x1024 }
    [Tooltip("Resolution of the generated LiDAR image.")]
    public ImageSizeSettings imageSize = ImageSizeSettings.Size512x512;
    private Image uiImageComponent_CompleteScan;

    [Header("Debug Settings")]
    public bool visualizeRaysInSceneView = false;

    [HideInInspector]
    public float[,] pointArray;

    // Internal private variables
    private float timeOfPreviousImageUpdate = 0f;
    private float timeOfLatestImageUpdaten = 0f;
    private Coroutine lidarCoroutine;
    private NativeArray<RaycastCommand> raycastCommands;
    private NativeArray<RaycastHit> raycastHits;

    // Track the Inspector parameters for changes over time
    private float lastVerticalHalfFOV;
    private float lastRaysPerDegree;
    private float lastMaxDistance;
    private void GetLastVariables()
    {
        lastVerticalHalfFOV = verticalHalfFOV;
        lastRaysPerDegree = raysPerDegree;
        lastMaxDistance = maxDistance;
    }

    void Start()
    {
        // Catching undeclared variables
        if (emitter == null)
        {
            Debug.LogWarning("LiDAR emitter transform is not assigned. Defaulting to this GameObject's transform.");
            emitter = this.transform;
        }
        try
        {
            uiImageComponent_CompleteScan = GameObject.Find("LiDARImageDisplay_CompleteScan").GetComponent<Image>();
        }
        catch
        {
            Debug.LogError("UI Image component for LiDAR image display not found. Please ensure there is a gameobject named 'LiDARImageDisplay_CompleteScan' with an Image component in the scene.");
            generateLiDARImage = false;
        }
        // Initialize "last known" parameter values
        GetLastVariables();
    }

    void Update()
    {
        // Start the LiDAR scanning coroutine if not already running
        if (lidarCoroutine == null)
        {
            lidarCoroutine = StartCoroutine(PerformLiDARScan());
        }
        else
        {
            // If parameters have changed, restart the coroutine
            if (verticalHalfFOV != lastVerticalHalfFOV || raysPerDegree != lastRaysPerDegree || maxDistance != lastMaxDistance)
            {
                StopCoroutine(lidarCoroutine);
                raycastCommands.Dispose();
                raycastHits.Dispose();
                lidarCoroutine = StartCoroutine(PerformLiDARScan());
                GetLastVariables();
            }
        }
    }

    public IEnumerator PerformLiDARScan()
    {
        // SETUP PHASE - Ran when the coroutine starts or when parameters change
        // Note:    A generated point array will be square, so only one dimension is needed.
        //          Let 'i' be the horizontal index and 'j' be the vertical index.
        int pointArraySize = Mathf.CeilToInt(verticalHalfFOV * 2 * raysPerDegree);
        pointArray = new float[pointArraySize, pointArraySize];
        int totalRays = pointArraySize * pointArraySize;
        raycastCommands = new NativeArray<RaycastCommand>(totalRays, Allocator.Persistent);
        raycastHits = new NativeArray<RaycastHit>(totalRays, Allocator.Persistent);
        // Time management variables
        float frameTimeLimit = 1f / minimumSimulationFPS;
        float timeElapsedThisFrame = 0f;
        bool clockYielded = false;
        // Ray directions won't change between scans unless LiDAR parameters do, so precompute them once
        Vector3[,] rayDirections = new Vector3[pointArraySize, pointArraySize];
        yield return PrecomputeRayDirections();

        // LOOP PHASE - Main scanning loop
        while (true)
        {
            // Allocate a new batch of raycast commands
            JobHandle rayJobHandle = ScheduleRaycastJob();
            // Wait for the raycasting job to complete
            while (!rayJobHandle.IsCompleted)
            {
                yield return null;
            }
            // Finalize the job
            rayJobHandle.Complete();
            // Retrieve the results and assign distances to the point array
            for (int i = 0; i < totalRays; i++)
            {
                int row = i / pointArraySize;
                int col = i % pointArraySize;
                if (raycastHits[i].collider != null)
                {
                    pointArray[row, col] = raycastHits[i].distance;
                }
                else
                {
                    pointArray[row, col] = float.PositiveInfinity;
                }
            }
            // The job is done, so we update the scan image
            if (generateLiDARImage)
            {
                UpdateLiDARImageIfDue(pointArray);
            }
        }
    
        IEnumerator PrecomputeRayDirections()
        {
            // FOV/ray-density values
            int index = 0;
            float startDeg = -verticalHalfFOV;
            float stepDeg = 1f / raysPerDegree;
            for (int i = 0; i < pointArraySize; i++)
            {
                // Precompute vertical angle
                float verticalAngle_Deg = startDeg + (i * stepDeg);
                for (int j = 0; j < pointArraySize; j++)
                {
                    // Precompute horizontal angle
                    float horizontalAngle_Deg = startDeg + (j * stepDeg);
                    Quaternion rotation = Quaternion.Euler(verticalAngle_Deg, 0, horizontalAngle_Deg);
                    rayDirections[i, j] = rotation * emitter.up;    // Note: !! This might rely on emitter.up being constant during the simulation
                    index++;
                    // Yield control if frame time limit is reached during setup
                    yield return YieldIfFrameTimeExceeded();
                    if (clockYielded)
                    {
                        recalibrationProgress = ((float)index) / totalRays;
                    }
                }
            }
            // Debugging
            int count = pointArraySize * pointArraySize;
            long approxBytes = count * 12L;
            float approxMB = approxBytes / (1024f * 1024f);
            Debug.LogWarning($"Direction array ~ {approxMB:F2} MB");
        }
        
        JobHandle ScheduleRaycastJob()
        {
            // Setup the raycast commands for all rays
            int index = 0;
            QueryParameters qp = QueryParameters.Default;
            for (int i = 0; i < totalRays; i++)
            {
                raycastCommands[index++] = new RaycastCommand(
                    from: emitter.position,
                    direction: rayDirections[i / pointArraySize, i % pointArraySize],
                    queryParameters: qp,
                    distance: maxDistance);
            }
            // Schedule the raycast job
            return RaycastCommand.ScheduleBatch(
                commands: raycastCommands,
                results: raycastHits,
                minCommandsPerJob: 1);
        }
        
        IEnumerator YieldIfFrameTimeExceeded()
        {
            // Time management helper subroutine
            timeElapsedThisFrame += Time.deltaTime;
            if (timeElapsedThisFrame >= frameTimeLimit)
            {
                timeElapsedThisFrame = 0f;
                clockYielded = true;
                yield return null; // Pause the coroutine until the next frame
            }
            else
            {
                clockYielded = false;
            }
        }
    }

    // TODO: Try to make this a coroutine to avoid frame drops
    public void UpdateLiDARImageIfDue(float[,] pointArrayFrame)
    {
        float currentTime = Time.time;
        float timeSinceLastImageUpdate = currentTime - timeOfLatestImageUpdaten;
        if (timeSinceLastImageUpdate < 1f / imageRefreshRate)
        {
            return; // Not enough time has passed since the last update
        }
        else
        {
            // Generate the LiDAR image from the completed point array
            Texture2D image = LiDARImageGeneration.GenerateLiDARImage(pointArrayFrame, imageSize, maxDistance);
            uiImageComponent_CompleteScan.sprite = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(0.5f, 0.5f));
            timeOfPreviousImageUpdate = timeOfLatestImageUpdaten;
            timeOfLatestImageUpdaten = currentTime;
            //Debug.Log("LiDAR image update interval: " + (timeSinceLastImageUpdate / 1000) + "ms");
        }
    }
}
