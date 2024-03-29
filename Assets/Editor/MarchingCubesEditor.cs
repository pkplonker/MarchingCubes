using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[CustomEditor(typeof(MarchingCubes))]
[CanEditMultipleObjects]
public class MarchingCubesEditor : Editor
{
	private static int iterations = 10;

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
	}
}