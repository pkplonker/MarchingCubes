using System;
using Unity.VisualScripting;
using UnityEngine;

public interface ITerrainNoise3D
{
	void GenerateNoiseMap(Vector3Int dimensions, Noise noiseData, Vector3 offset, Action<float[]> callback,
		ComputeShaderController computeShaderController);
}