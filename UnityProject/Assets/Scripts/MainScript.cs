using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

public class MainScript : MonoBehaviour
{
    // Maximum number of molecule in the scene, change if too small
    public static int NumMolObjectMax = 1000;

    // The prefab corresponding to mol object, to connect in Unity Editor
    public GameObject MolPrefab;

    // List of mol objects in the scene
    private List<GameObject> molObjects = new List<GameObject>();
    
    // Reference to the renderer script attached to the camera
    private CameraScript cameraScript;

    // Molecule data to be transfered to the renderer, do not modify
    private Vector4[] positions = new Vector4[NumMolObjectMax];
    private Vector4[] rotations = new Vector4[NumMolObjectMax];
    private int[] types = new int[NumMolObjectMax];
    private int[] states = new int[NumMolObjectMax];

	void Start ()
	{
        // Init data buffers
        for(int i = 0; i < NumMolObjectMax; i++)
	    {
	        positions[i] = new Vector4(0, 0, 0, 0);
            rotations[i] = new Vector4(0, 0, 0, 0);
            types[i] = -1;
            states[i] = (int)MolState.Null;
	    }

        // Fetch reference to the script
	    cameraScript = Camera.main.GetComponent<CameraScript>();

        // Add a new type of molecule to the system
	    cameraScript.AddMoleculeType("p3", Color.blue);

        // Add a new molecule in the scene at position 0,0,0
        AddNewMolecule(0, new Vector3(0,0,0), Quaternion.identity);
	}

    void AddNewMolecule(int type, Vector3 position, Quaternion rotation)
    {
        var molObject = Instantiate(MolPrefab, position, rotation) as GameObject;
        var molScript = molObject.GetComponent<MolScript>();

        molScript.Id = molObjects.Count;
        molScript.Type = type;
        molScript.State = (int)MolState.Normal;

        molObjects.Add(molObject);
    }
	
	void Update()
	{
        // Push the molecules information into the buffers
	    foreach (var molObject in molObjects)
	    {
            var molScript = molObject.GetComponent<MolScript>();

	        positions[molScript.Id] = molObject.transform.position;
            rotations[molScript.Id] = Helper.QuanternionToVector4(molObject.transform.rotation);
	        types[molScript.Id] = molScript.Type;
            states[molScript.Id] = molScript.State;
	    }
	    
        // Send mol information to the renderer
		cameraScript.UpdateMoleculeData(positions, rotations, types, states);				
	}
}
