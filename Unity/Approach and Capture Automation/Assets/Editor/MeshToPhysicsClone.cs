using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Unity Editor tool for creating physics-accurate clones of GameObjects with complex meshes.
/// Decomposes meshes into convex sub-meshes and generates appropriate rigidbody properties.
/// 
/// Author: Claude (Anthropic AI Assistant)
/// Created: 2025
/// </summary>
public class MeshToPhysicsClone : EditorWindow
{
    private GameObject sourceObject;
    private float rootMass = 1.0f;
    private float convexityTolerance = 0.5f; // 0 = fewer pieces (less accurate), 1 = more pieces (more accurate)
    private string savePath = "Assets/Meshes/MeshToPhysicsClone/";
    
    [MenuItem("Custom Tools/Mesh to Physics Clone (Claude Sonnet 4.5)")]
    public static void ShowWindow()
    {
        GetWindow<MeshToPhysicsClone>("Mesh to Physics Clone");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Mesh to Physics Clone Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        sourceObject = (GameObject)EditorGUILayout.ObjectField(
            "Source GameObject", 
            sourceObject, 
            typeof(GameObject), 
            true
        );
        
        rootMass = EditorGUILayout.FloatField("Root Rigidbody Mass (kg)", rootMass);
        rootMass = Mathf.Max(0.001f, rootMass);
        
        EditorGUILayout.Space();
        GUILayout.Label("Convex Decomposition Quality", EditorStyles.label);
        convexityTolerance = EditorGUILayout.Slider(convexityTolerance, 0f, 1f);
        GUILayout.Label("← Fewer pieces (faster)     More pieces (accurate) →", EditorStyles.miniLabel);
        
        EditorGUILayout.Space();
        GUILayout.Label("Mesh Save Path", EditorStyles.label);
        EditorGUILayout.BeginHorizontal();
        savePath = EditorGUILayout.TextField(savePath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.SaveFolderPanel("Select Mesh Save Folder", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    savePath = "Assets" + path.Substring(Application.dataPath.Length) + "/";
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        GUI.enabled = sourceObject != null;
        if (GUILayout.Button("Generate Physics Clone", GUILayout.Height(30)))
        {
            GeneratePhysicsClone();
        }
        GUI.enabled = true;
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "This tool creates a clone with convex mesh colliders by decomposing the source mesh. " +
            "Generated meshes will be saved to the specified path.",
            MessageType.Info
        );
    }
    
    private void GeneratePhysicsClone()
    {
        if (sourceObject == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a source GameObject.", "OK");
            return;
        }
        
        // Ensure save directory exists
        if (!System.IO.Directory.Exists(savePath))
        {
            System.IO.Directory.CreateDirectory(savePath);
        }
        
        EditorUtility.DisplayProgressBar("Generating Physics Clone", "Starting...", 0f);
        
        try
        {
            // Create the clone
            GameObject clone = Instantiate(sourceObject);
            clone.name = sourceObject.name + "_SeparatedMesh";
            clone.transform.position = sourceObject.transform.position;
            clone.transform.rotation = sourceObject.transform.rotation;
            clone.transform.localScale = sourceObject.transform.localScale;
            
            // Find all rigidbodies in the hierarchy
            Rigidbody[] rigidbodies = clone.GetComponentsInChildren<Rigidbody>(true);
            
            if (rigidbodies.Length > 0)
            {
                // Calculate uniform density from root rigidbody
                Rigidbody rootRB = clone.GetComponent<Rigidbody>();
                if (rootRB == null) rootRB = rigidbodies[0];
                
                float rootVolume = CalculateHierarchyVolume(rootRB.gameObject, rigidbodies);
                float density = rootVolume > 0 ? rootMass / rootVolume : 1.0f;
                
                // Process each rigidbody
                for (int i = 0; i < rigidbodies.Length; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "Generating Physics Clone", 
                        $"Processing Rigidbody {i + 1}/{rigidbodies.Length}...", 
                        (float)i / rigidbodies.Length
                    );
                    
                    ProcessRigidbody(rigidbodies[i], density, rigidbodies);
                }
            }
            
            // Process all objects with meshes to generate convex colliders
            ProcessMeshHierarchy(clone.transform);
            
            Undo.RegisterCreatedObjectUndo(clone, "Create Physics Clone");
            Selection.activeGameObject = clone;
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog(
                "Success", 
                "Physics clone generated successfully!", 
                "OK"
            );
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to generate physics clone:\n{e.Message}", "OK");
            Debug.LogError($"Physics clone generation error: {e}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
    
    private void ProcessRigidbody(Rigidbody rb, float density, Rigidbody[] allRigidbodies)
    {
        // Calculate volume for this rigidbody's zone of influence
        float volume = CalculateHierarchyVolume(rb.gameObject, allRigidbodies);
        
        // Set mass based on density
        if (volume > 0)
        {
            rb.mass = density * volume;
        }
        
        // Let Unity automatically calculate inertia tensor from colliders
        rb.automaticInertiaTensor = true;
    }
    
    private float CalculateHierarchyVolume(GameObject root, Rigidbody[] allRigidbodies)
    {
        float totalVolume = 0f;
        CalculateVolumeRecursive(root.transform, root.GetComponent<Rigidbody>(), allRigidbodies, ref totalVolume);
        return totalVolume;
    }
    
    private void CalculateVolumeRecursive(Transform current, Rigidbody parentRB, Rigidbody[] allRigidbodies, ref float totalVolume)
    {
        // Check if this object has a rigidbody that's different from parent
        Rigidbody currentRB = current.GetComponent<Rigidbody>();
        if (currentRB != null && currentRB != parentRB)
        {
            // Stop here - this is a different rigidbody zone
            return;
        }
        
        // Add this object's mesh volume
        MeshFilter meshFilter = current.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinnedMesh = current.GetComponent<SkinnedMeshRenderer>();
        
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            totalVolume += CalculateMeshVolume(meshFilter.sharedMesh, current);
        }
        else if (skinnedMesh != null && skinnedMesh.sharedMesh != null)
        {
            totalVolume += CalculateMeshVolume(skinnedMesh.sharedMesh, current);
        }
        
        // Recurse to children
        foreach (Transform child in current)
        {
            CalculateVolumeRecursive(child, parentRB, allRigidbodies, ref totalVolume);
        }
    }
    
    private float CalculateMeshVolume(Mesh mesh, Transform transform)
    {
        // Calculate volume using signed volume of tetrahedra method
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        
        float volume = 0f;
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v1 = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 v3 = transform.TransformPoint(vertices[triangles[i + 2]]);
            
            volume += SignedVolumeOfTriangle(v1, v2, v3);
        }
        
        return Mathf.Abs(volume);
    }
    
    private float SignedVolumeOfTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return Vector3.Dot(p1, Vector3.Cross(p2, p3)) / 6.0f;
    }
    
    private void ProcessMeshHierarchy(Transform root)
    {
        ProcessMeshRecursive(root);
    }
    
    private void ProcessMeshRecursive(Transform current)
    {
        // Check if this object has a mesh
        MeshFilter meshFilter = current.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinnedMesh = current.GetComponent<SkinnedMeshRenderer>();
        
        Mesh mesh = null;
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            mesh = meshFilter.sharedMesh;
        }
        else if (skinnedMesh != null && skinnedMesh.sharedMesh != null)
        {
            mesh = skinnedMesh.sharedMesh;
        }
        
        if (mesh != null)
        {
            // Remove any existing colliders
            Collider[] existingColliders = current.GetComponents<Collider>();
            foreach (Collider col in existingColliders)
            {
                DestroyImmediate(col);
            }
            
            // Create collider child object
            GameObject colliderObject = new GameObject(current.name + "_Colliders");
            colliderObject.transform.SetParent(current, false);
            colliderObject.transform.SetAsFirstSibling();
            colliderObject.layer = current.gameObject.layer;
            
            // Decompose mesh into convex pieces
            DecomposeMeshIntoConvexPieces(mesh, colliderObject, current);
        }
        
        // Recurse to children
        foreach (Transform child in current)
        {
            ProcessMeshRecursive(child);
        }
    }
    
    private void DecomposeMeshIntoConvexPieces(Mesh originalMesh, GameObject colliderObject, Transform originalTransform)
    {
        // Get mesh data in local space
        Vector3[] vertices = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;
        
        Debug.Log($"Decomposing mesh '{originalMesh.name}' with {vertices.Length} vertices and {triangles.Length / 3} triangles");
        
        // Calculate maximum concavity threshold based on quality slider
        float concavityThreshold = Mathf.Lerp(0.3f, 0.05f, convexityTolerance);
        
        // Perform convex decomposition
        List<ConvexPiece> pieces = PerformConvexDecomposition(vertices, triangles, concavityThreshold);
        
        Debug.Log($"Decomposed into {pieces.Count} convex pieces");
        
        // Create mesh assets and colliders for each piece
        for (int i = 0; i < pieces.Count; i++)
        {
            CreateConvexCollider(pieces[i], colliderObject, originalMesh.name, i);
        }
    }
    
    private List<ConvexPiece> PerformConvexDecomposition(Vector3[] vertices, int[] triangles, float concavityThreshold)
    {
        List<ConvexPiece> pieces = new List<ConvexPiece>();
        
        // Start with the entire mesh as one piece
        ConvexPiece initialPiece = new ConvexPiece();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            initialPiece.AddTriangle(
                vertices[triangles[i]],
                vertices[triangles[i + 1]],
                vertices[triangles[i + 2]],
                triangles[i],
                triangles[i + 1],
                triangles[i + 2]
            );
        }
        
        Queue<ConvexPiece> toProcess = new Queue<ConvexPiece>();
        toProcess.Enqueue(initialPiece);
        
        int maxIterations = Mathf.RoundToInt(Mathf.Lerp(10, 100, convexityTolerance));
        int iterations = 0;
        
        while (toProcess.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            ConvexPiece piece = toProcess.Dequeue();
            
            // Check if this piece is convex enough
            float concavity = CalculatePieceConcavity(piece);
            
            if (concavity < concavityThreshold || piece.triangleCount < 4)
            {
                // This piece is convex enough
                pieces.Add(piece);
            }
            else
            {
                // Split this piece
                var splitPieces = SplitPiece(piece);
                if (splitPieces != null && splitPieces.Count == 2)
                {
                    toProcess.Enqueue(splitPieces[0]);
                    toProcess.Enqueue(splitPieces[1]);
                }
                else
                {
                    // Couldn't split, accept as is
                    pieces.Add(piece);
                }
            }
        }
        
        // Add any remaining pieces that weren't processed
        while (toProcess.Count > 0)
        {
            pieces.Add(toProcess.Dequeue());
        }
        
        return pieces;
    }
    
    private float CalculatePieceConcavity(ConvexPiece piece)
    {
        if (piece.vertices.Count < 4) return 0f;
        
        // Calculate convex hull
        Bounds bounds = new Bounds(piece.vertices[0], Vector3.zero);
        foreach (var v in piece.vertices)
        {
            bounds.Encapsulate(v);
        }
        
        // Simple concavity measure: ratio of actual volume to bounding box volume
        float boundingVolume = bounds.size.x * bounds.size.y * bounds.size.z;
        float actualVolume = Mathf.Abs(CalculatePieceVolume(piece));
        
        if (boundingVolume < 0.0001f) return 0f;
        
        float volumeRatio = actualVolume / boundingVolume;
        return 1f - volumeRatio; // Higher = more concave
    }
    
    private float CalculatePieceVolume(ConvexPiece piece)
    {
        float volume = 0f;
        for (int i = 0; i < piece.triangleCount; i++)
        {
            int idx = i * 3;
            volume += SignedVolumeOfTriangle(
                piece.vertices[piece.indices[idx]],
                piece.vertices[piece.indices[idx + 1]],
                piece.vertices[piece.indices[idx + 2]]
            );
        }
        return volume;
    }
    
    private List<ConvexPiece> SplitPiece(ConvexPiece piece)
    {
        // Find best splitting plane using PCA or longest axis
        Bounds bounds = new Bounds(piece.vertices[0], Vector3.zero);
        foreach (var v in piece.vertices)
        {
            bounds.Encapsulate(v);
        }
        
        // Split along longest axis
        Vector3 size = bounds.size;
        int axis = 0; // 0=X, 1=Y, 2=Z
        if (size.y > size.x && size.y > size.z) axis = 1;
        else if (size.z > size.x && size.z > size.y) axis = 2;
        
        float splitPos = bounds.center[axis];
        
        ConvexPiece piece1 = new ConvexPiece();
        ConvexPiece piece2 = new ConvexPiece();
        
        // Distribute triangles based on centroid position
        for (int i = 0; i < piece.triangleCount; i++)
        {
            int idx = i * 3;
            Vector3 v0 = piece.vertices[piece.indices[idx]];
            Vector3 v1 = piece.vertices[piece.indices[idx + 1]];
            Vector3 v2 = piece.vertices[piece.indices[idx + 2]];
            
            Vector3 centroid = (v0 + v1 + v2) / 3f;
            
            if (centroid[axis] < splitPos)
            {
                piece1.AddTriangle(v0, v1, v2, 
                    piece.originalIndices[idx],
                    piece.originalIndices[idx + 1],
                    piece.originalIndices[idx + 2]);
            }
            else
            {
                piece2.AddTriangle(v0, v1, v2,
                    piece.originalIndices[idx],
                    piece.originalIndices[idx + 1],
                    piece.originalIndices[idx + 2]);
            }
        }
        
        if (piece1.triangleCount > 0 && piece2.triangleCount > 0)
        {
            return new List<ConvexPiece> { piece1, piece2 };
        }
        
        return null;
    }
    
    private void CreateConvexCollider(ConvexPiece piece, GameObject parent, string meshName, int index)
    {
        // Validate piece has enough vertices
        if (piece.vertices.Count < 4)
        {
            Debug.LogWarning($"Skipping piece {index} - only has {piece.vertices.Count} vertices (need at least 4)");
            return;
        }
        
        // Check for degenerate vertices (all at same position)
        Bounds pieceBounds = new Bounds(piece.vertices[0], Vector3.zero);
        foreach (var v in piece.vertices)
        {
            pieceBounds.Encapsulate(v);
        }
        
        if (pieceBounds.size.magnitude < 0.0001f)
        {
            Debug.LogWarning($"Skipping piece {index} - degenerate geometry (all vertices at same position)");
            return;
        }
        
        // Create child GameObject for this collider
        GameObject colliderChild = new GameObject($"ConvexPiece_{index}");
        colliderChild.transform.SetParent(parent.transform, false);
        colliderChild.layer = parent.layer;
        
        // Create mesh from piece
        Mesh convexMesh = new Mesh();
        convexMesh.name = $"{meshName}_Convex_{index}";
        
        // Build unique vertex list and remap indices
        convexMesh.vertices = piece.vertices.ToArray();
        convexMesh.triangles = piece.indices.ToArray();
        
        // Validate mesh
        if (convexMesh.triangles.Length < 12) // Need at least 4 triangles for tetrahedron
        {
            Debug.LogWarning($"Skipping piece {index} - insufficient triangles ({convexMesh.triangles.Length / 3})");
            DestroyImmediate(colliderChild);
            return;
        }
        
        convexMesh.RecalculateNormals();
        convexMesh.RecalculateBounds();
        
        // Additional validation: check for NaN or infinite values
        bool hasInvalidVerts = false;
        foreach (var v in convexMesh.vertices)
        {
            if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
            {
                hasInvalidVerts = true;
                break;
            }
        }
        
        if (hasInvalidVerts)
        {
            Debug.LogWarning($"Skipping piece {index} - contains invalid vertex data");
            DestroyImmediate(colliderChild);
            return;
        }
        
        // Save mesh as asset
        string assetPath = $"{savePath}{convexMesh.name}.asset";
        AssetDatabase.CreateAsset(convexMesh, assetPath);
        
        // Add MeshCollider with error handling
        try
        {
            MeshCollider meshCollider = colliderChild.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = convexMesh;
            meshCollider.convex = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to create convex collider for piece {index}: {e.Message}");
            DestroyImmediate(colliderChild);
            AssetDatabase.DeleteAsset(assetPath);
        }
    }
    
    private class ConvexPiece
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<int> indices = new List<int>();
        public List<int> originalIndices = new List<int>();
        public int triangleCount => indices.Count / 3;
        
        private Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();
        
        public void AddTriangle(Vector3 v0, Vector3 v1, Vector3 v2, int origIdx0, int origIdx1, int origIdx2)
        {
            indices.Add(GetOrAddVertex(v0));
            indices.Add(GetOrAddVertex(v1));
            indices.Add(GetOrAddVertex(v2));
            
            originalIndices.Add(origIdx0);
            originalIndices.Add(origIdx1);
            originalIndices.Add(origIdx2);
        }
        
        private int GetOrAddVertex(Vector3 v)
        {
            // Round to avoid floating point precision issues
            Vector3 rounded = new Vector3(
                Mathf.Round(v.x * 10000f) / 10000f,
                Mathf.Round(v.y * 10000f) / 10000f,
                Mathf.Round(v.z * 10000f) / 10000f
            );
            
            if (vertexMap.TryGetValue(rounded, out int index))
            {
                return index;
            }
            
            int newIndex = vertices.Count;
            vertices.Add(v);
            vertexMap[rounded] = newIndex;
            return newIndex;
        }
    }
}