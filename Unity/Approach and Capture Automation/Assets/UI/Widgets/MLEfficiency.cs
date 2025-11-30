using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MLEfficiency : MonoBehaviour
{
    public Rendering rendering;

    [Serializable]
    public class Rendering
    {
        public Toggle renderingToggle;
        public RawImage cameraScreenSpace;
        public RenderTexture cameraTargetTexture;
        public Texture2D replacementTexture;
        public RawImage[] pausedOverlays;
        public bool doRendering = true;
        private Camera camera;

        public void Start_Manual()
        {
            camera = FindObjectOfType<Camera>();
            renderingToggle.isOn = camera.enabled;
            renderingToggle.onValueChanged.AddListener(OnRenderingToggle);
        }
        public void OnRenderingToggle(bool newDoRendering)
        {
            doRendering = newDoRendering;

            camera.enabled = doRendering;
            cameraScreenSpace.texture = doRendering ? cameraTargetTexture : replacementTexture;

            LiDARMonoBehaviour lidarComponent = FindObjectOfType<LiDARMonoBehaviour>();
            if (lidarComponent != null) { lidarComponent.imageParameters.generateLiDARImage = doRendering; }

            foreach (RawImage ri in pausedOverlays)
            {
                if (ri.transform.parent.gameObject.activeInHierarchy) { ri.gameObject.SetActive(!doRendering); }
            }
        }
    }

    void Start()
    {
        rendering.Start_Manual();
    }
}
