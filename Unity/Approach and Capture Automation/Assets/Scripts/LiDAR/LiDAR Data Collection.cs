using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Class for collecting LiDAR data in a Unity scene.
/// </summary>
public class LiDARDataCollection : MonoBehaviour
{
    private float timeSinceLastScan = 0f;
    private float timeSinceLastImageUpdate = 0f;

    [Header("LiDAR Initialisation Settings")]
    [Tooltip("Transform of the LiDAR emitter. Rays are cast in this transform's +y direction.")]
    public Transform emitter;
    [Tooltip("Should the LiDAR raycasts ignore colliders on parent objects of the emitter?")]
    public bool ignoreParentColliders = true;

    [Header("LiDAR Scan Settings")]
    [Tooltip("Half of the vertical field of view in degrees.")]
    public float verticalHalfFOV = 35f;
    [Tooltip("The number of rays to cast per degree in the sensor's field of view.")]
    public float raysPerDegree = 5f;
    [Tooltip("Maximum distance for each LiDAR raycast.")]
    public float maxDistance = 20f;
    [Tooltip("Frequency of LiDAR scans in Hz.")]
    public float scanFrequency = 10f;

    [Header("LiDAR image generation Settings")]
    public bool generateLiDARImage = true;
    [Tooltip("Refresh rate of the displayed LiDAR image in Hz (seperate from scanFrequency).")]
    public float imageRefreshRate = 5f;
    public enum ImageSizeSettings { Size512x512, Size1024x1024}
    [Tooltip("Resolution of the generated LiDAR image.")]
    public ImageSizeSettings imageSize = ImageSizeSettings.Size512x512;
    private Image uiImageComponent;

    [Header("Debug Settings")]
    public bool visualizeRaysInSceneView = false;

    void Start()
    {
        // Catching undeclared variables
        if (emitter == null)
        {
            Debug.LogError("LiDAR emitter transform is not assigned. Defaulting to this GameObject's transform.");
            emitter = this.transform;
        }
        try{
            uiImageComponent = GameObject.Find("LiDARImageDisplay").GetComponent<Image>();
        }
        catch{
            Debug.LogError("UI Image component for LiDAR image display not found. Please ensure there is a GameObject named 'LiDARImageDisplay' with an Image component in the scene.");
            generateLiDARImage = false; 
        }
    }

    void Update()
    {
        // Handle LiDAR scanning based on the specified frequency
        timeSinceLastScan += Time.deltaTime;
        timeSinceLastImageUpdate += Time.deltaTime;
        if (timeSinceLastScan >= 1f / scanFrequency)
        {
            PerformLiDARScan();
            timeSinceLastScan = 0f;
        }
    }

    private void PerformLiDARScan()
    {
        // Note: A generated point array will be square, so only one dimension is needed
        int pointArraySize = Mathf.CeilToInt(verticalHalfFOV * 2 * raysPerDegree);
        float[,] pointArray = new float[pointArraySize, pointArraySize];
        float stepDeg = 1f / raysPerDegree;
        Debug.Log($"Casting {pointArraySize*pointArraySize} rays.");
        //LayerMask layerMask = ignoreParentColliders ? ~(1 << emitter.gameObject.layer) : ~0;

        // Let 'i' be the vertical index and 'j' be the horizontal index
        float startDeg = -verticalHalfFOV;
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

                // 3. Cast the ray
                Vector3 rayDirection = rotation * emitter.up;
                Ray ray = new Ray(emitter.position, rayDirection);
                // (Debugging) Draw the ray in the scene view
                if (visualizeRaysInSceneView)
                {
                    Debug.DrawRay(emitter.position, rayDirection * maxDistance, Color.green, 1f / scanFrequency);
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
            }
        }
        
        // 5. (Optional) Display the point array as an image in the game view
        if (generateLiDARImage && timeSinceLastImageUpdate >= 1f / imageRefreshRate)
        {
            Texture2D image = LiDARImageGeneration.GenerateLiDARImage(pointArray, imageSize, maxDistance);
            uiImageComponent.sprite = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(0.5f, 0.5f));
            timeSinceLastImageUpdate = 0f;
        }
    }
}
