using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class PdbReader
{
	public static List<Vector4> ReadPdbFile(string path)
	{
		// radiuses
		Dictionary<string, float> van_der_waals = new Dictionary<string, float>();
		
		van_der_waals["F"]=1.47f;
		van_der_waals["CL"]= 1.89f;
		van_der_waals["H"]=1.100f;
		van_der_waals["C"]=1.548f;
		van_der_waals["N"]=1.400f;
		van_der_waals["O"]=1.348f;
		van_der_waals["P"]=1.880f;
		van_der_waals["S"]=1.808f;
		van_der_waals["CA"]=1.948f;
		van_der_waals["FE"]=1.948f;
		van_der_waals["ZN"]=1.148f;
		van_der_waals["I"]=1.748f;

        // clear molPositions
        List<Vector4> molAtomPositions = new List<Vector4>();
	
		foreach (string line in File.ReadAllLines(path)) 
		{
			float defaultAtomSize = 1.5f;
			Vector4 atom=new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
			if (line.StartsWith("ATOM") || line.StartsWith("HETATM"))
			{
				string[] split = line.Split(new char[]{' '},  StringSplitOptions.RemoveEmptyEntries);
				List<string> position = new List<string>();
				
				foreach (string s in split)
					if(s.Contains(".")) position.Add(s);
				
				atom.x = float.Parse(position[0]);
				atom.y = float.Parse(position[1]);
				atom.z = float.Parse(position[2]);

//				atom = Quaternion.Euler(0, -90, 0) * atom;
				atom.w = defaultAtomSize;

				if (van_der_waals.ContainsKey(split[1])) atom.w = van_der_waals[split[1]];
				molAtomPositions.Add(atom);
			}
		}
		
		// Find the bounding box of the molecule and align the molecule with the origin 
		var bbMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
		var bbMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
		
		foreach (var atom in molAtomPositions)
		{
			bbMin = Vector3.Min(bbMin,new Vector3(atom.x,atom.y,atom.z));
			bbMax = Vector3.Max(bbMax,new Vector3(atom.x,atom.y,atom.z));
		}

        var bbCenter = bbMin + (bbMax - bbMin) * 0.5f;

		for (int i = 0; i < molAtomPositions.Count; i++) 
		{
			molAtomPositions[i] -= new Vector4(bbCenter.x, bbCenter.y, bbCenter.z, 0);
		}

		return molAtomPositions;
	}
}

