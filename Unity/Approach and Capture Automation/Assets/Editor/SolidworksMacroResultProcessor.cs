using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using static RoboticsDataClasses;
using static BinarySTLLoader;
using static TransformationMatrixMath;

#if UNITY_EDITOR
public static class SolidworksMacroResultProcessor
{
    private static class Paths
    {
        public static class Directories
        {
            public static readonly string path_solidworksMacroResult = "Assets/Editor/SOLIDWORKSMacroResult";
            public static readonly string path_finalPrefabOutput = "Assets/Resources/Prefabs/ModelParts/SegmentsFromSOLIDWORKS";
            public static readonly string path_intermediateMeshFolder = "Assets/Meshes";
            public static readonly string path_intermediateThumbnailFolder = "unused";
        }
        public static class Prefabs
        {
            public static readonly string path_nodePrefab = "Assets/Resources/Prefabs/ModelParts/Nodes/Node.prefab";
            public static readonly string path_prefabInsertionWidgetPrefab = "unused";
            public static readonly string path_prefabInsertionWidgetTilePrefab = "unused";
        }
        public static class Materials
        {
            public static readonly string path_segmentMaterial = "Assets/Materials/White.mat";
            public static readonly string path_servoMaterial = "unused";

        }
    }

    public static class Workflow
    {
        [MenuItem("Custom Tools/Regenerate and Reintegrate Prefabs from SOLIDWORKS Macro Output")]
        public static async void RunWorkflow()
        {
            //t
            bool regeneratePrefabs = true;
            bool reintegratePrefabsIntoUIs = false;
            //\t

            // Begin system stopwatch for overall workflow timing.
            string elapsedTimeReport = "";
            System.Diagnostics.Stopwatch stopwatch_main = new System.Diagnostics.Stopwatch();
            stopwatch_main.Start();
            // Validate that at least one operation is selected.
            if (!(regeneratePrefabs | reintegratePrefabsIntoUIs))
            {
                throw new InvalidOperationException("No operations selected. Please enable prefab regeneration and/or prefab UI reintegration.");
            }
            // Execute selected operations.
            if (regeneratePrefabs)
            {
                System.Diagnostics.Stopwatch stopwatch_temp = new System.Diagnostics.Stopwatch();
                stopwatch_temp.Start();
                await PrefabRegeneration.RunPrefabRegeneration();
                stopwatch_temp.Stop();
                elapsedTimeReport += $"Prefab regeneration completed in {stopwatch_temp.ElapsedMilliseconds}ms. ";
            }
            if (reintegratePrefabsIntoUIs)
            {
                System.Diagnostics.Stopwatch stopwatch_temp = new System.Diagnostics.Stopwatch();
                stopwatch_temp.Start();
                await PrefabReintegration.RunPrefabReintegration();
                stopwatch_temp.Stop();
                elapsedTimeReport += $"Prefab UI reintegration completed in {stopwatch_temp.ElapsedMilliseconds}ms. ";
            }
            // End overall workflow timing and report to debug console.
            stopwatch_main.Stop();
            Debug.Log($"Workflow completed. {elapsedTimeReport}Total time elapsed: {stopwatch_main.ElapsedMilliseconds}ms.");
        }
    }
    
    private static class Utilities
    {
        public static void NukeAndRecreateFolders(List<string> folderPathList)
        {
            // Deletes and recreates the specified folders to ensure a clean state in those directories.
            foreach (string folderPath in folderPathList)
            {
                if (AssetDatabase.IsValidFolder(folderPath))
                {
                    Directory.Delete(folderPath, true);
                }
                Directory.CreateDirectory(folderPath);
            }
        }
        public static List<Task> ProcessFilesRecursively(string rootDirectory, Func<string, bool> validation, Func<string, Task> process)
        {
            // Recursively traverses a directory tree, validates each file/folder, processes valid entries asynchronously, and returns a list of all initiated Tasks.
            // Setup and initiation
            if (!AssetDatabase.IsValidFolder(rootDirectory))
            {
                throw new DirectoryNotFoundException($"The specified root directory does not exist inside the asset database: {rootDirectory}");
            }
            List<Task> initiatedTasks = new List<Task>();
            RecurseDirectory(rootDirectory);
            // Recursion procedure
            void RecurseDirectory(string currentDirectory)
            {
                foreach (string entity in Directory.GetFileSystemEntries(currentDirectory))
                {
                    if (validation(entity))
                    {
                        try
                        {
                            initiatedTasks.Add(process(entity));
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error processing {entity}: {e}");
                        }
                    }
                    if (File.GetAttributes(entity).HasFlag(FileAttributes.Directory))
                    {
                        RecurseDirectory(entity);
                    }
                }
            }
            // Return all initiated tasks
            return initiatedTasks;
        }
    }
    
    private static class FileValidation
    {
        public static bool IsValidSolidworksMacroResultLeafDirectory(string path)
        {
            // Validate that the given path is a valid SOLIDWORKS macro output leaf directory.
            // No other files or folders should exist in these valid leaf directories, but, if they do, they will be iterated through anyway due to the thorough nature of Utilies.ProcessFilesRecursively.
            return AssetDatabase.IsValidFolder(path)
                && Directory.GetFiles(path, "*.json").Length == 1
                && Directory.GetFiles(path, "*.stl").Length == 1;
        }
        public static bool IsValidPrefabAssetPath(string path)
        {
            // Validate that the given path is a valid prefab asset path.
            return File.Exists(path) && path.EndsWith(".prefab");
        }
    }
    
    private class PrefabRegeneration
    {
        private class AssetReferences
        {
            public static GameObject nodePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Paths.Prefabs.path_nodePrefab);
            public static Material segmentMaterial = AssetDatabase.LoadAssetAtPath<Material>(Paths.Materials.path_segmentMaterial);
            public static Material servoMaterial = AssetDatabase.LoadAssetAtPath<Material>(Paths.Materials.path_servoMaterial);
        }
        public static async Task RunPrefabRegeneration()
        {
            // Step 1: Nuke and recreate intermediate and final output folders.
            Utilities.NukeAndRecreateFolders(new List<string> {
                Paths.Directories.path_finalPrefabOutput,
                Paths.Directories.path_intermediateMeshFolder
            });
            // Step 2: Process each valid SOLIDWORKS macro output leaf directory to generate prefabs.
            List<Task> prefabGenerationTasks = Utilities.ProcessFilesRecursively(
                Paths.Directories.path_solidworksMacroResult,
                FileValidation.IsValidSolidworksMacroResultLeafDirectory,
                GeneratePrefabFromSolidworksMacroResult
            );
            await Task.WhenAll(prefabGenerationTasks);
            // Step 3: Refresh the AssetDatabase to recognize newly created assets.
            AssetDatabase.Refresh();
        }
        private static Task GeneratePrefabFromSolidworksMacroResult(string solidworksMacroResultItemPath)
        {
            // Finding the sub-path in the SOLIDWORKS macro output folder by removing the base path and creating useful folders in the Asset Database.
            string subPathInSolidworksMacroResult = solidworksMacroResultItemPath.Substring(Paths.Directories.path_solidworksMacroResult.Length);
            string intermediateMeshLocationPath = Paths.Directories.path_intermediateMeshFolder + subPathInSolidworksMacroResult;
            string finalPrefabLocationPath = Paths.Directories.path_finalPrefabOutput + subPathInSolidworksMacroResult;
            Directory.CreateDirectory(intermediateMeshLocationPath);
            Directory.CreateDirectory(finalPrefabLocationPath);
            
            // Deserialize the JSON file to get a SegmentProfile object.
            SegmentProfile segmentProfile = Newtonsoft.Json.JsonConvert.DeserializeObject<SegmentProfile>(
                File.ReadAllText(Directory.GetFiles(solidworksMacroResultItemPath, "*.json").FirstOrDefault()));
            //Debug.Log($"SegmentProfile (first read) of {segmentProfile.segmentName}:\n" + File.ReadAllText(Directory.GetFiles(solidworksMacroResultItemPath, "*.json").FirstOrDefault()));
            
            // Load the STL file as a mesh and store it as an asset.
            Mesh mesh = LoadBinarySTL(Directory.GetFiles(solidworksMacroResultItemPath, "*.stl").FirstOrDefault(), LengthUnits.Millimeters);
            AssetDatabase.CreateAsset(mesh, intermediateMeshLocationPath + "/" + segmentProfile.segmentName + ".asset");

            // Create a new GameObject for the prefab and add necessary core components.
            GameObject gameObject = new GameObject(segmentProfile.segmentName);
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = AssetReferences.segmentMaterial;  // TODO: Use segmentMaterial or servoMaterial based on segmentProfile.type. Don't rework this whole bloody thing - just add logic here!
            meshCollider.sharedMesh = mesh;
            gameObject.tag = "ModelSegment";

            // Giving the new GameObject an accessible and populated container for custom structures found in DataStructures.
            ContainerClass segmentContainerClass = gameObject.AddComponent<ContainerClass>();
            string report = $"SP output from ContainerClass on {gameObject.name}:\n";
            bool isEmpty = segmentProfile == null;
            report += $"SegmentProfile is null: {isEmpty}";
            if (!isEmpty && segmentProfile.nodes != null)
            {
                report += $"\n";
                report += $"Number of nodes: {segmentProfile.nodes.Length}\n";
                report += $"    Of node[0]:";
                report += $"        Has actuator: {segmentProfile.nodes[0].actuator != null}";
                report += $"        Has child: {segmentProfile.nodes[0].child != null}";
            }
            Debug.Log(report);
            //segmentContainerClass.segmentProfile = segmentProfile;
            segmentContainerClass.SetValue(segmentProfile);
            
            // If the segment has any attachment nodes, instantiate the node prefab for each node and set their positions and rotations based on the transformation matrices in the SegmentProfile.
            if (segmentProfile.nodes != null)
            {
                foreach (AttachmentNode attachmentNode in segmentProfile.nodes)
                {
                    GameObject nodeGameObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetReferences.nodePrefab);
                    nodeGameObject.transform.SetParent(gameObject.transform);
                    nodeGameObject.transform.localPosition = ExtractPosition(attachmentNode.transformationMatrix);
                    nodeGameObject.transform.localRotation = ExtractQuaternion(attachmentNode.transformationMatrix);
                }
            }

            // Save the finished prefab to its final location in the Asset Database and then destroy it in the scene.
            PrefabUtility.SaveAsPrefabAsset(gameObject, finalPrefabLocationPath + "/" + segmentProfile.segmentName + ".prefab");
            GameObject.DestroyImmediate(gameObject);
            return Task.CompletedTask;
        }
    }

    private class PrefabReintegration
    {
        private static class AssetReferences
        {
            public static GameObject prefabInsertionWidgetTilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Paths.Prefabs.path_prefabInsertionWidgetTilePrefab);
        }
        private static class ObjectInstances
        {
            public static GameObject prefabInsertionWidgetInstance = PrefabUtility.LoadPrefabContents(Paths.Prefabs.path_prefabInsertionWidgetPrefab);
            public static Transform prefabInsertionWidgetInstance_PrefabTileContainer = prefabInsertionWidgetInstance.transform.Find("Content Container/Panel1/Prefab Selection Scroll View/Viewport/PrefabTileContainer");
        }
        public static async Task RunPrefabReintegration()
        {
            // Step 1: Nuke and recreate intermediate thumbnail folder
            Utilities.NukeAndRecreateFolders(new List<string> {
                Paths.Directories.path_intermediateThumbnailFolder
            });
            // Step 2: Acknowledge that an instance of PrefabInsertionWidget is loaded temporarily for modification as soon as the static class ObjectInstances is accessed.
            // Step 3: Remove all existing PrefabUITiles from PrefabInsertionWidget.
            RemoveAllPrefabUITilesFromPrefabInsertionWidget();
            // Step 4: Process each valid prefab asset path to reintegrate them into PrefabInsertionWidget.
            List<Task> prefabUIIntegrationTasks = Utilities.ProcessFilesRecursively(
                Paths.Directories.path_finalPrefabOutput,
                FileValidation.IsValidPrefabAssetPath,
                IntegratePrefabIntoPrefabInsertionWidget
            );
            await Task.WhenAll(prefabUIIntegrationTasks);
            // Step 5: Save all and unload the modified PrefabInsertionWidget prefab.
            PrefabUtility.SaveAsPrefabAsset(ObjectInstances.prefabInsertionWidgetInstance, Paths.Prefabs.path_prefabInsertionWidgetPrefab);
            PrefabUtility.UnloadPrefabContents(ObjectInstances.prefabInsertionWidgetInstance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        public static void RemoveAllPrefabUITilesFromPrefabInsertionWidget()
        {
            // Remove all existing PrefabUITiles from the given PrefabInsertionWidget prefab instance.
            if (ObjectInstances.prefabInsertionWidgetInstance_PrefabTileContainer == null)
            {
                throw new InvalidOperationException($"PrefabTileContainer not reached in {ObjectInstances.prefabInsertionWidgetInstance.name} prefab. Check the Transform.Find() parameter used in PrefabReintegration.ObjectInstances!");
            }
            // Collect children to destroy ahead of time to avoid modifying the collection during iteration.
            List<GameObject> childrenToDestroy = new List<GameObject>();
            foreach (Transform child in ObjectInstances.prefabInsertionWidgetInstance_PrefabTileContainer)
            {
                if (child.name.StartsWith(AssetReferences.prefabInsertionWidgetTilePrefab.name))
                {
                    childrenToDestroy.Add(child.gameObject);
                }
            }
            // Destroy collected children.
            foreach (GameObject child in childrenToDestroy)
            {
                GameObject.DestroyImmediate(child);
            }
        }
        public static async Task IntegratePrefabIntoPrefabInsertionWidget(string prefabPath)
        {
            // Finding the sub-path in the completed prefabs output folder by removing the base path and creating useful folders in the Asset Database.
            string relativePrefabPath = Path.GetRelativePath(Paths.Directories.path_finalPrefabOutput, prefabPath);                                                                 // e.g., "SegmentCategory/SegmentName.prefab"
            string intermediateThumbnailSaveDirectory = Path.Combine(Paths.Directories.path_intermediateThumbnailFolder, Path.GetDirectoryName(relativePrefabPath) ?? "");          // e.g., "Assets/MaterialsAndTexturesAndMeshes/Textures/Thumbnails/SegmentCategory"
            string intermediateThumbnailSavePath = Path.Combine(intermediateThumbnailSaveDirectory, Path.GetFileNameWithoutExtension(relativePrefabPath) + "_thumbnail.asset");     // e.g., "Assets/MaterialsAndTexturesAndMeshes/Textures/Thumbnails/SegmentCategory/SegmentName.asset"

            // Load the prefab asset.
            GameObject prefabAssetReference = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            // Wait until the asset preview of the prefab at the given path to be ready and then store it as a Texture2D.
            Texture2D previewTexture = AssetPreview.GetAssetPreview(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            while (AssetPreview.IsLoadingAssetPreview(prefabAssetReference.GetInstanceID()))
            {
                await Task.Yield();
            }
            previewTexture = AssetPreview.GetAssetPreview(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));

            // Make a new Texture2D and copy pixels from the preview. This ensures the texture is persistent and not a temporary one managed by Unity's AssetPreview system.
            Texture2D persistentTexture = new Texture2D(previewTexture.width, previewTexture.height, TextureFormat.RGBA32, false);
            persistentTexture.SetPixels(previewTexture.GetPixels());
            persistentTexture.Apply();

            // Save the thumbnail as a Texture2D asset in the intermediate thumbnail folder.
            Directory.CreateDirectory(intermediateThumbnailSaveDirectory);
            AssetDatabase.CreateAsset(persistentTexture, intermediateThumbnailSavePath);

            // Note: The above procedure once produced an error I've not been able to reproduce:
            //  Assertion failed on expression: 'nameSpace.GetDestroyedObjectsPtr() == NULL'
            //  UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

            // Instantiate a new PrefabUITile from the PrefabUITile prefab and set its properties.
            GameObject prefabUITileInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetReferences.prefabInsertionWidgetTilePrefab, ObjectInstances.prefabInsertionWidgetInstance_PrefabTileContainer);
            prefabUITileInstance.name = AssetReferences.prefabInsertionWidgetTilePrefab.name + " - " + prefabAssetReference.name;
            // Causes an error - //prefabUITileInstance.GetComponent<PrefabUITileController>().Initialise(prefabAssetReference.name, prefabPath, AssetDatabase.LoadAssetAtPath<Texture2D>(intermediateThumbnailSavePath));

            //Note: In general, using this class has and can still produce errors if the Editor is having a bad day or is currently doing something that interferes with it.
        }
    }
}
#endif