using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

public class MoleculeDisplayScript : MonoBehaviour
{
    public Shader MolShader;
    public Shader AtomShader;
    public Shader DepthNormalsBlitShader;
       
    /*****/
        
    private const int NumMoleculeInstancesMax = 25000;

    private bool _showAtomColors = false;

    private int _numMoleculeInstances = 0;
    private int _previousScreenWidth = 0;
    private int _previousScreenHeight = 0;
        
    private float _scale = 0;
    
    private Material _molMaterial;
    private Material _atomMaterial;
    private Material _depthNormalsBlitMaterial;

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

    private ComputeBuffer _atomRadiiBuffer;
    private ComputeBuffer _atomColorsBuffer;

    private List<int> _atomCount = new List<int>();
    private List<int> _atomStart = new List<int>();
    private List<Color> _molColors = new List<Color>();
    private List<Vector4> _atomDataPdb = new List<Vector4>();

    /*****/  

	public void Start()
	{
        camera.depthTextureMode |= DepthTextureMode.Depth;
        camera.depthTextureMode |= DepthTextureMode.DepthNormals;

        _molTypesBuffer = new ComputeBuffer(NumMoleculeInstancesMax, 4);
        _molStatesBuffer = new ComputeBuffer(NumMoleculeInstancesMax, 4);
        _molPositionsBuffer = new ComputeBuffer(NumMoleculeInstancesMax, 16);
        _molRotationsBuffer = new ComputeBuffer(NumMoleculeInstancesMax, 16);
        
        _atomRadiiBuffer = new ComputeBuffer(PdbReader.AtomSymbols.Length, 4);
        _atomRadiiBuffer.SetData(PdbReader.AtomRadii);

        _atomColorsBuffer = new ComputeBuffer(PdbReader.AtomSymbols.Length, 16);
        _atomColorsBuffer.SetData(PdbReader.AtomColors);

        _drawArgsBuffer = new ComputeBuffer(1, 16, ComputeBufferType.DrawIndirect);
        _drawArgsBuffer.SetData(new[] { 0, 1, 0, 0 });

	    _molMaterial = new Material(MolShader) {hideFlags = HideFlags.HideAndDontSave};
	    _atomMaterial = new Material(AtomShader) {hideFlags = HideFlags.HideAndDontSave};
	    _depthNormalsBlitMaterial = new Material(DepthNormalsBlitShader) {hideFlags = HideFlags.HideAndDontSave};
	}

    void OnDestroy()
    {
        ReleaseResources();
    }

    private void ReleaseResources()
    {
        if (_drawArgsBuffer != null) _drawArgsBuffer.Release();
        if (_atomDataBuffer != null) _atomDataBuffer.Release();        
        if (_molTypesBuffer != null) _molTypesBuffer.Release();
        if (_molStatesBuffer != null) _molStatesBuffer.Release();
        if (_molColorsBuffer != null) _molColorsBuffer.Release();
        if (_atomRadiiBuffer != null) _atomRadiiBuffer.Release();
        if (_atomColorsBuffer != null) _atomColorsBuffer.Release();
        if (_atomDataPdbBuffer != null) _atomDataPdbBuffer.Release();
        if (_molAtomCountBuffer != null) _molAtomCountBuffer.Release();
        if (_molAtomStartBuffer != null) _molAtomStartBuffer.Release();
        if (_molPositionsBuffer != null) _molPositionsBuffer.Release();
        if (_molRotationsBuffer != null) _molRotationsBuffer.Release();
    }

    public void AddMoleculeType(string pdbName, Color color)
    {
        var atoms = PdbReader.ReadPdbFile(Application.dataPath + "/Molecules/" + pdbName + ".pdb");
        
        _atomCount.Add(atoms.Count);
        _atomStart.Add(_atomDataPdb.Count);
        _atomDataPdb.AddRange(atoms);
        _molColors.Add(color);

        if (_molAtomCountBuffer == null) _molAtomCountBuffer = new ComputeBuffer(1000, 4);
        if (_molAtomStartBuffer == null) _molAtomStartBuffer = new ComputeBuffer(1000, 4);
        if (_atomDataPdbBuffer == null) _atomDataPdbBuffer = new ComputeBuffer(1000000, 16);
        if (_molColorsBuffer == null) _molColorsBuffer = new ComputeBuffer(1000, 16);
        
        _molAtomCountBuffer.SetData(_atomCount.ToArray());        
        _molAtomStartBuffer.SetData(_atomStart.ToArray());        
        _atomDataPdbBuffer.SetData(_atomDataPdb.ToArray());
        _molColorsBuffer.SetData(_molColors.ToArray());
    }

    public void UpdateMoleculeData(Vector4[] positions, Vector4[] rotations, int[] types, int[] states, float scale, bool showAtomColors)
    {        
        _numMoleculeInstances = positions.Length;

        if (_numMoleculeInstances > NumMoleculeInstancesMax) throw new Exception("Too much instances to draw, resize compute buffers");

        _molPositionsBuffer.SetData(positions);
        _molRotationsBuffer.SetData(rotations);
        _molTypesBuffer.SetData(types);
        _molStatesBuffer.SetData(states);

        _scale = scale;
        _showAtomColors = showAtomColors;
    }  

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        // Return if no instances to draw
        if (_numMoleculeInstances == 0) { Graphics.Blit(src, dst); return; }

        //*** Check screen size ***//
        if (Screen.width != _previousScreenWidth || Screen.height != _previousScreenHeight)
        {
            if (_atomDataBuffer != null) _atomDataBuffer.Release();
            _atomDataBuffer = new ComputeBuffer(Screen.width * Screen.height, 32, ComputeBufferType.Append);
            
            _previousScreenWidth = Screen.width;
            _previousScreenHeight = Screen.height;
        }         

        //*** Cull atoms ***//

        _molMaterial.SetFloat("scale", _scale);
        _molMaterial.SetBuffer("molTypes", _molTypesBuffer);
        _molMaterial.SetBuffer("molStates", _molStatesBuffer);
        _molMaterial.SetBuffer("molPositions", _molPositionsBuffer);
        _molMaterial.SetBuffer("molRotations", _molRotationsBuffer);
        _molMaterial.SetBuffer("atomDataPDBBuffer", _atomDataPdbBuffer);
        _molMaterial.SetBuffer("molAtomCountBuffer", _molAtomCountBuffer);
        _molMaterial.SetBuffer("molAtomStartBuffer", _molAtomStartBuffer);

        RenderTexture posTexture = RenderTexture.GetTemporary(src.width, src.height, 24, RenderTextureFormat.ARGBFloat);
        RenderTexture infoTexture = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGBFloat);

        // Clear the temporary render targets
        Graphics.SetRenderTarget(new[] { posTexture.colorBuffer, infoTexture.colorBuffer }, posTexture.depthBuffer);
        GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));        

        _molMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Points, _numMoleculeInstances);

        _molMaterial.SetTexture("posTex", posTexture);
        _molMaterial.SetTexture("infoTex", infoTexture);

        Graphics.SetRandomWriteTarget(1, _atomDataBuffer);
        Graphics.Blit(null, dst, _molMaterial, 1);
        Graphics.ClearRandomWriteTargets();
        ComputeBuffer.CopyCount(_atomDataBuffer, _drawArgsBuffer, 0);

        RenderTexture.ReleaseTemporary(infoTexture);
        RenderTexture.ReleaseTemporary(posTexture);        

        //*** Render atoms ***//

        _atomMaterial.SetInt("showAtomColors", (_showAtomColors) ? 1 : 0);

        _atomMaterial.SetFloat("scale", _scale);        
        _atomMaterial.SetBuffer("molTypes", _molTypesBuffer);
        _atomMaterial.SetBuffer("molStates", _molStatesBuffer);
        _atomMaterial.SetBuffer("molColors", _molColorsBuffer);
        _atomMaterial.SetBuffer("atomRadii", _atomRadiiBuffer);
        _atomMaterial.SetBuffer("atomColors", _atomColorsBuffer);
        _atomMaterial.SetBuffer("atomDataBuffer", _atomDataBuffer);

        var cameraDepthBuffer = RenderTexture.GetTemporary(src.width, src.height, 24, RenderTextureFormat.Depth);
        var cameraDepthNormalBuffer = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        
        // Fetch depth and normals from Unity
        Graphics.SetRenderTarget(cameraDepthNormalBuffer.colorBuffer, cameraDepthBuffer.depthBuffer);
        Graphics.Blit(src, _depthNormalsBlitMaterial, 0);
        
        Graphics.SetRenderTarget(new[] { src.colorBuffer, cameraDepthNormalBuffer.colorBuffer }, cameraDepthBuffer.depthBuffer);     
        _atomMaterial.SetPass(0);
        Graphics.DrawProceduralIndirect(MeshTopology.Points, _drawArgsBuffer);

        Shader.SetGlobalTexture("_CameraDepthTexture", cameraDepthBuffer);
        //Shader.SetGlobalTexture("_CameraDepthNormalsTexture", cameraDepthNormalBuffer);

        Graphics.Blit(src, dst);

        RenderTexture.ReleaseTemporary(cameraDepthBuffer);
        RenderTexture.ReleaseTemporary(cameraDepthNormalBuffer);
	}
}
        
		