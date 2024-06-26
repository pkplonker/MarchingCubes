﻿using System;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public interface ITerrainNoise3D
{
	void GenerateNoiseMap(Vector3Int dimensions, Noise noiseData, Vector3 offset, Action<float4[]> callback,
		AsyncQueue computeShaderQueue, AsyncQueue computeShaderReadbackQueue, Vector3Int worldChunk, Vector3Int maxChunk);
}