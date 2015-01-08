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

    private Material _molMaterial;

    [HideInInspector]
    public List<string> MolNames;
    
    public Color[] MolColors = new Color[0];

    /*****/

    [RangeAttribute(0, 1)]
    public float molScale = 0.015f;

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
        CheckScreenSize();

        if (_molColorsBuffer == null || _molColorsBuffer.count != MolColors.Length) _molColorsBuffer = new ComputeBuffer(MolColors.Length, sizeof(float) * 4);
        _molColorsBuffer.SetData(MolColors);

        _molMaterial.SetFloat("molScale", molScale);
        _molMaterial.SetVector("viewDirection", Camera.main.transform.forward);

        _molMaterial.SetBuffer("atomDataBuffer", _atomDataBuffer);
        _molMaterial.SetBuffer("atomDataPDBBuffer", _atomDataPdbBuffer);
        _molMaterial.SetBuffer("molAtomCountBuffer", _molAtomCountBuffer);
        _molMaterial.SetBuffer("molAtomStartBuffer", _molAtomStartBuffer);
        _molMaterial.SetBuffer("molPositions", _molPositionsBuffer);
        _molMaterial.SetBuffer("molRotations", _molRotationsBuffer);
        _molMaterial.SetBuffer("molStates", _molStatesBuffer);
        _molMaterial.SetBuffer("molTypes", _molTypesBuffer);
        _molMaterial.SetBuffer("molColors", _molColorsBuffer);

        ////*****//

        RenderTexture tmpRenderTex = RenderTexture.GetTemporary(src.width, src.height, 24, RenderTextureFormat.ARGBFloat);
        RenderTexture tmpRenderTex2 = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGBFloat);

        Graphics.SetRenderTarget(tmpRenderTex);
        GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

        Graphics.SetRenderTarget(tmpRenderTex2);
        GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

        var renderBuffer = new RenderBuffer[2];
        renderBuffer[0] = tmpRenderTex.colorBuffer;
        renderBuffer[1] = tmpRenderTex2.colorBuffer;

        Graphics.SetRenderTarget(renderBuffer, tmpRenderTex.depthBuffer);

        _molMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Points, MainScript.NumMolObjectMax);

        _molMaterial.SetTexture("posTex", tmpRenderTex);
        _molMaterial.SetTexture("infoTex", tmpRenderTex2);
        Graphics.SetRandomWriteTarget(1, _atomDataBuffer);
        Graphics.Blit(src, dst, _molMaterial, 1);
        Graphics.ClearRandomWriteTargets();
        ComputeBuffer.CopyCount(_atomDataBuffer, _drawArgsBuffer, 0);

        RenderTexture.ReleaseTemporary(tmpRenderTex);
        RenderTexture.ReleaseTemporary(tmpRenderTex2);

        ////*****//

        RenderTexture tmpIDBuffer = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.RFloat);

        Graphics.SetRenderTarget(tmpIDBuffer);
        GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

        renderBuffer[0] = src.colorBuffer;
        renderBuffer[1] = tmpIDBuffer.colorBuffer;

        Graphics.SetRenderTarget(renderBuffer, src.depthBuffer);

        _molMaterial.SetPass(2);
        Graphics.DrawProceduralIndirect(MeshTopology.Points, _drawArgsBuffer);

        _molMaterial.SetPass(3);
        Graphics.DrawProceduralIndirect(MeshTopology.Points, _drawArgsBuffer);

        Graphics.Blit(src, dst);

        // Release render textures
        RenderTexture.ReleaseTemporary(tmpIDBuffer);
	}
}
        
		