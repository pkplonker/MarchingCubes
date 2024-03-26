using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static class NoiseTerrain3D
{
	public static float[,,] GenerateNoiseMap(Vector3Int dimensions, int seed, float scale, int octaves,
		float persistance,
		float lacunarity, Vector3 offset)
	{
		dimensions += new Vector3Int(1, 1, 1);
		float maxNoiseHeight = 0f;
		float minNoiseHeight = 0f;
		float amp = 1f;

		for (int i = 0; i < octaves; i++)
		{
			maxNoiseHeight += amp;
			minNoiseHeight -= amp;
			amp *= persistance;
		}

		if (scale <= 0)
		{
			scale = 0.0001f;
		}

		var random = new System.Random(seed);

		float[,,] result = new float[dimensions.x, dimensions.y, dimensions.z];
		var octaveOffsets = new List<Vector3>(octaves);

		for (var i = 0; i < octaves; i++)
		{
			var offsetX = random.Next(-100000, 100000) + offset.x;
			var offsetY = random.Next(-100000, 100000) + offset.y;
			var offsetZ = random.Next(-100000, 100000) + offset.z;
			octaveOffsets.Add(new Vector3(offsetX, offsetY, offsetZ));
		}

		for (int x = 0; x < dimensions.x; x++)
		{
			for (int y = 0; y < dimensions.y; y++)
			{
				for (int z = 0; z < dimensions.z; z++)
				{
					var amplitude = 1f;
					var frequency = 1f;
					var noiseHeight = 0f;

					for (var i = 0; i < octaves; i++)
					{
						float sampleX = (x ) / scale * frequency + octaveOffsets[i].x * frequency;
						float sampleY = (y ) / scale * frequency + octaveOffsets[i].y * frequency;
						float sampleZ = (z ) / scale * frequency + octaveOffsets[i].z * frequency;

						var perlinValue = noise.cnoise(new Vector3(sampleX, sampleY, sampleZ));

						noiseHeight += perlinValue * amplitude;

						amplitude *= persistance;
						frequency *= lacunarity;
					}

					//result[x, y, z] = noiseHeight;
					result[x, y, z] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noiseHeight);
				}
			}
		}

		return result;
	}
}