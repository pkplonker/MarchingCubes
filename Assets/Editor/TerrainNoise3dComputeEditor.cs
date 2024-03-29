using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainNoise3DCompute))]
[CanEditMultipleObjects]
public class TerrainNoise3dComputeEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		var shader = (TerrainNoise3DCompute) target;

		
	}
}