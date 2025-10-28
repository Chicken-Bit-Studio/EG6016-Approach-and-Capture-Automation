using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public float minSimulationFPS = 20f;    //is it actually minimum fps or exact fps?

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
    public float scanProgress = 0f; // Value from 0 to 1 indicating the progress of the current scan

    [Header("LiDAR image generation Settings")]
    public bool generateLiDARImage = true;
    [Tooltip("Refresh rate of the displayed LiDAR image in Hz.")]// (seperate from scanFrequency).")]
    public float imageRefreshRate = 5f;
    public enum ImageSizeSettings { Size512x512, Size1024x1024}
    [Tooltip("Resolution of the generated LiDAR image.")]
    public ImageSizeSettings imageSize = ImageSizeSettings.Size512x512;
    private Image uiImageComponent_LiveScan;
    private Image uiImageComponent_CompleteScan;

    [Header("Debug Settings")]
    public bool visualizeRaysInSceneView = false;

    [HideInInspector]
    public float[,] pointArray;

    // Internal private variables
    private float timeSinceLastImageUpdate_LiveScan = 0f;
    private Coroutine lidarCoroutine;

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
            uiImageComponent_LiveScan = GameObject.Find("LiDARImageDisplay_LiveScan").GetComponent<Image>();
            uiImageComponent_CompleteScan = GameObject.Find("LiDARImageDisplay_CompleteScan").GetComponent<Image>();
        }
        catch
        {
            Debug.LogError("UI Image component for LiDAR image display not found. Please ensure there are GameObjects named 'LiDARImageDisplay_LiveScan' and 'LiDARImageDisplay_CompleteScan' with an Image component in the scene.");
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
                lidarCoroutine = StartCoroutine(PerformLiDARScan());
                GetLastVariables();
            }
        }
        // 5. (Optional) Display the point array as it currently is in an image in the game view
        timeSinceLastImageUpdate_LiveScan += Time.deltaTime;
        if (generateLiDARImage && timeSinceLastImageUpdate_LiveScan >= 1f / imageRefreshRate)
        {
            //StartCoroutine(UpdateLiDARImage());
            UpdateLiDARImage_LiveScan();
            timeSinceLastImageUpdate_LiveScan = 0f;
        }
    }

    public IEnumerator PerformLiDARScan()
    {
        // Constant definitions. If parameters are changed during runtime, this coroutine should be restarted.
        // Note: A generated point array will be square, so only one dimension is needed
        int pointArraySize = Mathf.CeilToInt(verticalHalfFOV * 2 * raysPerDegree);
        pointArray = new float[pointArraySize, pointArraySize];
        float startDeg = -verticalHalfFOV;
        float stepDeg = 1f / raysPerDegree;
        // Time management variables
        float frameTimeLimit = 1f / minSimulationFPS;
        float timeElapsedThisFrame = 0f;
        //LayerMask layerMask = ignoreParentColliders ? ~(1 << emitter.gameObject.layer) : ~0;

        // Loop the raycasting process indefinitely
        while (true)
        {
            // Let 'i' be the horizontal index and 'j' be the vertical index
            for (int i = 0; i < pointArraySize; i++)
            {
                // 1a. Calculate vertical angle
                float verticalAngle_Deg = startDeg + (i * stepDeg);

                for (int j = 0; j < pointArraySize; j++)
                {
                    // 1b. Calculate horizontal angle
                    float horizontalAngle_Deg = startDeg + (j * stepDeg);

                    // 2. Create a rotation from the emitter's orientation
                    Quaternion rotation = Quaternion.Euler(verticalAngle_Deg, 0, horizontalAngle_Deg);

                    // 3. Cast the ray and visualize if needed
                    Vector3 rayDirection = rotation * emitter.up;
                    Ray ray = new Ray(emitter.position, rayDirection);
                    if (visualizeRaysInSceneView)
                    {
                        Debug.DrawRay(emitter.position, rayDirection * maxDistance, Color.green, 1f / minSimulationFPS);
                    }

                    // 4. Record the distance to the first hit object or max distance
                    if (Physics.Raycast(ray, out RaycastHit hitInfo, maxDistance))
                    {
                        pointArray[i, j] = hitInfo.distance;
                    }
                    else
                    {
                        pointArray[i, j] = float.PositiveInfinity;
                    }

                    // Time management: Yield control if frame time limit is reached
                    timeElapsedThisFrame += Time.deltaTime;
                    if (timeElapsedThisFrame >= frameTimeLimit)
                    {
                        timeElapsedThisFrame = 0f;
                        yield return null; // Pause the coroutine until the next frame
                    }
                }
                // Update scan progress
                scanProgress = (float)(i + 1) / pointArraySize;
            }
            // 5b. (Optional) Display the point array as it is completed as an image in the game view
            if (generateLiDARImage)
            {
                UpdateLiDARImage_CompleteScan(pointArray);
            }
        }
    }

    // TODO: Try to make this a coroutine to avoid frame drops
    public void UpdateLiDARImage_LiveScan()
    {
        // Generate the LiDAR image from the point array snapshot
        float[,] pointArraySnapshot = (float[,])pointArray.Clone();
        Texture2D image = LiDARImageGeneration.GenerateLiDARImage(pointArraySnapshot, imageSize, maxDistance);
        uiImageComponent_LiveScan.sprite = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(0.5f, 0.5f));
    }
    public void UpdateLiDARImage_CompleteScan(float[,] pointArrayFrame)
    {
        // Generate the LiDAR image from the completed point array
        Texture2D image = LiDARImageGeneration.GenerateLiDARImage(pointArrayFrame, imageSize, maxDistance);
        uiImageComponent_CompleteScan.sprite = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(0.5f, 0.5f));
    }
}
