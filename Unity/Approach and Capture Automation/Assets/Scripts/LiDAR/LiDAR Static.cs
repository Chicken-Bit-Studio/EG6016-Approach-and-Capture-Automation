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
        public enum fov { _30deg, _60deg, _90deg, _120deg }
        // Summary: The density of rays per degree in both vertical and horizontal directions.
        public enum rayDensity { _1, _2, _4, _8 }
        public static float DecodeFOV(fov enumFOV)
        {
            switch (enumFOV)
            {
                case fov._30deg:
                    return 30f;
                case fov._60deg:
                    return 60f;
                case fov._90deg:
                    return 90f;
                case fov._120deg:
                    return 120f;
                default:
                    Debug.LogError("Unrecognized FOV enum value: " + Enum.GetName(typeof(fov), enumFOV));
                    return 0f;
            }
        }
        public static float DecodeRayDensity(rayDensity enumRayDensity)
        {
            switch (enumRayDensity)
            {
                case rayDensity._1:
                    return 1f;
                case rayDensity._2:
                    return 2f;
                case rayDensity._4:
                    return 4f;
                case rayDensity._8:
                    return 8f;
                default:
                    Debug.LogError("Unrecognized Ray Density enum value: " + Enum.GetName(typeof(rayDensity), enumRayDensity));
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
            foreach (LiDARParameters.rayDensity rayDensitySetting in Enum.GetValues(typeof(LiDARParameters.rayDensity)))
            {
                foreach (LiDARParameters.fov fovSetting in Enum.GetValues(typeof(LiDARParameters.fov)))
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
        private static void GenerateLiDARRayDirectionBinary(LiDARParameters.fov enumFOV, LiDARParameters.rayDensity enumRayDensity)
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
            int pointArraySize = Mathf.CeilToInt(fieldOfView * raysPerDegree);
            int rayCount = pointArraySize * pointArraySize;
            float startDeg = -fieldOfView / 2f;
            float stepDeg = 1f / raysPerDegree;

            try
            {
                // Make temporary file stream and binary writer classes
                using (var fs = File.Open(path, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fs))
                {
                    // Write the total ray count at the start of the file
                    bw.Write(rayCount);

                    // Write row-major (row = vertical index, col = horizontal index)
                    for (int r = 0; r < pointArraySize; r++)
                    {
                        // Precompute vertical angle
                        float vAngle = startDeg + r * stepDeg;
                        for (int c = 0; c < pointArraySize; c++)
                        {
                            // Precompute horizontal angle
                            float hAngle = startDeg + c * stepDeg;
                            // Compute direction vector
                            Vector3 dirVec = (Quaternion.Euler(vAngle, hAngle, 0f) * Vector3.up).normalized;
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
        public static class NativeArrays
        {
            public static int totalRayCount;
            public static int idealBatchSize;
            public static JobHandle lastJobHandle;
            public static NativeArray<float3> localspaceDirs;
            public static NativeArray<float3> worldspaceDirs;
            public static NativeArray<RaycastCommand> raycastCommands;
            public static NativeArray<RaycastHit> raycastHits;
            private static LiDARParameters.fov lastFovUsed;
            private static LiDARParameters.rayDensity lastRayDensityUsed;
            private static float lastMaxDistanceUsed;

            // Native arrays are unmanaged, so this method calls Dispose() automatically on application exit.
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            private static void WaitForApplicationQuit()
            {
                Application.quitting += Dispose;
            }
            public static void Dispose()
            {
                // Unity throws a fit if you try to dispose of native arrays if there are curerntly-running jobs that depend on them
                lastJobHandle.Complete();
                if (localspaceDirs.IsCreated) localspaceDirs.Dispose();
                if (worldspaceDirs.IsCreated) worldspaceDirs.Dispose();
                if (raycastCommands.IsCreated) raycastCommands.Dispose();
                if (raycastHits.IsCreated) raycastHits.Dispose();
            }
            public static void DisposeIfSensorParametersHaveChanged(LiDARParameters.fov thisFovUsed, LiDARParameters.rayDensity thisRayDensityUsed, float thisMaxDistanceUsed)
            {
                // If any parameters are changed between native array uses they need to be disposed of and reallocated.
                if(lastFovUsed != thisFovUsed || lastRayDensityUsed != thisRayDensityUsed || lastMaxDistanceUsed != thisMaxDistanceUsed)
                {
                    Dispose();
                    lastFovUsed = thisFovUsed;
                    lastRayDensityUsed = thisRayDensityUsed;
                    lastMaxDistanceUsed = thisMaxDistanceUsed;
                }
            }
        }
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
        public static void GenerateLocalspaceDirsNativeArray(LiDARParameters.fov enumFOV, LiDARParameters.rayDensity enumRayDensity)
        {
            // Note: Each float3 here is representitive of a localspace Vector3 ray direction centered around the +y direction.
            // Retrieve a corresponding BinaryReader
            BinaryReader br = LiDARDirectionsBinaryFileUtilities.GetBinaryFileReader(enumFOV, enumRayDensity);
            // Allocate the NativeArrays (uninitialized to avoid clearing cost)
            int count = br.ReadInt32();
            NativeArrays.totalRayCount = count;
            NativeArrays.idealBatchSize = math.clamp(count/(SystemInfo.processorCount*4), 64, 1024);
            NativeArrays.localspaceDirs = new NativeArray<float3>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeArrays.worldspaceDirs = new NativeArray<float3>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeArrays.raycastCommands = new NativeArray<RaycastCommand>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeArrays.raycastHits = new NativeArray<RaycastHit>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // Loop through the .bin file and create populate the float3 native array
            for (int i = 0; i < count; i++)
            {
                float x = br.ReadSingle();
                float y = br.ReadSingle();
                float z = br.ReadSingle();
                NativeArrays.localspaceDirs[i] = new float3(x, y, z);
            }
        }
        // Schedule and run a Burst-based sequence of tasks - perform a LiDAR scan!
        public static JobHandle ScheduleAndRunLiDARRaycasts(Transform emitter, LiDARParameters.fov enumFov, LiDARParameters.rayDensity enumRayDensity, float maxDistance)
        {
            // Perform some simple logic if the parameters being passed have changed from last time
            NativeArrays.DisposeIfSensorParametersHaveChanged(enumFov, enumRayDensity, maxDistance);

            // Ensure the proper localspace ray directions array has been populated
            if (!NativeArrays.localspaceDirs.IsCreated) { GenerateLocalspaceDirsNativeArray(enumFov, enumRayDensity); }

            // Create and schedule a RotateRayDirectionsJob task
            var rotateRayDirectionsJob = new JobStructures.RotateRayDirectionsJob
            {
                lsDirs = NativeArrays.localspaceDirs,
                wsDirs = NativeArrays.worldspaceDirs,
                rot = new quaternion(emitter.rotation.x, emitter.rotation.y, emitter.rotation.z, emitter.rotation.w),
            };
            JobHandle rotateRayDirectionsJob_jobHandle = rotateRayDirectionsJob.Schedule(NativeArrays.totalRayCount, NativeArrays.idealBatchSize);

            // Create and schedule a BuildRaycastCommandsJob task. Note that rotateJob is passed as a completion prerequisite in the .Schedule method.
            var buildRaycastCommandsJob = new JobStructures.BuildRaycastCommandsJob
            {
                wsDirs = NativeArrays.worldspaceDirs,
                raycastCommands = NativeArrays.raycastCommands,
                raysOrigin = (float3)emitter.position,
                qp = QueryParameters.Default,   // Note: Install potential ignore/backface/other raycast logic here if necessary, or pass it as a parameter.
                maxDistance = maxDistance,
            };
            JobHandle buildRaycastCommandsJob_jobHandle = buildRaycastCommandsJob.Schedule(NativeArrays.totalRayCount, NativeArrays.idealBatchSize, rotateRayDirectionsJob_jobHandle);

            // Schedule the actual batched raycast physics processes
            NativeArrays.lastJobHandle = RaycastCommand.ScheduleBatch(
                NativeArrays.raycastCommands,
                NativeArrays.raycastHits,
                NativeArrays.idealBatchSize,
                buildRaycastCommandsJob_jobHandle
            );
            return NativeArrays.lastJobHandle;
        }
    }

    public static class LiDARDirectionsBinaryFileUtilities
    {
        // LiDAR ray direction binary files use the following naming convention:
        // $"LiDARRayDirections{sanitizedFov}FOV{sanitizedRpd}RaysPerDegree.bin"

        // Generate LiDAR ray direction binary file names using known conventions
        public static string GenerateLiDARRayDirectionBinaryPath(LiDARParameters.fov enumFOV, LiDARParameters.rayDensity enumRayDensity, bool fileNameOnly = false)
        {
            // Generate filename and path
            string sanitizedFov = Enum.GetName(typeof(LiDARParameters.fov), enumFOV);
            string sanitizedRpd = Enum.GetName(typeof(LiDARParameters.rayDensity), enumRayDensity);
            string fileName = $"LiDARRayDirections{sanitizedFov}FOV{sanitizedRpd}RaysPerDegree.bin";
            if (fileNameOnly) return fileName;
            string dir = Application.streamingAssetsPath;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, fileName);
        }
        // Provide a BinaryReader for a LiDAR ray direction binary file for the given sensor parameters
        public static BinaryReader GetBinaryFileReader(LiDARParameters.fov enumFOV, LiDARParameters.rayDensity enumRayDensity)
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
        public enum ImageResolution { Size512x512, Size1024x1024 };
        private static Texture2D lidarImage;

        public static void UpdateLiDARImage(Image destinationImage, ImageResolution enumResolution)
        {
            Debug.
        }
    }
}
