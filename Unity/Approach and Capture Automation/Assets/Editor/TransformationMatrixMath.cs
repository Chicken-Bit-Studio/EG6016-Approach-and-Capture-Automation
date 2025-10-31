using UnityEngine;

public static class TransformationMatrixMath
{
    // This rework of TransformationMatrixMath assumes the input transformation matrix has been flattened into a 1-dimensional, 16-element float array.
    // This change was neccesary because Unity cannot serialise multidimensional arrays, so float[,] notation didn't work, pretty as it was.

    // https://help.solidworks.com/2020/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IMathTransform.html
    // This document breaks down the principles behind transformation matrices. SOLIDWORKS supplies this as a flat array in a particular layout, so be careful.
    public static Vector3 ExtractPosition(float[] matrix)
    {
        // We don't need to scale this. SldWorks already accounts for the model units in the transformation matrix.
        // Assumes row-major flattened 4x4: index = row * 4 + col
        return new Vector3(matrix[9], matrix[10], matrix[11]);
    }
    public static Quaternion ExtractQuaternion(float[] matrix)
    {
        // Directly extract the rotation matrix elements from the flattened 4x4 array.
        // row-major: [0 1 2 3; 4 5 6 7; 8 9 10 11; 12 13 14 15]
        float m00 = matrix[0]; float m01 = matrix[1]; float m02 = matrix[2];
        float m10 = matrix[3]; float m11 = matrix[4]; float m12 = matrix[5];
        float m20 = matrix[6]; float m21 = matrix[7]; float m22 = matrix[8];

        // Standard algorithm to convert a 3x3 rotation matrix to a Quaternion.
        float trace = m00 + m11 + m22;
        Quaternion q = new Quaternion();

        if (trace > 0.0f)
        {
            float s = Mathf.Sqrt(trace + 1.0f) * 2.0f; // s = 4 * q.w
            q.w = 0.25f * s;
            q.x = (m21 - m12) / s;
            q.y = (m02 - m20) / s;
            q.z = (m10 - m01) / s;
        }
        else if (m00 > m11 && m00 > m22)
        {
            float s = Mathf.Sqrt(1.0f + m00 - m11 - m22) * 2.0f; // s = 4 * q.x
            q.w = (m21 - m12) / s;
            q.x = 0.25f * s;
            q.y = (m01 + m10) / s;
            q.z = (m02 + m20) / s;
        }
        else if (m11 > m22)
        {
            float s = Mathf.Sqrt(1.0f + m11 - m00 - m22) * 2.0f; // s = 4 * q.y
            q.w = (m02 - m20) / s;
            q.x = (m01 + m10) / s;
            q.y = 0.25f * s;
            q.z = (m12 + m21) / s;
        }
        else
        {
            float s = Mathf.Sqrt(1.0f + m22 - m00 - m11) * 2.0f; // s = 4 * q.z
            q.w = (m10 - m01) / s;
            q.x = (m02 + m20) / s;
            q.y = (m12 + m21) / s;
            q.z = 0.25f * s;
        }

        return q;
    }
}
