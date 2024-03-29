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

		if (GUILayout.Button("Regenerate Noise"))
		{
			foreach (var target in targets)
			{
				((MarchingCubes) target).CreateNoise();
			}
		}
		if (GUILayout.Button("Regenerate Debug Noise"))
		{
			foreach (var target in targets)
			{
				((MarchingCubes) target).CreateDebugNoise();
			}
		}
		if (GUILayout.Button("Regenerate Mesh"))
		{
			foreach (var target in targets)
			{
				((MarchingCubes) target).March();
			}
		}

		EditorGUILayout.IntField("Iterations", iterations);
		if (GUILayout.Button("Regenerate Noise iterations"))
		{
			var count = 0;
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < iterations; i++)
			{
				foreach (var target in targets)
				{
					((MarchingCubes) target).CreateNoise();
					count++;
				}
			}

			Debug.Log(
				$"Total: {sw.ElapsedMilliseconds}, Iterations: {count}, Average: {sw.ElapsedMilliseconds / (float) count}");
		}

		if (GUILayout.Button("Regenerate Mesh iterations"))
		{
			var count = 0;
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < iterations; i++)
			{
				foreach (var target in targets)
				{
					((MarchingCubes) target).March();
					count++;
				}
			}

			Debug.Log(
				$"Total: {sw.ElapsedMilliseconds}, Iterations: {count}, Average: {sw.ElapsedMilliseconds / (float) count}");
		}
	}
}