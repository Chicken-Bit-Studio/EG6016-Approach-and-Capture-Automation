using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraDisable : MonoBehaviour
{
    private Camera mainCamera;
    private LiDARMonoBehaviour lidarComponent;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null) { Debug.LogError("Main Camera not found!"); }
        lidarComponent = FindObjectOfType<LiDARMonoBehaviour>();
    }

    public void OnRenderingToggle(bool isRendering)
    {
        mainCamera.enabled = isRendering;
        if (lidarComponent != null)
        {
            lidarComponent.imageParameters.generateLiDARImage = isRendering;
        }

        string t = isRendering ? "enabled" : "disabled";
        Debug.Log("Camera rendering " + t);
    }
}
