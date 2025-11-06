using System.IO;
using System.Collections.Generic;
using UnityEngine;

//Copilot (2025) Binary STL file parsing in Unity, Microsoft Copilot. Produced with: 
//  "   Please produce a C# code snippet for use with Unity game engine: 
//      The snippet should take an .STL file as input and create a viable Mesh object
//      from its contents. Ensure the units used in the .STL file are conserved when
//      making the mesh!    "
//                                                              (Accessed: 7 May 2025)

public static class BinarySTLLoader
{
    public enum LengthUnits
    {
        Millimeters, Centimeters, Meters
    }

    public static float GetScaleFactor(LengthUnits stlBaseUnits)
    {
        switch (stlBaseUnits)
        {
            case LengthUnits.Millimeters:
                return 0.001f;          // Convert from Unity units to mm
            case LengthUnits.Centimeters:
                return 0.01f;           // Convert from Unity units to m
            case LengthUnits.Meters:
                return 1.0f;            // Unity units are already in meters (1:1)
            default:
                return 1.0f;            // Default to meters if no case matches
        }
    }

    public static Mesh LoadBinarySTL(string path, LengthUnits stlBaseUnits)
    {

        // Invert the 'Z' coordinate throughout to account for the SLDWKS-Unity triad mismatch

        float scaleFactor = GetScaleFactor(stlBaseUnits);

        if (!File.Exists(path)) return null;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();

        using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
        {
            reader.ReadBytes(80); // Skip the header
            uint triangleCount = reader.ReadUInt32();

            int vertexIndex = 0;

            for (uint i = 0; i < triangleCount; i++)
            {
                // Read and mirror normal vector
                Vector3 normal = new Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    -reader.ReadSingle()); // Mirror Z
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);

                // Read and mirror vertices
                for (int j = 0; j < 3; j++)
                {
                    Vector3 vertex = new Vector3(
                        reader.ReadSingle() * scaleFactor,
                        reader.ReadSingle() * scaleFactor,
                        -reader.ReadSingle() * scaleFactor); // Mirror Z

                    vertices.Add(vertex);
                    triangles.Add(vertexIndex++);
                }

                reader.ReadUInt16(); // Skip attribute byte count
            }

            // Fix winding order after mirroring
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int temp = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = temp;
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        mesh.RecalculateBounds();

        return mesh;
    }
}

