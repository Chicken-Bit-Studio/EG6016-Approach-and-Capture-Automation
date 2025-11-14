using System.Collections;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using static LiDARStatic;

/// <summary>
/// LiDAR MonoBehaviour class. Also see LiDARStatic.
/// </summary>
public class LiDARMonoBehaviour : MonoBehaviour
{
    [System.Serializable]
    public class SensorParameters
    {
        [Header("LiDAR Initialisation Settings")]
        [Tooltip("Transform of the LiDAR emitter. Rays are cast in this transform's +y direction.")]
        public Transform emitter;
        [HideInInspector]
        public quaternion inverseRotation = new();
        [Tooltip("Should the LiDAR raycasts ignore colliders on parent objects of the emitter?")]
        public bool ignoreParentColliders = true;
        [HideInInspector] public QueryParameters queryParameters;

        [Header("Scanning Settings")]
        [TextArea]
        public readonly string note = "The ML-friendly settings are 30deg at 1ray-per-deg due to observations feed limits.";
        [Tooltip("The sensor's field of view in degrees.")]
        public LiDARParameters.FOV fieldOfView = LiDARParameters.FOV._60deg;
        [Tooltip("The number of rays to cast per degree in the sensor's field of view.")]
        public LiDARParameters.RayDensity raysPerDegree = LiDARParameters.RayDensity._4;
        [Tooltip("Maximum distance for each LiDAR raycast.")]
        [Range(0.5f, 50f)]
        public float maxDistance = 20f;

        // Track the Inspector parameters for changes over time
        private LiDARParameters.FOV lastFieldOfView;
        private LiDARParameters.RayDensity lastRaysPerDegree;
        public bool HasChanged()
        {
            // Note: We don't need to poll maxDistance here, as it is only read into buildRaycastCommandsJob and doesn't affect raycast command input native array dimensions or values.
            bool changed = lastFieldOfView != fieldOfView ||
                lastRaysPerDegree != raysPerDegree;
            if (changed)
            {
                lastFieldOfView = fieldOfView;
                lastRaysPerDegree = raysPerDegree;
            }
            return changed;
        }
        public SensorParameters()
        {
            // Write to the tracking variables during class initiation
            HasChanged();
        }
    }

    [System.Serializable]
    public class ImageParameters
    {
        [Header("Image Generation Settings")]
        [Tooltip("Should the LiDAR data be used to generate an image on-screen?")]
        public bool generateLiDARImage = true;
        [Tooltip("The UnityEngine.UI.Image component the generated image feeds to.")]
        public RawImage lidarUIImage;
        [Tooltip("The colour mapping curve used to translate between distance and pixel colour.")]
        public LiDARImageGeneration.MappingCurve mappingCurve = LiDARImageGeneration.MappingCurve.Exponential;
        [Tooltip("A variable used in the colour curve calculations.")]
        [Range(0.05f, 8)]
        public float a = 0.4f;
        [Tooltip("The maximum resolution of the output image. If the LiDAR point cloud data array has a smaller size than this value the resulting image will be smaller.")]
        public LiDARImageGeneration.ImageResolution maxResolution = LiDARImageGeneration.ImageResolution.Size512x512;
        [Tooltip("The refresh rate of the LiDAR image in Hz.")]
        [Range(1f, 30f)]
        public float maxRefreshRate = 8f;
        [HideInInspector]
        public float imageRefreshPeriod = 1 / 8f;
        [HideInInspector]
        public Texture2D lidarTexture;
        [HideInInspector]
        public int lidarTexturePixelCount;
        [HideInInspector]
        public int[] lidarTextureMappedIndexes;
        [HideInInspector]
        public byte[] lidarTextureByteBuffer;   // TextureFormat.R16 uses two bytes per pixel.

        // Track the Inspector parameters for changes over time
        private LiDARImageGeneration.ImageResolution lastMaxResolution;
        private float lastImageRefreshRate;
        public bool HasChanged()
        {
            bool changed = maxResolution != lastMaxResolution || lastImageRefreshRate != maxRefreshRate;
            if (changed)
            {
                lastMaxResolution = maxResolution;
                lastImageRefreshRate = maxRefreshRate;
                imageRefreshPeriod = 1 / maxRefreshRate;
            }
            return changed;
        }
        public ImageParameters()
        {
            // Write to the tracking variables during class initiation
            HasChanged();
        }
    }

    [System.Serializable]
    public class DebuggingSettings
    {
        [Header("Debugging Tools")]
        [Tooltip("Draw every 283rd ray for debugging purposes.")]
        public bool drawRays = false;
    }

    public class NativeArrays
    {
        // Native arrays are built for highly efficient job allocation but are unmanaged.
        public int rootOfArraySize, totalRayCount, idealBatchSize;
        public NativeArray<float3> localspaceDirs;
        public NativeArray<float3> worldspaceDirs;
        public NativeArray<RaycastCommand> raycastCommands;
        public NativeArray<RaycastHit> raycastHits;
        public NativeArray<float> hitDistances;
        public NativeArray<float> hitDistances_forML;
        //public NativeArray<float3> hitPointsInLocalSpace;
        public JobHandle lastJobHandle;

        public void DisposeAll()
        {
            // Unity throws a fit if you try to dispose of native arrays if there are currently-running jobs that depend on them.
            // Note: Unity actually creates duplicate scene objects and their attached classes  in the edit-runtime lifecycle, and so DisposeAll is actually
            //  called multiple times on application exit, but only the instance used in runtime is populatd, and so memory leaks don't happen here.
            lastJobHandle.Complete();
            if (localspaceDirs.IsCreated) localspaceDirs.Dispose();
            if (worldspaceDirs.IsCreated) worldspaceDirs.Dispose();
            if (raycastCommands.IsCreated) raycastCommands.Dispose();
            if (raycastHits.IsCreated) raycastHits.Dispose();
            if (hitDistances.IsCreated) hitDistances.Dispose();
            if (hitDistances_forML.IsCreated) hitDistances_forML.Dispose();
            //if (hitPointsInLocalSpace.IsCreated) hitPointsInLocalSpace.Dispose();
        }
        public NativeArrays()
        {
            Application.quitting += DisposeAll;
        }
        public int CountRayHits()
        {
            // For debugging. Slow.
            if (!raycastHits.IsCreated) { return -1; }
            int count = 0;
            for (int i = 0; i < totalRayCount; i++)
            {
                if (raycastHits[i].collider != null) { count++; }
            }
            return count;
        }
    }

    public SensorParameters sensorParameters = new();
    public ImageParameters imageParameters = new();
    public DebuggingSettings debuggingSettings = new();
    public NativeArrays nativeArrays = new();
    public Coroutine liDARScanCoroutine;

    void Start()
    {
        // Catch undeclared variables
        if (sensorParameters.emitter == null) Debug.LogError($"{this.name}: Emitter transform not assigned.");
        // Instigate the correct raycast QueryParameters for the given SensorParameters initialisation settings
        sensorParameters.queryParameters = new QueryParameters
        {
            layerMask = ~((1 << LayerMask.NameToLayer("Ignore Raycast")) | (1 << LayerMask.NameToLayer("Gripper Arm Elements"))),
            hitBackfaces = false
        };
        // Start LiDAR scanning coroutine
        liDARScanCoroutine = StartCoroutine(PerformLiDARScan());
    }

    void Update()
    {
        // Check for parameter changes
        if (sensorParameters.HasChanged())
        {
            // Parameters have changed, so restart the LiDAR scan coroutine and dispose of unmanaged native arrays and dependants.
            if (liDARScanCoroutine != null) StopCoroutine(liDARScanCoroutine);
            nativeArrays.DisposeAll();
            imageParameters.lidarTexture = null;
            liDARScanCoroutine = StartCoroutine(PerformLiDARScan());
        }
        if (imageParameters.HasChanged())
        {
            // Parameters have changed, so clear lidarTexture. This makes UpdateLiDARImage call RegenerateLiDARImage.
            imageParameters.lidarTexture = null;
        }
    }

    private IEnumerator PerformLiDARScan()
    {
        // Ensure the LiDAR image isn't updating too rapidly
        float secondsSinceImageUpdate = 0f;

        // Note: Almost all logic and data management has been optimised and offloaded to a static script. See LiDARStatic.
        while (true)
        {
            // Create, await, and the net raycasting job
            JobHandle jh = LiDARRuntimeJobs.ScheduleAndRunLiDARRaycasts(monoBehaviourInstance: this);
            while (!jh.IsCompleted) { yield return null; }
            jh.Complete();

            // Proceed to use the raycast hit data at nativeArrays.raycastHits
            if (imageParameters.generateLiDARImage)
            {
                if (imageParameters.lidarUIImage == null)
                {
                    Debug.LogWarning("LiDAR tried to generate an image, but no target texture has been set.");
                    imageParameters.generateLiDARImage = false;
                    continue;
                }
                secondsSinceImageUpdate += Time.deltaTime;
                if (secondsSinceImageUpdate >= imageParameters.imageRefreshPeriod)
                {
                    LiDARImageGeneration.UpdateLiDARImage(monoBehaviourInstance: this);
                    secondsSinceImageUpdate = 0;
                }
            }
        }
    }

    public (bool, float[]) GetMLObservation()
    {   
        nativeArrays.lastJobHandle.Complete();
        return (true, nativeArrays.hitDistances_forML.ToArray());
    }
}
