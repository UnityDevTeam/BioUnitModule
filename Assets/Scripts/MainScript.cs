using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

public class MainScript : MonoBehaviour
{
    [RangeAttribute(0, 1)]
    public float Scale = 0.5f;

    public bool ShowAtomColors = false;

    /*****/

    // Reference to the renderer script attached to the camera
    private MoleculeDisplayScript _moleculeDisplayScript;
    
    // List of molecule names
    private List<string> _moleculeNames = new List<string>();

    // List of molecule objects in the scene
    private List<GameObject> _gameObjects = new List<GameObject>();

	void Start ()
	{
        // Fetch reference to the script
        Camera.main.depthTextureMode = DepthTextureMode.Depth;
        _moleculeDisplayScript = Camera.main.GetComponent<MoleculeDisplayScript>();
        
        // Add a new molecule in the scene at position 0,0,0
        AddMoleculeInstace("2OAU", new Vector3(0, 0, 0), Quaternion.identity);

        // Add a new molecule in the scene at position 0,0,0
        AddMoleculeInstace("1OKC", new Vector3(-35, 0, 0), Quaternion.identity);

        // Add a new molecule in the scene at position 0,0,0
        AddMoleculeInstace("2FP4", new Vector3(35, 0, 0), Quaternion.identity);
	}

    void AddMoleculeType(string pdbName, Color color)
    {
        _moleculeNames.Add(pdbName);
        _moleculeDisplayScript.AddMoleculeType(pdbName, color);
    }

    void AddMoleculeInstace(string pdbName, Vector3 position, Quaternion rotation)
    {
        // If molecule type is not present => add new type to the system
        if(_moleculeNames.IndexOf(pdbName) < 0)
        {
            AddMoleculeType(pdbName, new Color(0.5f, 0.1f, 0.85f));
        }        
        
        // Create the game object and assign position + rotation
        var gameObject = new GameObject("Molecule_" +  pdbName + "_" + _gameObjects.Count);
        gameObject.transform.parent = this.transform;
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;

        // Create Molecule component and assign attributes
        var molecule = gameObject.AddComponent<MoleculeScript>();
        molecule.Id = _gameObjects.Count;
        molecule.Type =  _moleculeNames.IndexOf(pdbName);
        molecule.State = (int)MolState.Normal;

        // Add game object to the list
        _gameObjects.Add(gameObject);
    }

    void SendMoleculeDataToRenderer()
    {
        // Molecule data to be transfered to the renderer, do not modify
        var positions = new Vector4[_gameObjects.Count];
        var rotations = new Vector4[_gameObjects.Count];
        var states = new int[_gameObjects.Count];
        var types = new int[_gameObjects.Count];

        // Push the molecules information into the buffers
        foreach (var molObject in _gameObjects)
        {
            var molecule = molObject.GetComponent<MoleculeScript>();

            positions[molecule.Id] = molObject.transform.position;
            rotations[molecule.Id] = Helper.QuanternionToVector4(molObject.transform.rotation);
            states[molecule.Id] = molecule.State;
            types[molecule.Id] = molecule.Type;
        }

        // Send mol information to the renderer
        _moleculeDisplayScript.UpdateMoleculeData(positions, rotations, types, states, Scale, ShowAtomColors);				
    }
	    
	void Update()
	{
        SendMoleculeDataToRenderer();         
	}
}
