using System;
using UnityEngine;

public static class Helper
{
    public static Vector4 QuanternionToVector4(Quaternion q)
    {
        return new Vector4(q.x, q.y, q.z, q.z);
    }

    public static void QuanternionIntoVector4(Quaternion q, ref Vector4 v)
    {
        v.Set(q.x, q.y, q.z, q.z);
    }

    public static Vector3 FloatArrayToVector3(float[] floatArray)
    {
        return new Vector3(floatArray[0], floatArray[1], floatArray[2]);
    }

    public static void FloatArrayIntoVector3(float[] floatArray, ref Vector3 inVector) 
    {
        inVector.Set(floatArray[0], floatArray[1], floatArray[2]);
    }

    public static Vector4 FloatArrayToVector4(float[] floatArray, float z = 1)
    {
        return new Vector4(floatArray[0], floatArray[1], floatArray[2], z);
    }

    public static void FloatArrayIntoVector4(float[] floatArray, ref Vector4 inVector, float z = 1)
    {
        inVector.Set(floatArray[0], floatArray[1], floatArray[2], z);
    }
}

