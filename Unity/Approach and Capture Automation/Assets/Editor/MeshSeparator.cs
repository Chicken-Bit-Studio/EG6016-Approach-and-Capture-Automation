using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// By: GameDevBox
// https://github.com/GameDevBox/Split-Combine-Mesh-Unity/blob/main/Editor/MeshSeparator.cs

public class MeshSeparator : EditorWindow
{
    [MenuItem("Custom Tools/Mesh Separator (GameDevBox)")]
    static void Init()
    {
        MeshSeparator window = GetWindow<MeshSeparator>();
        window.titleContent = new GUIContent("Mesh Separator");
        window.minSize = new Vector2(350, 300);
        window.Show();
    }

    GameObject targetObject;
    float maxAngle = 180f;
    bool keepOriginal = false;
    bool recalculateNormals = false;
    bool optimizeMeshes = true;
    bool separateByMaterial = false;
    bool generateLightmapUVs = false;
    int targetPartCount = 0; // 0 = automatic
    bool saveMeshesAsAssets = false;
    string savePath = "Assets/Meshes";

    GameObject generatedParent;

    void OnGUI()
    {
        GUILayout.Label("Separate Disconnected Mesh Parts", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Mesh Object", targetObject, typeof(GameObject), true);

        maxAngle = EditorGUILayout.Slider("Max Edge Angle", maxAngle, 0f, 180f);
        EditorGUILayout.HelpBox("Set the max angle between faces to consider them connected. 180� = any connection, 0� = perfectly smooth only.", MessageType.Info);

        targetPartCount = EditorGUILayout.IntField("Target Part Count (0=auto)", targetPartCount);
        EditorGUILayout.HelpBox("Set to 0 to automatically detect all parts, or specify how many parts you want to create.", MessageType.Info);

        keepOriginal = EditorGUILayout.Toggle("Keep Original Object", keepOriginal);
        recalculateNormals = EditorGUILayout.Toggle("Recalculate Normals", recalculateNormals);
        optimizeMeshes = EditorGUILayout.Toggle("Optimize Meshes", optimizeMeshes);
        separateByMaterial = EditorGUILayout.Toggle("Separate by Material", separateByMaterial);
        generateLightmapUVs = EditorGUILayout.Toggle("Generate Lightmap UVs", generateLightmapUVs);

        saveMeshesAsAssets = EditorGUILayout.Toggle("Save Meshes as Assets", saveMeshesAsAssets);
        if (saveMeshesAsAssets)
        {
            EditorGUILayout.BeginHorizontal();
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string newPath = EditorUtility.SaveFolderPanel("Select Folder to Save Meshes", savePath, "");
                if (!string.IsNullOrEmpty(newPath))
                {
                    savePath = "Assets" + newPath.Replace(Application.dataPath, "");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();

        GUI.enabled = targetObject != null;
        if (GUILayout.Button("Separate Mesh", GUILayout.Height(30)))
        {
            if (targetObject != null)
                SeparateMesh(targetObject, maxAngle);
        }
        GUI.enabled = true;

        EditorGUILayout.Space();

        GUI.enabled = generatedParent != null && saveMeshesAsAssets;
        if (GUILayout.Button("Save All Meshes as Assets", GUILayout.Height(25)))
        {
            SaveAllMeshesAsAssets();
        }
        GUI.enabled = true;
    }

    void SeparateMesh(GameObject obj, float angleThreshold)
    {
        MeshFilter mf = obj.GetComponent<MeshFilter>();
        MeshRenderer mr = obj.GetComponent<MeshRenderer>();

        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("No mesh found on the selected object!");
            return;
        }

        Mesh originalMesh = mf.sharedMesh;
        Material[] materials = mr.sharedMaterials;

        // Create a parent object for all the separated parts
        generatedParent = new GameObject(obj.name + "_Separated");
        generatedParent.transform.SetParent(obj.transform.parent);
        generatedParent.transform.position = obj.transform.position;
        generatedParent.transform.rotation = obj.transform.rotation;
        generatedParent.transform.localScale = obj.transform.localScale;

        if (separateByMaterial && materials.Length > 1)
        {
            // Separate by material first
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Mesh submeshMesh = ExtractSubmesh(originalMesh, materialIndex);
                if (submeshMesh.vertexCount == 0) continue;

                SeparateMeshParts(submeshMesh, materials[materialIndex], generatedParent.transform,
                                angleThreshold, materialIndex.ToString());
            }
        }
        else
        {
            // Separate the whole mesh by connectivity
            SeparateMeshParts(originalMesh, materials.Length > 0 ? materials[0] : null,
                             generatedParent.transform, angleThreshold, "");
        }

        // If targetPartCount is specified and we have more parts than needed, merge smallest parts
        if (targetPartCount > 0 && generatedParent.transform.childCount > targetPartCount)
        {
            MergeSmallestParts(generatedParent.transform, targetPartCount);
        }

        if (!keepOriginal)
        {
            Undo.DestroyObjectImmediate(obj);
        }
        else
        {
            Undo.RegisterCreatedObjectUndo(generatedParent, "Separate Mesh");
        }

        Debug.Log($"Mesh separation complete. Created {generatedParent.transform.childCount} parts.");
    }

    void MergeSmallestParts(Transform parent, int targetCount)
    {
        while (parent.childCount > targetCount)
        {
            // Find the two smallest parts by vertex count
            Transform smallest1 = null;
            Transform smallest2 = null;
            int smallest1Count = int.MaxValue;
            int smallest2Count = int.MaxValue;

            foreach (Transform child in parent)
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    int vertexCount = mf.sharedMesh.vertexCount;
                    if (vertexCount < smallest1Count)
                    {
                        smallest2 = smallest1;
                        smallest2Count = smallest1Count;
                        smallest1 = child;
                        smallest1Count = vertexCount;
                    }
                    else if (vertexCount < smallest2Count)
                    {
                        smallest2 = child;
                        smallest2Count = vertexCount;
                    }
                }
            }

            if (smallest1 != null && smallest2 != null)
            {
                CombineMeshes(smallest1, smallest2);
                Undo.DestroyObjectImmediate(smallest2.gameObject);
            }
            else
            {
                break;
            }
        }
    }

    void CombineMeshes(Transform target, Transform source)
    {
        MeshFilter targetMF = target.GetComponent<MeshFilter>();
        MeshFilter sourceMF = source.GetComponent<MeshFilter>();
        MeshRenderer targetMR = target.GetComponent<MeshRenderer>();
        MeshRenderer sourceMR = source.GetComponent<MeshRenderer>();

        if (targetMF == null || sourceMF == null || targetMF.sharedMesh == null || sourceMF.sharedMesh == null)
            return;

        CombineInstance[] combine = new CombineInstance[2];
        combine[0].mesh = targetMF.sharedMesh;
        combine[0].transform = target.localToWorldMatrix;
        combine[1].mesh = sourceMF.sharedMesh;
        combine[1].transform = source.localToWorldMatrix;

        Mesh newMesh = new Mesh();
        newMesh.CombineMeshes(combine, true);

        if (recalculateNormals)
        {
            newMesh.RecalculateNormals();
        }

        if (optimizeMeshes)
        {
            MeshUtility.Optimize(newMesh);
        }

        targetMF.sharedMesh = newMesh;

        // Try to maintain material
        if (targetMR != null && sourceMR != null && targetMR.sharedMaterial == null && sourceMR.sharedMaterial != null)
        {
            targetMR.sharedMaterial = sourceMR.sharedMaterial;
        }
    }

    void SeparateMeshParts(Mesh mesh, Material material, Transform parent, float angleThreshold, string suffix)
    {
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var normals = mesh.normals;
        var uvs = mesh.uv;
        var uv2s = mesh.uv2.Length > 0 ? mesh.uv2 : null;
        var uv3s = mesh.uv3.Length > 0 ? mesh.uv3 : null;
        var uv4s = mesh.uv4.Length > 0 ? mesh.uv4 : null;
        var colors = mesh.colors.Length > 0 ? mesh.colors : null;
        var tangents = mesh.tangents.Length > 0 ? mesh.tangents : null;

        // Build adjacency list with angle check
        Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        for (int i = 0; i < vertices.Length; i++)
            adjacency[i] = new List<int>();

        // Store face normals and face indices
        Vector3[] faceNormals = new Vector3[triangles.Length / 3];
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];
            faceNormals[i / 3] = Vector3.Cross(v1 - v0, v2 - v0).normalized;
        }

        // Map vertex to faces
        Dictionary<int, List<int>> vertexToFaces = new Dictionary<int, List<int>>();
        for (int i = 0; i < vertices.Length; i++)
            vertexToFaces[i] = new List<int>();

        for (int i = 0; i < triangles.Length; i++)
            vertexToFaces[triangles[i]].Add(i / 3);

        // Connect vertices if any shared face is within the angle threshold
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];

            TryConnect(a, b, vertexToFaces, faceNormals, adjacency, angleThreshold);
            TryConnect(b, c, vertexToFaces, faceNormals, adjacency, angleThreshold);
            TryConnect(c, a, vertexToFaces, faceNormals, adjacency, angleThreshold);
        }

        // BFS to find connected groups
        bool[] visited = new bool[vertices.Length];
        List<List<int>> groups = new List<List<int>>();

        for (int i = 0; i < vertices.Length; i++)
        {
            if (visited[i]) continue;

            List<int> group = new List<int>();
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                group.Add(v);
                foreach (var neighbor in adjacency[v])
                {
                    if (!visited[neighbor])
                    {
                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Only keep groups that have faces (vertices used in triangles)
            if (group.Any(v => vertexToFaces[v].Count > 0))
            {
                groups.Add(group);
            }
        }

        if (groups.Count <= 1)
        {
            Debug.Log("No separate parts found with current angle threshold.");
            return;
        }

        // Sort groups by size (vertex count) descending
        groups = groups.OrderByDescending(g => g.Count).ToList();

        // Create new GameObjects for each group
        for (int g = 0; g < groups.Count; g++)
        {
            List<int> group = groups[g];
            Dictionary<int, int> indexMap = new Dictionary<int, int>();

            Vector3[] newVertices = new Vector3[group.Count];
            Vector3[] newNormals = normals.Length > 0 ? new Vector3[group.Count] : null;
            Vector2[] newUVs = uvs.Length > 0 ? new Vector2[group.Count] : null;
            Vector2[] newUV2s = uv2s != null ? new Vector2[group.Count] : null;
            Vector2[] newUV3s = uv3s != null ? new Vector2[group.Count] : null;
            Vector2[] newUV4s = uv4s != null ? new Vector2[group.Count] : null;
            Color[] newColors = colors != null ? new Color[group.Count] : null;
            Vector4[] newTangents = tangents != null ? new Vector4[group.Count] : null;

            for (int vi = 0; vi < group.Count; vi++)
            {
                int oldIndex = group[vi];
                indexMap[oldIndex] = vi;
                newVertices[vi] = vertices[oldIndex];
                if (newNormals != null) newNormals[vi] = normals[oldIndex];
                if (newUVs != null) newUVs[vi] = uvs[oldIndex];
                if (newUV2s != null) newUV2s[vi] = uv2s[oldIndex];
                if (newUV3s != null) newUV3s[vi] = uv3s[oldIndex];
                if (newUV4s != null) newUV4s[vi] = uv4s[oldIndex];
                if (newColors != null) newColors[vi] = colors[oldIndex];
                if (newTangents != null) newTangents[vi] = tangents[oldIndex];
            }

            List<int> newTriangles = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                if (indexMap.ContainsKey(a) && indexMap.ContainsKey(b) && indexMap.ContainsKey(c))
                {
                    newTriangles.Add(indexMap[a]);
                    newTriangles.Add(indexMap[b]);
                    newTriangles.Add(indexMap[c]);
                }
            }

            if (newTriangles.Count == 0) continue; // Skip empty meshes

            Mesh newMesh = new Mesh();
            newMesh.vertices = newVertices;
            if (newNormals != null) newMesh.normals = newNormals;
            if (newUVs != null) newMesh.uv = newUVs;
            if (newUV2s != null) newMesh.uv2 = newUV2s;
            if (newUV3s != null) newMesh.uv3 = newUV3s;
            if (newUV4s != null) newMesh.uv4 = newUV4s;
            if (newColors != null) newMesh.colors = newColors;
            if (newTangents != null) newMesh.tangents = newTangents;
            newMesh.triangles = newTriangles.ToArray();


            if (generateLightmapUVs)
            {
                Unwrapping.GenerateSecondaryUVSet(newMesh);
            }

            if (recalculateNormals)
            {
                newMesh.RecalculateNormals();
            }

            newMesh.RecalculateBounds();

            if (optimizeMeshes)
            {
                MeshUtility.Optimize(newMesh);
            }

            string partName = $"{parent.name}_Part{g + 1}";
            if (!string.IsNullOrEmpty(suffix))
            {
                partName += $"_{suffix}";
            }

            GameObject newObj = new GameObject(partName);
            newObj.transform.SetParent(parent);
            newObj.transform.localPosition = Vector3.zero;
            newObj.transform.localRotation = Quaternion.identity;
            newObj.transform.localScale = Vector3.one;

            MeshFilter newMF = newObj.AddComponent<MeshFilter>();
            newMF.sharedMesh = newMesh;

            MeshRenderer newMR = newObj.AddComponent<MeshRenderer>();
            newMR.sharedMaterial = material;

            Undo.RegisterCreatedObjectUndo(newObj, "Create Mesh Part");
        }
    }

    void SaveAllMeshesAsAssets()
    {
        if (generatedParent == null)
        {
            Debug.LogWarning("No generated meshes to save.");
            return;
        }

        // Ensure directory exists
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        int savedCount = 0;
        foreach (Transform child in generatedParent.transform)
        {
            MeshFilter mf = child.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                string meshName = child.name;
                string assetPath = Path.Combine(savePath, meshName + ".asset");

                // Make sure the path is unique
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                Mesh meshToSave = Instantiate(mf.sharedMesh);

                if (generateLightmapUVs && meshToSave.uv2.Length == 0)
                {
                    Unwrapping.GenerateSecondaryUVSet(meshToSave);
                }

                AssetDatabase.CreateAsset(meshToSave, assetPath);
                savedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Saved {savedCount} meshes to {savePath}");
        EditorUtility.FocusProjectWindow();
    }

    Mesh ExtractSubmesh(Mesh mesh, int submeshIndex)
    {
        if (submeshIndex >= mesh.subMeshCount)
            return new Mesh();

        int[] submeshTriangles = mesh.GetTriangles(submeshIndex);
        if (submeshTriangles.Length == 0)
            return new Mesh();

        // Get all unique vertices in the submesh
        HashSet<int> vertexIndices = new HashSet<int>(submeshTriangles);

        // Create mapping from original index to new index
        Dictionary<int, int> indexMap = new Dictionary<int, int>();
        Vector3[] newVertices = new Vector3[vertexIndices.Count];
        Vector3[] newNormals = mesh.normals.Length > 0 ? new Vector3[vertexIndices.Count] : null;
        Vector2[] newUVs = mesh.uv.Length > 0 ? new Vector2[vertexIndices.Count] : null;

        int newIndex = 0;
        foreach (int oldIndex in vertexIndices)
        {
            indexMap[oldIndex] = newIndex;
            newVertices[newIndex] = mesh.vertices[oldIndex];
            if (newNormals != null) newNormals[newIndex] = mesh.normals[oldIndex];
            if (newUVs != null) newUVs[newIndex] = mesh.uv[oldIndex];
            newIndex++;
        }

        // Remap triangles
        int[] newTriangles = new int[submeshTriangles.Length];
        for (int i = 0; i < submeshTriangles.Length; i++)
        {
            newTriangles[i] = indexMap[submeshTriangles[i]];
        }

        Mesh submesh = new Mesh();
        submesh.vertices = newVertices;
        if (newNormals != null) submesh.normals = newNormals;
        if (newUVs != null) submesh.uv = newUVs;
        submesh.triangles = newTriangles;
        submesh.RecalculateBounds();

        return submesh;
    }

    void TryConnect(int v1, int v2, Dictionary<int, List<int>> vertexToFaces, Vector3[] faceNormals, Dictionary<int, List<int>> adjacency, float angleThreshold)
    {
        foreach (int f1 in vertexToFaces[v1])
        {
            foreach (int f2 in vertexToFaces[v2])
            {
                float angle = Vector3.Angle(faceNormals[f1], faceNormals[f2]);
                if (angle <= angleThreshold)
                {
                    if (!adjacency[v1].Contains(v2)) adjacency[v1].Add(v2);
                    if (!adjacency[v2].Contains(v1)) adjacency[v2].Add(v1);
                    return; // connected, no need to check more
                }
            }
        }
    }
}