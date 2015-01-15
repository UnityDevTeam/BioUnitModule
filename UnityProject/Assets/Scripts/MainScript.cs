using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

struct TunnelSphere
{
    public float Radius;
    public Vector4 Position;
}

struct Atom
{
    public int Type;
    public Vector4 Position;
}

public class MainScript : MonoBehaviour
{
    /*** Global const attributes ***/

    public const string Path = "D:/Projects/Unity/BrnoProject/trunk/UnityProject/Data/";
    public Material DepthNormalsBlitMaterial;

    private RenderTexture _cameraDepthBuffer;
    private RenderTexture _cameraDepthNormalBuffer;

    /*** Molecule attributes ***/

    public const int NumAtomMax = 10000;
    public const int AtomSize = 5 * sizeof(float);

    public const string AtomDataFilePath = Path + "atoms/data.bin";

    private ComputeBuffer _atomPositionsBuffer;
    private ComputeBuffer _atomTypesBuffer;
    private ComputeBuffer _atomAlphasBuffer;
    private ComputeBuffer _atomRadiiBuffer;
    private ComputeBuffer _atomColorsBuffer;

    private int[] _atomTypes = new int[NumAtomMax];
    private int[] _atomDisplayTypes = new int[NumAtomMax];
    private float[] _atomDisplayAlphas = new float[NumAtomMax];

    private Vector4[] _atomPositions = new Vector4[NumAtomMax];
    private Vector4[] _atomCurrentPositions = new Vector4[NumAtomMax];
    private Vector4[] _atomDisplayPositions = new Vector4[NumAtomMax];
    
    public Material AtomMaterial;

    /*** Tunnel attributes ***/

    public const int NumTunnelSphereMax = 1000;

    public const string TunnelDataFilePath = Path + "tunnels/cluster_data_1.bin";
    public const string TunnelIndexFilePath = Path + "tunnels/cluster_index_1.bin";

    private ComputeBuffer _tunnelPositionsBuffer;
    private ComputeBuffer _tunnelColorBuffer;
    private ComputeBuffer _tunnelRadiiBuffer;

    private float[] _tunnelRadii = new float[NumTunnelSphereMax];
    private float[] _tunnelDisplayRadii = new float[NumTunnelSphereMax];

    private Vector4[] _tunnelColors = new Vector4[NumTunnelSphereMax];
    private Vector4[] _tunnelPositions = new Vector4[NumTunnelSphereMax];
    private Vector4[] _tunnelCurrentPositions = new Vector4[NumTunnelSphereMax];
    private Vector4[] _tunnelDisplayPositions = new Vector4[NumTunnelSphereMax];
    
    public Material TunnelMaterial;

    //*** Inspector Hidden Attributes ***//

    [HideInInspector]
    public int CurrentFrame = 0;

    [HideInInspector]
    public int PreviousFrame = -1;

    [HideInInspector]
    public int NextFrameCount = 0;

    [HideInInspector]
    public bool Pause = true;
    
    [HideInInspector]
    private int[] _tunnelFrameSizes;

    [HideInInspector]
    private int[] _tunnelFrameOffsets;

    [HideInInspector]
    public int MolFrameSize = 0;

    [HideInInspector]
    public int NumAtoms = 0;

    [HideInInspector]
    public int NumFrames = 0;

    [HideInInspector]
    public bool Init = false;

    private bool _resetDisplayPositions = false;

    //private int lastSphereCount = 0;


    //*** Inspector Exposed Attributes ***//

    [RangeAttribute(0, 10)]
    public float Scale = 1.0f;

    [RangeAttribute(1, 10)]
    public int NextFrameDelay = 1;

    [RangeAttribute(1, 100)]
    public int TemporalResolution = 1;

    [RangeAttribute(0, 1)]
    public float SpeedReduction = 1;

    [RangeAttribute(1, 20)]
    public int CurrentTunnel = 1;

    public Color TunnelColor;
    
    //*** Functions ***//
    
    void CreateBuffers()
    {
        if (_tunnelPositionsBuffer == null) _tunnelPositionsBuffer = new ComputeBuffer(NumTunnelSphereMax, 16);
        if (_tunnelColorBuffer == null) _tunnelColorBuffer = new ComputeBuffer(NumTunnelSphereMax, 16);
        if (_tunnelRadiiBuffer == null) _tunnelRadiiBuffer = new ComputeBuffer(NumTunnelSphereMax, 4);
        
        if (_atomPositionsBuffer == null) _atomPositionsBuffer = new ComputeBuffer(NumAtomMax, 16);
        if (_atomTypesBuffer == null) _atomTypesBuffer = new ComputeBuffer(NumAtomMax, 4);
        if (_atomAlphasBuffer == null) _atomAlphasBuffer = new ComputeBuffer(NumAtomMax, 4);
        if (_atomRadiiBuffer == null) _atomRadiiBuffer = new ComputeBuffer(PdbReader.AtomSymbols.Length, 4);
        if (_atomColorsBuffer == null) _atomColorsBuffer = new ComputeBuffer(PdbReader.AtomSymbols.Length, 16);

        _atomRadiiBuffer.SetData(PdbReader.AtomRadii);
        _atomColorsBuffer.SetData(PdbReader.AtomColors);
    }

    void DestroyBuffers()
    {
        if (_tunnelPositionsBuffer != null) _tunnelPositionsBuffer.Release();
        if (_tunnelColorBuffer != null) _tunnelColorBuffer.Release();
        if (_tunnelRadiiBuffer != null) _tunnelRadiiBuffer.Release();
        
        if (_atomPositionsBuffer != null) _atomPositionsBuffer.Release();
        if (_atomTypesBuffer != null) _atomTypesBuffer.Release();
        if (_atomAlphasBuffer != null) _atomAlphasBuffer.Release();
        if (_atomRadiiBuffer != null) _atomRadiiBuffer.Release();
        if (_atomColorsBuffer != null) _atomColorsBuffer.Release();
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        Debug.Log("Create Compute Buffers");
        CreateBuffers();
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        Debug.Log("Destroy Compute Buffers");
        DestroyBuffers();

        _resetDisplayPositions = true;
    }

    void Start()
    {
        if (!File.Exists(AtomDataFilePath)) throw new Exception("No file found at: " + AtomDataFilePath);
        if (!File.Exists(TunnelDataFilePath)) throw new Exception("No file found at: " + TunnelDataFilePath);
        if (!File.Exists(TunnelIndexFilePath)) throw new Exception("No file found at: " + TunnelIndexFilePath);

        NumFrames = Directory.GetFiles(Path + "atoms/", "*.pdb").Count();
        NumAtoms = (int)(new FileInfo(AtomDataFilePath).Length / NumFrames / AtomSize);
        MolFrameSize = NumAtoms * AtomSize;

        // Read tunnel frame sizes
        _tunnelFrameSizes = new int[NumFrames];
        var tempBuffer = File.ReadAllBytes(TunnelIndexFilePath);

        Buffer.BlockCopy(tempBuffer, 0, _tunnelFrameSizes, 0, tempBuffer.Length);

        // Find tunnel frame offsets
        _tunnelFrameOffsets = new int[NumFrames];
        _tunnelFrameOffsets[0] = 0;

        for (int i = 1; i < NumFrames; i++) _tunnelFrameOffsets[i] = _tunnelFrameOffsets[i - 1] + _tunnelFrameSizes[i - 1];

        camera.depthTextureMode |= DepthTextureMode.Depth;
        camera.depthTextureMode |= DepthTextureMode.DepthNormals;
    }  

    void OnGUI()
    {
        //GUI.contentColor = Color.black;
        //GUILayout.Label("Current frame: " + currentFrame);

        var progress = (float)CurrentFrame / (float)NumFrames;
        var newProgress = GUI.HorizontalSlider(new Rect(25, Screen.height - 25, Screen.width - 50, 30), progress, 0.0f, 1.0f);

        if (progress != newProgress)
        {
            CurrentFrame = (int)(((float)NumFrames - 1.0f) * newProgress);
            _resetDisplayPositions = true;
        }
    }
    
    void LoadMoleculeFrame(int frame)
    {
        var fs = new FileStream(AtomDataFilePath, FileMode.Open);
        var offset = (long)frame * MolFrameSize;
        var frameBytes = new byte[MolFrameSize];
        var frameData = new float[MolFrameSize / sizeof(float)];

        fs.Seek(offset, SeekOrigin.Begin);
        fs.Read(frameBytes, 0, MolFrameSize);
        fs.Close();

        Buffer.BlockCopy(frameBytes, 0, frameData, 0, MolFrameSize);

        for (var i = 0; i < NumAtoms; i++)
        {
            _atomPositions[i].Set(frameData[i*5 + 1], frameData[i*5 + 2], frameData[i*5 + 3], 1);
            _atomTypes[i] = (int)frameData[i * 5 + 4];
        }

        //var atoms = new List<Atom>();
        //for (var i = 0; i < NumAtoms; i++)
        //{
        //    var position = new Vector4(frameData[i*5 + 1], frameData[i*5 + 2], frameData[i*5 + 3], 1);
        //    atoms.Add(new Atom()
        //    {
        //        Position = position,
        //        DistanceFromCamera = Vector3.Distance(Camera.main.transform.position, position),
        //        Type = (int)frameData[i * 5 + 4]
        //    });
        //}

        //var sortedAtoms = atoms.OrderByDescending(a => a.DistanceFromCamera).ToArray();

        //for (var i = 0; i < NumAtoms; i++)
        //{
        //    _atomPositions[i] = sortedAtoms[i].Position;
        //    _atomTypes[i] = sortedAtoms[i].Type;
        //}
    }

    void LoadTunnelFrame(int frame)
    {
        //Debug.Log(frame + 1);

        var frameSize = _tunnelFrameSizes[frame];
        if (frameSize == 0) return;
        
        var offset = (long)_tunnelFrameOffsets[frame];
        var frameBytes = new byte[frameSize];
        var frameData = new float[frameSize / sizeof(float)];
        var numSpheres = frameData.Length / 5;

        var fs = new FileStream(TunnelDataFilePath, FileMode.Open);
        fs.Seek(offset, SeekOrigin.Begin);
        fs.Read(frameBytes, 0, frameSize);
        fs.Close();

        Buffer.BlockCopy(frameBytes, 0, frameData, 0, frameSize);

        // Build tunnels data structure
        var tunnels = new SortedDictionary<int, List<TunnelSphere>>();
        for (int i = 0; i < numSpheres; i++)
        {
            int tunnelId = (int) frameData[i*5 + 4];
            if (!tunnels.ContainsKey(tunnelId)) tunnels[tunnelId] = new List<TunnelSphere>();
            tunnels[tunnelId].Add(new TunnelSphere()
            {
                Position = new Vector4(frameData[i * 5 + 0], frameData[i * 5 + 1], frameData[i * 5 + 2], 1),
                Radius = frameData[i * 5 + 3]
            });
        }

        // Only display one tunnel 
        var displayTunnelSpheres = (tunnels.ContainsKey(CurrentTunnel)) ? tunnels[CurrentTunnel] : tunnels.Values.ToList()[0];
        for (int i = 0; i < displayTunnelSpheres.Count; i++)
        {
            _tunnelPositions[i] = displayTunnelSpheres[i].Position;
            _tunnelRadii[i] = displayTunnelSpheres[i].Radius;
        }

        NumTunnelSpheres = displayTunnelSpheres.Count;
    }
    
    private const float FloatDigitScale = 100000.0f;

    void UpdateAtomDisplayPositions()
    {
        var sortKeys = new int[NumAtoms];
        var sortIndices = new int[NumAtoms];

        for (int i = 0; i < NumAtoms; i++)
        {
            _atomCurrentPositions[i] += (_atomPositions[i] - _atomCurrentPositions[i]) * SpeedReduction;

            var v = Mathf.Abs(Vector3.Dot(Camera.main.transform.forward, Camera.main.transform.position - (Vector3)_atomCurrentPositions[i]));
            sortKeys[i] = (int)(-v * FloatDigitScale);
            sortIndices[i] = i;
        }

        Array.Sort(sortKeys, sortIndices);

        for (int i = 0; i < NumAtoms; i++)
        {
            int index = sortIndices[i];
            _atomDisplayTypes[i] = _atomTypes[index];
            _atomDisplayPositions[i] = _atomCurrentPositions[index];
            _atomDisplayAlphas[i] = 1;
        }
    }

    private int NumTunnelSpheres;

    void UpdateTunnelDisplayPositions()
    {
        var sortKeys = new int[NumTunnelSpheres];
        var sortIndices = new int[NumTunnelSpheres];

        for (int i = 0; i < NumTunnelSpheres; i++)
        {
            _tunnelCurrentPositions[i] += (_tunnelPositions[i] - _tunnelCurrentPositions[i]) * SpeedReduction;
            
            var v = Mathf.Abs(Vector3.Dot(Camera.main.transform.forward, Camera.main.transform.position - (Vector3)_tunnelCurrentPositions[i]));
            sortKeys[i] = (int)(v * FloatDigitScale);
            sortIndices[i] = i;
        }

        Array.Sort(sortKeys, sortIndices);

        for (int i = 0; i < NumTunnelSpheres; i++)
        {
            int index = sortIndices[i];
            _tunnelDisplayPositions[i] = _tunnelCurrentPositions[index];
            _tunnelDisplayRadii[i] = _tunnelRadii[index];
            _tunnelColors[i] = TunnelColor;
        }
    }

	void Update()
	{
        if (CurrentFrame != PreviousFrame)
        {
            LoadMoleculeFrame(CurrentFrame);
            LoadTunnelFrame(CurrentFrame);

            if (CurrentFrame == 0 || _resetDisplayPositions)
            {
                Array.Copy(_atomPositions, _atomCurrentPositions, NumAtoms);
                Array.Copy(_tunnelPositions, _tunnelDisplayPositions, NumTunnelSphereMax);
            }

            PreviousFrame = CurrentFrame;
            Init = true;
        }

        UpdateAtomDisplayPositions();
        UpdateTunnelDisplayPositions();

        if (Input.GetKeyDown(KeyCode.Space)) Pause = !Pause;

        if (!Pause)
        {
            NextFrameCount++;

            if (NextFrameCount >= NextFrameDelay)
            {
                CurrentFrame += TemporalResolution;
                NextFrameCount = 0;
            }
        }
        else if (Input.GetKeyDown(KeyCode.N))
        {
            CurrentFrame += TemporalResolution;
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            CurrentFrame -= TemporalResolution;
        }

        if (CurrentFrame > NumFrames - 1) CurrentFrame = 0;
        if (CurrentFrame < 0) CurrentFrame = NumFrames - 1;
	}
    
    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (!Init) return;

        if (_cameraDepthBuffer != null && (_cameraDepthBuffer.width != src.width || _cameraDepthBuffer.height != src.height))
        {
            _cameraDepthBuffer.Release();
            _cameraDepthBuffer = null;
        }

        if (_cameraDepthBuffer == null)
        {
            _cameraDepthBuffer = new RenderTexture(src.width, src.height, 24, RenderTextureFormat.Depth);
        }

        if (_cameraDepthNormalBuffer != null && (_cameraDepthNormalBuffer.width != src.width || _cameraDepthNormalBuffer.height != src.height))
        {
            _cameraDepthNormalBuffer.Release();
            _cameraDepthNormalBuffer = null;
        }

        if (_cameraDepthNormalBuffer == null)
        {
            _cameraDepthNormalBuffer = new RenderTexture(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        }

        // Display tunnel spheres

        //_tunnelPositionsBuffer.SetData(_tunnelDisplayPositions);
        //_tunnelColorBuffer.SetData(_tunnelColors);
        //_tunnelRadiiBuffer.SetData(_tunnelDisplayRadii);

        //TunnelMaterial.SetFloat("scale", Scale);
        //TunnelMaterial.SetBuffer("tunnelPositions", _tunnelPositionsBuffer);
        //TunnelMaterial.SetBuffer("tunnelColors", _tunnelColorBuffer);
        //TunnelMaterial.SetBuffer("tunnelRadii", _tunnelRadiiBuffer);

        //TunnelMaterial.SetPass(0);
        //Graphics.DrawProcedural(MeshTopology.Points, NumTunnelSpheres);
        
        // Display atoms

        Graphics.SetRenderTarget(_cameraDepthNormalBuffer.colorBuffer, _cameraDepthBuffer.depthBuffer);
        GL.Clear(true, true, Color.black, 1);
        Graphics.Blit(src, DepthNormalsBlitMaterial, 0);

        var buffers = new[] { src.colorBuffer, _cameraDepthNormalBuffer.colorBuffer };
        Graphics.SetRenderTarget(buffers, _cameraDepthBuffer.depthBuffer);

        _atomPositionsBuffer.SetData(_atomDisplayPositions);
        _atomAlphasBuffer.SetData(_atomDisplayAlphas);
        _atomTypesBuffer.SetData(_atomDisplayTypes);

        AtomMaterial.SetFloat("scale", Scale);
        AtomMaterial.SetBuffer("atomPositions", _atomPositionsBuffer);
        AtomMaterial.SetBuffer("atomAlphas", _atomAlphasBuffer);
        AtomMaterial.SetBuffer("atomTypes", _atomTypesBuffer);
        AtomMaterial.SetBuffer("atomRadii", _atomRadiiBuffer);
        AtomMaterial.SetBuffer("atomColors", _atomColorsBuffer);

        AtomMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Points, NumAtoms);

        Shader.SetGlobalTexture("_CameraDepthTexture", _cameraDepthBuffer);
        Shader.SetGlobalTexture("_CameraDepthNormalsTexture", _cameraDepthNormalBuffer);
        
        //Graphics.SetRenderTarget(Graphics.activeColorBuffer, depthBuffer);
        //GL.Clear(true, false, Color.black, 1);

        //TunnelMaterial.SetPass(1);
        //Graphics.DrawProcedural(MeshTopology.Points, NumTunnelSpheres);

        Graphics.Blit(src, dest); 
    }

    
}