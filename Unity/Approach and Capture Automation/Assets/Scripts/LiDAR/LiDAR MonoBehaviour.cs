using System.Collections;
using Unity.Jobs;
using UnityEngine;
using static LiDARStatic;

/// <summary>
/// LiDAR MonoBehaviour class. Also see LiDARStatic.
/// </summary>
public class LiDARMonoBehaviour : MonoBehaviour
{
    [Header("LiDAR Initialisation Settings")]
    [Tooltip("Transform of the LiDAR emitter. Rays are cast in this transform's +y direction.")]
    public Transform emitter;
    [Tooltip("Should the LiDAR raycasts ignore colliders on parent objects of the emitter?")]
    public bool ignoreParentColliders = true;
    
    [Header("LiDAR Scan Settings")]
    [Tooltip("The sensor's field of view in degrees.")]
    public LiDARParameters.fov fieldOfView = LiDARParameters.fov._60deg;
    [Tooltip("The number of rays to cast per degree in the sensor's field of view.")]
    public LiDARParameters.rayDensity raysPerDegree = LiDARParameters.rayDensity._4;
    [Tooltip("Maximum distance for each LiDAR raycast.")]
    [Range(0.5f, 50f)]
    public float maxDistance = 20f;

    // The point cloud data from the most recent LiDAR scan
    public float[] pointCloudData { get; private set; }

    // Private variables related to task scheduling and tracking
    private Coroutine liDARScanCoroutine;
    
    // Track the Inspector parameters for changes over time
    private LiDARParameters.fov lastFieldOfView;
    private LiDARParameters.rayDensity lastRaysPerDegree;
    private float lastMaxDistance;
    private void GetLastVariables()
    {
        lastFieldOfView = fieldOfView;
        lastRaysPerDegree = raysPerDegree;
        lastMaxDistance = maxDistance;
    }

    void Start()
    {
        // Catch undeclared variables
        if (emitter == null) Debug.LogError("LiDARMonoBehaviour: Emitter transform not assigned.");
        // Initialize "last known" parameter values
        GetLastVariables();
        // Start LiDAR scanning coroutine
        liDARScanCoroutine = StartCoroutine(PerformLiDARScan());
    }

    void Update()
    {
        // Check for parameter changes
        if (fieldOfView != lastFieldOfView || raysPerDegree != lastRaysPerDegree || maxDistance != lastMaxDistance)
        {
            // Parameters have changed, so restart the LiDAR scan coroutine, dispose of unmanaged native arrays, and update last known values
            if (liDARScanCoroutine != null) StopCoroutine(liDARScanCoroutine);
            LiDARRuntimeJobs.NativeArrays.Dispose();
            GetLastVariables();
            liDARScanCoroutine = StartCoroutine(PerformLiDARScan());
        }
    }

    private IEnumerator PerformLiDARScan()
    {
        // Note: Almost all logic and data management has been optimised and offloaded to a static script. See LiDARStatic.
        while (true)
        {
            // Create, await, and the net raycasting job
            JobHandle jh = LiDARRuntimeJobs.ScheduleAndRunLiDARRaycasts(emitter, fieldOfView, raysPerDegree, maxDistance);
            while (!jh.IsCompleted) { yield return null; }
            jh.Complete();
            // Proceede to use the raycast hit data at LiDARStatic.NativeArrays.raycastHits
            LiDARImageGeneration.UpdateLiDARImage();
        }
    }
}
