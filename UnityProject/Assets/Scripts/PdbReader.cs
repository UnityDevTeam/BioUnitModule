using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

public static class PdbReader
{
    public static string[] AtomSymbols = { "C", "H", "N", "O", "P", "S" };
    public static float[] AtomRadii = { 1.548f, 1.100f, 1.400f, 1.348f, 1.880f, 1.808f };
    public static Color[] AtomColors = 
    { 
        new Color(0.282f, 0.6f, 0.498f, 1f), 
        Color.white, 
        new Color(0.443f, 0.662f, 0.882f, 1f), 
        new Color(0.827f, 0.294f, 0.333f, 1f), 
        new Color(1f, 0.839f, 0.325f,1f),
        new Color(0.960f, 0.521f, 0.313f, 1f) 
    };    

	public static List<Vector4> ReadPdbFile(string path)
	{
		var atoms = new List<Vector4>();
	
		foreach (var line in File.ReadAllLines(path)) 
		{
            if (line.StartsWith("ATOM"))
			{
				var split = line.Split(new char[]{' '},  StringSplitOptions.RemoveEmptyEntries);				
                var position = split.Where(s => s.Contains(".")).ToList();

			    var atom = new Vector4
			    {
			        x = float.Parse(position[0]),
			        y = float.Parse(position[1]),
			        z = float.Parse(position[2]),
			        w = Array.IndexOf(AtomSymbols, split.Last())
			    };

			    if (Math.Abs(atom.w - (-1)) < Single.Epsilon) Debug.Log("Unknown symbol: " + split.Last());

				atoms.Add(atom);
			}

		    if (line.StartsWith("TER")) break;
		}

        return atoms;
	}
}

