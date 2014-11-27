using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

enum MolState
{
    Null = -1,           // Molecule will not be displayed
    Normal = 0,          // Molecule will be displayed with normal color
    Highlighted = 1      // Molecule will be displayed with highlighted color
};

//[ExecuteInEditMode]
public class CameraScript : MonoBehaviour
{
    private int _previousScreenWidth = 0;
	private int _previousScreenHeight = 0;

    /*****/

    private ComputeBuffer _drawArgsBuffer;
    private ComputeBuffer _atomDataBuffer;
    private ComputeBuffer _atomDataPdbBuffer;
    private ComputeBuffer _molColorsBuffer;
	private ComputeBuffer _molAtomCountBuffer;
	private ComputeBuffer _molAtomStartBuffer;
    private ComputeBuffer _molPositionsBuffer;
    private ComputeBuffer _molRotationsBuffer;
    private ComputeBuffer _molStatesBuffer;
    private ComputeBuffer _molTypesBuffer;

    /*****/

    public Shader MolShader;
    public Material AoMaterial;

    private Material _molMaterial;
    private Material _dofMaterial;

    [HideInInspector]
    public List<string> MolNames;
    
    public Color[] MolColors = new Color[0];

    /*****/

    [RangeAttribute(0, 1)]
    public float molScale = 0.015f;

    [RangeAttribute(0, 3)]
    public float intensity = 0.5f;

    [RangeAttribute(0.1f, 3)]
    public float radius = 0.2f;

    [RangeAttribute(0, 2)]
    public float sharpness = 1.0f;

    [RangeAttribute(0, 3)]
    public int blurIterations = 1;

    [RangeAttribute(0, 5)]
    public float blurFilterDistance = 1.25f;

    /*****/

	public void Start()
	{
        if (_drawArgsBuffer == null)
		{
			_drawArgsBuffer = new ComputeBuffer (1, 16, ComputeBufferType.DrawIndirect);
			var args = new []{0,1,0,0};
			_drawArgsBuffer.SetData (args);
		}
        
        if (_molPositionsBuffer == null) 
            _molPositionsBuffer = new ComputeBuffer(MainScript.NumMolObjectMax, 16);

        if (_molRotationsBuffer == null)
            _molRotationsBuffer = new ComputeBuffer(MainScript.NumMolObjectMax, 16);

        if (_molStatesBuffer == null)
            _molStatesBuffer = new ComputeBuffer(MainScript.NumMolObjectMax, 4);

        if (_molTypesBuffer == null)
            _molTypesBuffer = new ComputeBuffer(MainScript.NumMolObjectMax, 4);

        if (_molMaterial == null)
        {
            _molMaterial = new Material(MolShader);
            _molMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
	}

    public void AddMoleculeType(string pdbName, Color color)
    {
        var path = Application.dataPath + "/Molecules/" + pdbName + ".pdb";
        
        if(!File.Exists(path)) throw new Exception("Pdb file does not exists");

        var atoms = PdbReader.ReadPdbFile(path);

        var atomCount = new List<int>();
        if (_molAtomCountBuffer != null)  // if buffer alreay exists fetch information from the buffer
        {
            var temp = new int[_molAtomCountBuffer.count];
            _molAtomCountBuffer.GetData(temp);
            atomCount = new List<int>(temp);
        }
        
        var atomStart = new List<int>();
        if (_molAtomStartBuffer != null)
        {
            var temp = new int[_molAtomStartBuffer.count];
            _molAtomStartBuffer.GetData(temp);
            atomStart = new List<int>(temp);
        }
        
        var atomDataPdb = new List<Vector4>();
        if (_atomDataPdbBuffer != null)
        {
            var temp = new Vector4[_atomDataPdbBuffer.count];
            _atomDataPdbBuffer.GetData(temp);
            atomDataPdb = new List<Vector4>(temp);
        }

        atomCount.Add(atoms.Count);
        atomStart.Add(atomDataPdb.Count);
        atomDataPdb.AddRange(atoms);

        if(_molAtomCountBuffer != null) _molAtomCountBuffer.Release();
        _molAtomCountBuffer = new ComputeBuffer(atomCount.Count, 4);
        _molAtomCountBuffer.SetData(atomCount.ToArray());

        if (_molAtomStartBuffer != null) _molAtomCountBuffer.Release();
        _molAtomStartBuffer = new ComputeBuffer(atomStart.Count, 4);
        _molAtomStartBuffer.SetData(atomStart.ToArray());

        if (_atomDataPdbBuffer != null) _molAtomCountBuffer.Release();
        _atomDataPdbBuffer = new ComputeBuffer(atomDataPdb.Count, 16);
        _atomDataPdbBuffer.SetData(atomDataPdb.ToArray());

        MolNames.Add(name);

        Array.Resize(ref MolColors, MolColors.Length + 1);
        MolColors[MolColors.Length - 1] = color;
    }

    public void UpdateMoleculeData( Vector4[] positions, Vector4[] rotations, int[] types, int[] states)
    {
        _molPositionsBuffer.SetData(positions);
        _molRotationsBuffer.SetData(rotations);
        _molTypesBuffer.SetData(types);
        _molStatesBuffer.SetData(states);
    }

    void OnDisable()
    {
        ReleaseResources();
    }

	private void ReleaseResources ()
	{
		if (_drawArgsBuffer != null) _drawArgsBuffer.Release ();
		if (_atomDataBuffer != null) _atomDataBuffer.Release(); 
		if (_atomDataPdbBuffer != null) _atomDataPdbBuffer.Release(); 
		if (_molAtomCountBuffer != null) _molAtomCountBuffer.Release(); 
		if (_molAtomStartBuffer != null) _molAtomStartBuffer.Release(); 
		if (_molPositionsBuffer != null) _molPositionsBuffer.Release(); 
		if (_molRotationsBuffer != null) _molRotationsBuffer.Release();
		if (_molTypesBuffer != null) _molTypesBuffer.Release();
		if (_molStatesBuffer != null) _molStatesBuffer.Release(); 
        if (_molColorsBuffer != null) _molColorsBuffer.Release();

		DestroyImmediate (_molMaterial);
		DestroyImmediate (_dofMaterial);
	}

    private void CheckScreenSize()
    {
        if (Screen.width != _previousScreenWidth || Screen.height != _previousScreenHeight)
        {
            if (_atomDataBuffer != null) _atomDataBuffer.Release();
            _atomDataBuffer = new ComputeBuffer(Screen.width * Screen.height, 28, ComputeBufferType.Append);

            _previousScreenWidth = Screen.width;
            _previousScreenHeight = Screen.height;
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        CheckScreenSize ();

        if (_molColorsBuffer == null || _molColorsBuffer.count != MolColors.Length) _molColorsBuffer = new ComputeBuffer(MolColors.Length, sizeof(float) * 4);
        _molColorsBuffer.SetData(MolColors);

        //*****//

		_molMaterial.SetFloat ("molScale", molScale);		
		_molMaterial.SetVector ("viewDirection", Camera.main.transform.forward);
        
		_molMaterial.SetBuffer ("atomDataBuffer", _atomDataBuffer);
		_molMaterial.SetBuffer ("atomDataPDBBuffer", _atomDataPdbBuffer);
		_molMaterial.SetBuffer ("molAtomCountBuffer", _molAtomCountBuffer);		
		_molMaterial.SetBuffer ("molAtomStartBuffer", _molAtomStartBuffer);
		_molMaterial.SetBuffer ("molPositions", _molPositionsBuffer);
		_molMaterial.SetBuffer ("molRotations", _molRotationsBuffer);
		_molMaterial.SetBuffer ("molStates", _molStatesBuffer);
		_molMaterial.SetBuffer ("molTypes", _molTypesBuffer);
        _molMaterial.SetBuffer ("molColors", _molColorsBuffer);

		//*****//

		RenderTexture tmpRenderTex = RenderTexture.GetTemporary (src.width, src.height, 0, RenderTextureFormat.ARGBFloat);
		RenderTexture tmpRenderTex2 = RenderTexture.GetTemporary (src.width, src.height, 0, RenderTextureFormat.ARGBFloat);
		RenderTexture tmpDepthBuffer = RenderTexture.GetTemporary (src.width, src.height, 32, RenderTextureFormat.Depth);
		
		Graphics.SetRenderTarget (tmpRenderTex);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		
		Graphics.SetRenderTarget (tmpRenderTex2);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		
		Graphics.SetRenderTarget (tmpDepthBuffer);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		
		var renderBuffer = new RenderBuffer[2];
		renderBuffer[0] = tmpRenderTex.colorBuffer;
		renderBuffer[1] = tmpRenderTex2.colorBuffer;
		
		Graphics.SetRenderTarget (renderBuffer, tmpDepthBuffer.depthBuffer);		
		
		_molMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Points, MainScript.NumMolObjectMax);
		
		_molMaterial.SetTexture ("posTex", tmpRenderTex);
		_molMaterial.SetTexture ("infoTex", tmpRenderTex2);
		Graphics.SetRandomWriteTarget (1, _atomDataBuffer);
		Graphics.Blit (src, dst, _molMaterial, 1);
		Graphics.ClearRandomWriteTargets ();		
		ComputeBuffer.CopyCount (_atomDataBuffer, _drawArgsBuffer, 0);

		RenderTexture.ReleaseTemporary (tmpRenderTex);
		RenderTexture.ReleaseTemporary (tmpRenderTex2);
		RenderTexture.ReleaseTemporary (tmpDepthBuffer);

		//*****//

		tmpRenderTex = RenderTexture.GetTemporary (src.width, src.height, 0, RenderTextureFormat.ARGB32);
		tmpRenderTex2 = RenderTexture.GetTemporary (src.width, src.height, 0, RenderTextureFormat.ARGB32);
//		tmpColorPing = RenderTexture.GetTemporary (src.width, src.height, 0, RenderTextureFormat.ARGBFloat);
//		tmpColorPong = RenderTexture.GetTemporary (src.width, src.height, 0, RenderTextureFormat.ARGBFloat);
		tmpDepthBuffer = RenderTexture.GetTemporary (src.width, src.height, 32, RenderTextureFormat.Depth);
		RenderTexture tmpIDBuffer = RenderTexture.GetTemporary (src.width, src.height, 0, RenderTextureFormat.RFloat);

		Graphics.SetRenderTarget (tmpRenderTex);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
        Graphics.Blit(src, tmpRenderTex);

		Graphics.SetRenderTarget (tmpRenderTex2);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		
		Graphics.SetRenderTarget (tmpDepthBuffer);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		
		Graphics.SetRenderTarget (tmpIDBuffer);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));

		renderBuffer[0] = tmpRenderTex.colorBuffer;
		renderBuffer[1] = tmpIDBuffer.colorBuffer;

		Graphics.SetRenderTarget (renderBuffer, tmpDepthBuffer.depthBuffer);
		
		_molMaterial.SetPass(2);
		Graphics.DrawProceduralIndirect(MeshTopology.Points, _drawArgsBuffer);
		
		_molMaterial.SetPass(3);
		Graphics.DrawProceduralIndirect(MeshTopology.Points, _drawArgsBuffer);

		// Blur the normal buffer prior to the lighting
//		molMaterial.SetTexture ("_IDTex", tmpIDBuffer);
//		molMaterial.SetTexture ("_DepthTex", tmpDepthBuffer);
//
//		Graphics.Blit (tmpRenderTex, tmpRenderTex2, molMaterial, 4);
//		Graphics.Blit (tmpRenderTex2, tmpRenderTex, molMaterial, 5);

		// Use normal texture format from here since we dont not need that much precision anymore
		// TODO: Find out if lower precision gives worst results from this point

//		RenderTexture.ReleaseTemporary (tmpColorPong);
//		tmpColorPong = RenderTexture.GetTemporary (src.width, src.height, 0, src.format);

		// Perform lighting from blurred normal buffer
//		Graphics.Blit (src, tmpRenderTex2);
//		Graphics.Blit (tmpRenderTex, tmpRenderTex2, molMaterial, 6);

//		RenderTexture.ReleaseTemporary (tmpColorPing);
//		tmpColorPing = RenderTexture.GetTemporary (src.width, src.height, 0, src.format);
        
		// Local AO
        //Graphics.Blit(tmpRenderTex, tmpRenderTex2);
        //_molMaterial.SetTexture("_DepthTex", tmpDepthBuffer);
        //Graphics.Blit(tmpRenderTex2, tmpRenderTex, _molMaterial, 7);

		// Draw compartment mesh 
		Graphics.SetRenderTarget (tmpRenderTex.colorBuffer, tmpDepthBuffer.depthBuffer);

		// TODO: Find best AO parameters

		// Global AO
		if(true) 
		{
			Matrix4x4 P = Camera.main.projectionMatrix;
//			Matrix4x4 P = Helper.GetProjectionMatrix();
			Matrix4x4 invP = P.inverse;
			Vector4 projInfo = new Vector4 ((-2.0f / (Screen.width * P[0])), (-2.0f / (Screen.height * P[5])), ((1.0f - P[2]) / P[0]), ((1.0f + P[6]) / P[5]));
			
			AoMaterial.SetFloat ("_Radius", radius);
			AoMaterial.SetFloat ("_Radius2", radius*radius);
			AoMaterial.SetFloat ("_Intensity", intensity);
			AoMaterial.SetFloat ("_BlurFilterDistance", blurFilterDistance);
			AoMaterial.SetFloat ("_Sharpness", sharpness);
			AoMaterial.SetVector ("_ProjInfo", projInfo); // used for unprojection
			AoMaterial.SetMatrix ("_ProjectionInv", invP); // only used for reference
			AoMaterial.SetTexture ("_DepthTexture", tmpDepthBuffer);
			
			int rtW = src.width;
			int rtH = src.height;
			
			RenderTexture tmpRt  = RenderTexture.GetTemporary (rtW, rtH);
			RenderTexture tmpRt2;
			
			Graphics.Blit (tmpRenderTex, tmpRt, AoMaterial, 0);
			
			for (int i  = 0; i < blurIterations; i++) 
			{
				AoMaterial.SetVector("_Axis", new Vector2(1.0f,0.0f));
				tmpRt2 = RenderTexture.GetTemporary (rtW, rtH);
				Graphics.Blit (tmpRt, tmpRt2, AoMaterial, 1);
				RenderTexture.ReleaseTemporary (tmpRt);
				
				AoMaterial.SetVector("_Axis", new Vector2(0.0f,1.0f));
				tmpRt = RenderTexture.GetTemporary (rtW, rtH);
				Graphics.Blit (tmpRt2, tmpRt, AoMaterial, 1);
				RenderTexture.ReleaseTemporary (tmpRt2);
			}
			
			tmpRt2 = RenderTexture.GetTemporary (rtW, rtH);
			
			AoMaterial.SetTexture ("_AOTex", tmpRt);
			Graphics.Blit (tmpRenderTex, tmpRt2, AoMaterial, 2);
//			Graphics.Blit (tmpColorPing, tmpRt2, aoMaterial, 3);
			Graphics.Blit (tmpRt2, tmpRenderTex);
			
			RenderTexture.ReleaseTemporary (tmpRt);
			RenderTexture.ReleaseTemporary (tmpRt2);
		}
		
		//		molMaterial.SetTexture ("_IDTex", tmpID);
		//		Graphics.Blit (tmpColor, dst, molMaterial, 7);
		Graphics.Blit (tmpRenderTex, dst);
		
		// Release render textures
		RenderTexture.ReleaseTemporary (tmpRenderTex);
		RenderTexture.ReleaseTemporary (tmpRenderTex2);
		RenderTexture.ReleaseTemporary (tmpDepthBuffer);	
		RenderTexture.ReleaseTemporary (tmpIDBuffer);			
	}
}
        
		