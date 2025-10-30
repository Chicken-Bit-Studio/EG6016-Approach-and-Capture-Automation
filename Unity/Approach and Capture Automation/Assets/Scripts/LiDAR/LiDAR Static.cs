using System;
using System.IO;
using Microsoft.Unity.VisualStudio.Editor;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static StaticUtilities;

/// <summary>
/// LiDAR Static class. Also see LiDARMonoBehaviour.
/// An oversight: This fails if more than one LiDAR sensor is running in the scene at any given time, due to single static instances of each native array.
/// </summary>
public static class LiDARStatic
{
    // Static LiDAR parameters
    public static class LiDARParameters
    {
        // Summary: The complete field of view from the sensor's 'up' direction. Not the half FOV.
        public enum FOV { _30deg, _60deg, _90deg, _120deg }
        // Summary: The density of rays per degree in both vertical and horizontal directions.
        public enum RayDensity { _1, _2, _4, _8 }
        public static float DecodeFOV(FOV enumFOV)
        {
            switch (enumFOV)
            {
                case FOV._30deg:
                    return 30f;
                case FOV._60deg:
                    return 60f;
                case FOV._90deg:
                    return 90f;
                case FOV._120deg:
                    return 120f;
                default:
                    Debug.LogError("Unrecognized FOV enum value: " + Enum.GetName(typeof(FOV), enumFOV));
                    return 0f;
            }
        }
        public static float DecodeRayDensity(RayDensity enumRayDensity)
        {
            switch (enumRayDensity)
            {
                case RayDensity._1:
                    return 1f;
                case RayDensity._2:
                    return 2f;
                case RayDensity._4:
                    return 4f;
                case RayDensity._8:
                    return 8f;
                default:
                    Debug.LogError("Unrecognized Ray Density enum value: " + Enum.GetName(typeof(RayDensity), enumRayDensity));
                    return 0f;
            }
        }
    }

    // Subclass containting precomputable LiDAR tasks not needing to be ran in a MonoBehaviour
    public static class LiDARPrecomputation
    {
        // Precomputation of LiDAR ray directions for a discrete set of LiDAR parameters reduces runtime computation load significantly.
        // This class provides an Editor menu item to recompute all combinations of LiDAR ray direction binaries for all LiDARParameters enum combinations.
        // The generated .bin files are stored in Application.streamingAssetsPath by default. They are structured as follows:
        // [int32] array size
        // [int32] count
        // [float32] x0, [float32] y0, [float32] z0, [float32] x1, [float32] y1, [float32] z1, ...
        // Each (x,y,z) triple is a normalized direction vector for a LiDAR ray.
        // The ray direction triples are stored in row-major order, where each row corresponds to a vertical angle index and each column to a horizontal angle index.
        // To use the generated .bin files, read the count first, then read count * 3 float32 values and reconstruct Vector3 direction vectors from each consecutive triple,
        // multiplying each direction vector by the emitter object's rotation to get world-space directions.

        // Management of precomputation tasks on an iterative per-LiDARParameters parameter basis
        [MenuItem("Custom Tools/Recompute LiDAR Ray Direction Binaries")]
        private static void PrecomputationManager()
        {
            // Iterate through all combinations of LiDAR parameters
            // Note: Iterate through the low ray density settings first to reduce computation time for testing and to ensure the cheaper tasks are completed and saved first
            foreach (LiDARParameters.RayDensity rayDensitySetting in Enum.GetValues(typeof(LiDARParameters.RayDensity)))
            {
                foreach (LiDARParameters.FOV fovSetting in Enum.GetValues(typeof(LiDARParameters.FOV)))
                {
                    // Generate LiDAR ray direction binary for this parameter combination
                    GenerateLiDARRayDirectionBinary(fovSetting, rayDensitySetting);
                    // Note: Task.Run() was removed above to avoid issues with Unity API calls from non-main threads. The precomputation may take longer now.
                }
            }
            // Refresh the AssetDatabase so StreamingAssets changes show up in Editor. Only do this on the next Editor update tick to avoid issues with using multiple threads.
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
                Debug.Log("LiDAR Precomputation Manager has completed all tasks.");
            };
        }
        private static void GenerateLiDARRayDirectionBinary(LiDARParameters.FOV enumFOV, LiDARParameters.RayDensity enumRayDensity)
        {
            // Declare the process has started and start a stopwatch to time it
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            // Map enum settings to numerical values
            float fieldOfView = LiDARParameters.DecodeFOV(enumFOV);
            float raysPerDegree = LiDARParameters.DecodeRayDensity(enumRayDensity);

            // Generate filename and path
            string path = LiDARDirectionsBinaryFileUtilities.GenerateLiDARRayDirectionBinaryPath(enumFOV, enumRayDensity);

            // Calculate point array size, ray count, etc.
            int rootOfArraySize = Mathf.CeilToInt(fieldOfView * raysPerDegree);
            int rayCount = rootOfArraySize * rootOfArraySize;
            float startDeg = -fieldOfView / 2f;
            float stepDeg = 1f / raysPerDegree;

            try
            {
                // Make temporary file stream and binary writer classes
                using (var fs = File.Open(path, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fs))
                {
                    // Write the array size and total ray count at the start of the file
                    bw.Write(rootOfArraySize);
                    bw.Write(rayCount);

                    // Write row-major (row = vertical index, col = horizontal index)
                    for (int r = 0; r < rootOfArraySize; r++)
                    {
                        // Precompute vertical angle
                        float vAngle = startDeg + r * stepDeg;
                        for (int c = 0; c < rootOfArraySize; c++)
                        {
                            // Precompute horizontal angle
                            float hAngle = startDeg + c * stepDeg;
                            // Compute direction vector
                            Vector3 dirVec = (Quaternion.Euler(vAngle, 0f, hAngle) * Vector3.up).normalized;
                            bw.Write(dirVec.x);
                            bw.Write(dirVec.y);
                            bw.Write(dirVec.z);
                        }
                    }
                }
                // End the process and report success to console with file path and size
                stopwatch.Stop();
                Debug.Log($"LiDAR direction .bin written for \"{LiDARDirectionsBinaryFileUtilities.GenerateLiDARRayDirectionBinaryPath(enumFOV, enumRayDensity, true)}\"" +
                    $" in: {FormatStopwatchDuration(stopwatch)}\n(vectors: {rayCount}, approx {rayCount * 12f / 1048576f:F3}MB)");
            }
            // Catch any exceptions and report failure to console. The workflow can continue for other parameter combinations.
            catch (Exception ex)
            {
                Debug.LogError($"Failed to write LiDAR direction file for FOV: {enumFOV}, Ray Density: {enumRayDensity}. See below.\n{ex}");
            }
        }
    }

    public static class LiDARRuntimeJobs
    {
        // Burst job structs for mega-efficient execution of heavy batch-based math
        private static class JobStructures
        {
            public struct RotateRayDirectionsJob : IJobParallelFor
            {
                [ReadOnly] public NativeArray<float3> lsDirs;
                public NativeArray<float3> wsDirs;
                public quaternion rot;  // unity.mathematics quaternion
                public void Execute(int i)
                {
                    wsDirs[i] = math.mul(rot, lsDirs[i]);
                }
            }
            public struct BuildRaycastCommandsJob : IJobParallelFor
            {
                [ReadOnly] public NativeArray<float3> wsDirs;
                public NativeArray<RaycastCommand> raycastCommands;
                public float3 raysOrigin;
                public QueryParameters qp;
                public float maxDistance;
                public void Execute(int i)
                {
                    // RaycastCommand has a constructor with (origin, direction, QueryParameters, maxDistance)
                    raycastCommands[i] = new RaycastCommand(raysOrigin, wsDirs[i], qp, maxDistance);
                }
            }
        }
        // Get a NativeArray<float3> object for the given sensor parameters. This array is generated once and not changed outside of sensor parameter updates.
        public static void GenerateLocalspaceDirsNativeArray(LiDARMonoBehaviour monoBehaviourInstance)
        {
            // Create a shorter alias for readability
            var mono = monoBehaviourInstance;

            // Note: Each float3 here is representitive of a localspace Vector3 ray direction centered around the +y direction.
            // Retrieve a corresponding BinaryReader
            BinaryReader br = LiDARDirectionsBinaryFileUtilities.GetBinaryFileReader(mono.sensorParameters.fieldOfView, mono.sensorParameters.raysPerDegree);

            // Allocate the NativeArrays (uninitialized to avoid clearing cost)
            mono.nativeArrays.rootOfArraySize = br.ReadInt32();
            int count = br.ReadInt32();
            mono.nativeArrays.totalRayCount = count;
            mono.nativeArrays.idealBatchSize = math.clamp(count / (SystemInfo.processorCount * 4), 64, 1024);
            mono.nativeArrays.localspaceDirs = new NativeArray<float3>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mono.nativeArrays.worldspaceDirs = new NativeArray<float3>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mono.nativeArrays.raycastCommands = new NativeArray<RaycastCommand>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mono.nativeArrays.raycastHits = new NativeArray<RaycastHit>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Loop through the .bin file and populate the float3 native array
            for (int i = 0; i < count; i++)
            {
                float x = br.ReadSingle();
                float y = br.ReadSingle();
                float z = br.ReadSingle();
                mono.nativeArrays.localspaceDirs[i] = new float3(x, y, z);
            }
        }
        // Schedule and run a Burst-based sequence of tasks - perform a LiDAR scan!
        public static ref JobHandle ScheduleAndRunLiDARRaycasts(LiDARMonoBehaviour monoBehaviourInstance)
        {
            // Create a shorter alias for readability
            var mono = monoBehaviourInstance;

            // Ensure the proper localspace ray directions array has been populated
            if (!mono.nativeArrays.localspaceDirs.IsCreated) { GenerateLocalspaceDirsNativeArray(mono); }

            // Create and schedule a RotateRayDirectionsJob task
            var rotateRayDirectionsJob = new JobStructures.RotateRayDirectionsJob
            {
                lsDirs = mono.nativeArrays.localspaceDirs,
                wsDirs = mono.nativeArrays.worldspaceDirs,
                rot = new quaternion(mono.sensorParameters.emitter.rotation.x, mono.sensorParameters.emitter.rotation.y, mono.sensorParameters.emitter.rotation.z, mono.sensorParameters.emitter.rotation.w),
            };
            JobHandle rotateRayDirectionsJob_jobHandle = rotateRayDirectionsJob.Schedule(mono.nativeArrays.totalRayCount, mono.nativeArrays.idealBatchSize);

            // Create and schedule a BuildRaycastCommandsJob task. Note that rotateJob is passed as a completion prerequisite in the .Schedule method.
            var buildRaycastCommandsJob = new JobStructures.BuildRaycastCommandsJob
            {
                wsDirs = mono.nativeArrays.worldspaceDirs,
                raycastCommands = mono.nativeArrays.raycastCommands,
                raysOrigin = (float3)mono.sensorParameters.emitter.position,
                qp = QueryParameters.Default,   // TODO: Install potential ignore/backface/other raycast logic here if necessary, or pass it as a parameter.
                maxDistance = mono.sensorParameters.maxDistance
            };
            JobHandle buildRaycastCommandsJob_jobHandle = buildRaycastCommandsJob.Schedule(mono.nativeArrays.totalRayCount, mono.nativeArrays.idealBatchSize, rotateRayDirectionsJob_jobHandle);

            // Schedule the actual batched raycast physics processes
            mono.nativeArrays.lastJobHandle = RaycastCommand.ScheduleBatch(
                mono.nativeArrays.raycastCommands,
                mono.nativeArrays.raycastHits,
                mono.nativeArrays.idealBatchSize,
                buildRaycastCommandsJob_jobHandle
            );

            // Allow for ray drawing as a debugging tool. Slow.
            if (mono.debuggingSettings.drawRays)
            {
                buildRaycastCommandsJob_jobHandle.Complete();
                for (int i = 0; i < mono.nativeArrays.totalRayCount; i++)
                {
                    if (i % 283 == 0)
                    {
                        Debug.DrawRay(
                            mono.sensorParameters.emitter.position,
                            mono.nativeArrays.worldspaceDirs[i],
                            Color.green, mono.imageParameters.imageRefreshPeriod
                        );
                    }
                }
            }

            return ref mono.nativeArrays.lastJobHandle;
        }
    }

    public static class LiDARDirectionsBinaryFileUtilities
    {
        // LiDAR ray direction binary files use the following naming convention:
        // $"LiDARRayDirections{sanitizedFov}FOV{sanitizedRpd}RaysPerDegree.bin"

        // Generate LiDAR ray direction binary file names using known conventions
        public static string GenerateLiDARRayDirectionBinaryPath(LiDARParameters.FOV enumFOV, LiDARParameters.RayDensity enumRayDensity, bool fileNameOnly = false)
        {
            // Generate filename and path
            string sanitizedFov = Enum.GetName(typeof(LiDARParameters.FOV), enumFOV);
            string sanitizedRpd = Enum.GetName(typeof(LiDARParameters.RayDensity), enumRayDensity);
            string fileName = $"LiDARRayDirections{sanitizedFov}FOV{sanitizedRpd}RaysPerDegree.bin";
            if (fileNameOnly) return fileName;
            string dir = Application.streamingAssetsPath;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, fileName);
        }
        // Provide a BinaryReader for a LiDAR ray direction binary file for the given sensor parameters
        public static BinaryReader GetBinaryFileReader(LiDARParameters.FOV enumFOV, LiDARParameters.RayDensity enumRayDensity)
        {
            // Generate expected file path using the same logic as the precomputation function
            string path = GenerateLiDARRayDirectionBinaryPath(enumFOV, enumRayDensity);
            // Check that the file exists
            if (!File.Exists(path))
            {
                Debug.LogError($"LiDAR ray direction binary file not found at expected path. Run .bin precomputation in [Custom Tools > Recompute LiDAR Ray Direction Binaries]. Failed path: {path}");
                return null;
            }
            // Return a BinaryReader for the file
            return new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read));
        }
    }

    public static class LiDARImageGeneration
    {
        //***********
        //Note: Image generation is currently bare-bones and not parametised.

        public enum ImageResolution { Size512x512, Size1024x1024 };
        public enum MappingCurve { Linear, Exponential, Reciprocal, Logarithmic, GammaCorrection};
        public static int DecodeImageResolution(ImageResolution enumImgRes)
        {
            switch (enumImgRes)
            {
                case ImageResolution.Size512x512:
                    return 512;
                case ImageResolution.Size1024x1024:
                    return 1024;
                default:
                    Debug.LogError("Unrecognized ImageResolution enum value: " + Enum.GetName(typeof(ImageResolution), enumImgRes));
                    return 4;
            }
        }
        public static Func<float, float, float> DecodeMappingCurve(MappingCurve enumMappingCurve)
        {
            switch (enumMappingCurve)
            {
                case MappingCurve.Linear:
                    return (v, a) => v;
                case MappingCurve.Exponential:
                    return (v, a) => 1f - Mathf.Exp(-a * (1f - v));
                case MappingCurve.Reciprocal:
                    return (v, a) => 1f / (1f + a * v);
                case MappingCurve.Logarithmic:
                    return (v, a) => Mathf.Log10(1f + a * (1f - v)) / Mathf.Log10(1f + a);
                case MappingCurve.GammaCorrection:
                    return (v, a) => Mathf.Pow(v, a);
                default:
                    Debug.LogError("Unrecognized MappingCurve enum value: " + Enum.GetName(typeof(ImageResolution), enumMappingCurve));
                    return (v, a) => 0f;
            }   
        }
        public static void UpdateLiDARImage(LiDARMonoBehaviour monoBehaviourInstance)
        {
            // Optimisation target

            // Start a stopwatch for debugging purposes
            //var stopwatch = new System.Diagnostics.Stopwatch();
            //stopwatch.Start();

            // Create a shorter alias for readability
            var mono = monoBehaviourInstance;

            // If the mono behaviour's output texture is undefined, generate it
            if (mono.imageParameters.lidarTexture == null) { RegenerateLiDARImage(mono); }

            // Loop through each pixel in the monobehaviour's LiDAR output texture
            for (int i = 0; i < mono.imageParameters.lidarTexturePixelCount; i++)
            {
                // Determine the corresponding native array element and collect the RaycastHit object corresponding to this pixel
                RaycastHit hit = mono.nativeArrays.raycastHits[mono.imageParameters.lidarTextureMappedIndexes[i]];
                // Note: No hit corresponds to a distance value of zero for some reason. Surely it should be float.PositiveInfinity, but what do I know.
                // Calculate the 16-bit colour of the resulting pixel. Make non-hits black and others fade in from gloom ooo spooky
                ushort value16 = 0;
                if(hit.collider != null)
                {
                    float pseudoValue = 1 - hit.distance / mono.sensorParameters.maxDistance; // 0.0 to 1.0
                    value16 = (ushort)(DecodeMappingCurve(mono.imageParameters.mappingCurve)(pseudoValue, mono.imageParameters.a) * 65535f);
                }
                // Remember the expected byte buffer in 16-bit format uses two bytes per pixel so the buffer size is twice as large as the pixel conut. "little-endian" order: low byte then high byte.
                int i2 = i * 2;
                mono.imageParameters.lidarTextureByteBuffer[i2] = (byte)(value16 & 0xFF); // Bitmask AND the 16-bit value with 8-bit [11111111] to take the low byte.
                mono.imageParameters.lidarTextureByteBuffer[i2 + 1] = (byte)((value16 >> 8) & 0xFF); // Do the same but make eight right shifts first. Thanks to Stack Overflow for this one.
            }

            // Apply the byte buffer and comit the changes to the texture
            mono.imageParameters.lidarTexture.LoadRawTextureData(mono.imageParameters.lidarTextureByteBuffer);
            mono.imageParameters.lidarTexture.Apply();

            // Report to console
            //stopwatch.Stop();
            //Debug.Log($"Texture update took {FormatStopwatchDuration(stopwatch)} with {mono.imageParameters.lidarTexturePixelCount} pixels and {mono.nativeArrays.CountRayHits()} hits out of {mono.nativeArrays.totalRayCount} rays.");

        }
        private static void RegenerateLiDARImage(LiDARMonoBehaviour monoBehaviourInstance)
        {
            // Create a shorter alias for readability
            var mono = monoBehaviourInstance;

            // Size, create, and assign an empty Texture2D object
            int newImgSize = math.clamp(mono.nativeArrays.rootOfArraySize, 4, 512);
            //Debug.LogWarning($"Regenerating Texture2D with image size {newImgSize}x{newImgSize}\nnativeArrays.rootOfArraySize: {mono.nativeArrays.rootOfArraySize}");
            mono.imageParameters.lidarTexture = new Texture2D(
                width: newImgSize,
                height: newImgSize,
                textureFormat: TextureFormat.R16,
                mipChain: false);
            mono.imageParameters.lidarTexture.filterMode = FilterMode.Point;
            mono.imageParameters.lidarUIImage.texture = mono.imageParameters.lidarTexture;

            // Calculate and record now-unchanging values used during texture updates
            mono.imageParameters.lidarTexturePixelCount = newImgSize * newImgSize;
            mono.imageParameters.lidarTextureByteBuffer = new byte[mono.imageParameters.lidarTexturePixelCount * 2];
            mono.imageParameters.lidarTextureMappedIndexes = new int[mono.imageParameters.lidarTexturePixelCount];
            float pixelMappingScale = (float)mono.nativeArrays.totalRayCount / mono.imageParameters.lidarTexturePixelCount;
            for (int i = 0; i < mono.imageParameters.lidarTexturePixelCount; i++)
            {
                mono.imageParameters.lidarTextureMappedIndexes[i] = Mathf.FloorToInt(i * pixelMappingScale);
            }
        }
    }
}
