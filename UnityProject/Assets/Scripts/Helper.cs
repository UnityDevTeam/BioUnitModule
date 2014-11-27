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

    // Calculates new MVP matrix from main camera
    public static Matrix4x4 GetMVPMatrix()
    {
        Matrix4x4 V = Camera.main.worldToCameraMatrix;
        return GetProjectionMatrix() * V;
    }

	public static Matrix4x4 GetProjectionMatrix()
	{
		bool d3d = SystemInfo.graphicsDeviceVersion.IndexOf("Direct3D") > -1;		
		Matrix4x4 P = Camera.main.projectionMatrix;
		
//		if (d3d)
//		{
//			// Invert Y for rendering to a render texture
//			for ( int i = 0; i < 4; i++) { P[1,i] = -P[1,i]; }
//			// Scale and bias from OpenGL -> D3D depth range
//			for ( int i = 0; i < 4; i++) { P[2,i] = P[2,i]*0.5f + P[3,i]*0.5f;}
//		}
		
		return P;
	}
}

