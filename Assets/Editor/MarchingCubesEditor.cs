using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MarchingCubes))]
[CanEditMultipleObjects]
public class MarchingCubesEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		if (GUILayout.Button("Regenerate Noise"))
		{
			foreach (var target in targets)
			{
				((MarchingCubes) target).Create();
			}
		}
		if (GUILayout.Button("Regenerate Mesh"))
		{
			foreach (var target in targets)
			{
				((MarchingCubes) target).March();
			}
		}
	}
}