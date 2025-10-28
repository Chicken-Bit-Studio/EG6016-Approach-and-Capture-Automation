using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Class for generating LiDAR images from collected LiDAR data.
/// </summary>
public static class LiDARImageGeneration
{
    public static Texture2D GenerateLiDARImage(float[,] pointArray, LiDARDataCollection.ImageSizeSettings imageSize, float maxDistance)
    {
        int resolution = 1024; // Default resolution
        switch (imageSize)
        {
            case LiDARDataCollection.ImageSizeSettings.Size512x512:
                resolution = 512;
                break;
            case LiDARDataCollection.ImageSizeSettings.Size1024x1024:
                resolution = 1024;
                break;
        }

        Texture2D lidarImage = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);

        int pointArraySize = pointArray.GetLength(0);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // Map pixel coordinates to point array indices
                int pointX = Mathf.Clamp(Mathf.FloorToInt((float)x / resolution * pointArraySize), 0, pointArraySize - 1);
                int pointY = Mathf.Clamp(Mathf.FloorToInt((float)y / resolution * pointArraySize), 0, pointArraySize - 1);

                float distance = pointArray[pointX, pointY];
                Color pixelColor;

                if (distance > 0f)
                {
                    // Map distance to grayscale value (closer = brighter)
                    float intensity = Mathf.Clamp01(1f - (distance / maxDistance));
                    pixelColor = new Color(intensity, intensity, intensity);
                }
                else
                {
                    // No return detected
                    pixelColor = Color.black;
                }

                lidarImage.SetPixel(x, y, pixelColor);
            }
        }

        lidarImage.Apply();
        return lidarImage;
    }
}
