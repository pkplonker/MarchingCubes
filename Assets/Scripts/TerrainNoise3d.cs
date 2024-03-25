using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static class NoiseTerrain3D
{
	[BurstCompile]
	public static float[,,] GenerateNoiseMap(int3 dimensions, int seed, float scale, int octaves,
		float persistance,
		float lacunarity, float3 offset, int innerLoopSize = 32)
	{
		if (scale <= 0)
		{
			scale = 0.0001f;
		}

		var random = new System.Random(seed);

		using var jobResult = new NativeArray<float>(dimensions.x * dimensions.y * dimensions.z, Allocator.TempJob);
		using var octaveOffsets = new NativeArray<float3>(octaves, Allocator.TempJob);

		for (var i = 0; i < octaves; i++)
		{
			var offsetX = random.Next(-100000, 100000) + offset.x;
			var offsetY = random.Next(-100000, 100000) + offset.y;
			var offsetZ = random.Next(-100000, 100000) + offset.z;
			var nativeOctaveOffsets = octaveOffsets;
			nativeOctaveOffsets[i] = new float3(offsetX, offsetY, offsetZ);
		}

		var job = new NoiseJobTerrain3d()
		{
			Dimensions = dimensions,
			HalfWidth = (float) dimensions.x / 2,
			HalfHeight = (float) dimensions.y / 2,
			HalfDepth = (float) dimensions.z / 2,
			Lacunarity = lacunarity,
			Octaves = octaves,
			OctaveOffsets = octaveOffsets,
			Persistance = persistance,
			Result = jobResult,
			Scale = scale,
		};

		var handle = job.Schedule(jobResult.Length, innerLoopSize);
		handle.Complete();

		return SmoothNoiseMap(dimensions, jobResult);
	}

	[BurstCompile]
	private static float[,,] SmoothNoiseMap(int3 dimensions, NativeArray<float> jobResult)
	{
		var result = new float[dimensions.x, dimensions.y, dimensions.z];

		var maxNoiseHeight = float.MinValue;
		var minNoiseHeight = float.MaxValue;

		for (var z = 0; z < dimensions.z; z++)
		{
			for (var y = 0; y < dimensions.y; y++)
			{
				for (var x = 0; x < dimensions.x; x++)
				{
					var noiseHeight = jobResult[z * dimensions.x * dimensions.y + y * dimensions.x + x] + y;

					if (noiseHeight > maxNoiseHeight)
					{
						maxNoiseHeight = noiseHeight;
					}
					else if (noiseHeight < minNoiseHeight)
					{
						minNoiseHeight = noiseHeight;
					}

					result[x, y, z] = noiseHeight;
				}
			}
		}

		// for (var z = 0; z < dimensions.z; z++)
		// {
		// 	for (var y = 0; y < dimensions.y; y++)
		// 	{
		// 		for (var x = 0; x < dimensions.x; x++)
		// 		{
		// 			result[x, y, z] = math.unlerp(minNoiseHeight, maxNoiseHeight, result[x, y, z]);
		// 		}
		// 	}
		// }

		return result;
	}
}

[BurstCompile]
public struct NoiseJobTerrain3d : IJobParallelFor
{
	public int3 Dimensions;
	public float HalfWidth;
	public float HalfHeight;
	public float HalfDepth;
	public float Scale;
	public int Octaves;
	public float Persistance;
	public float Lacunarity;

	[ReadOnly]
	public NativeArray<float3> OctaveOffsets;

	[WriteOnly]
	public NativeArray<float> Result;

	public void Execute(int index)
	{
		var amplitude = 1f;
		var frequency = 1f;
		var noiseHeight = 0f;

		var x = index % Dimensions.x;
		var y = (index / Dimensions.x) % Dimensions.y;
		var z = index / (Dimensions.x * Dimensions.y);

		for (var i = 0; i < Octaves; i++)
		{
			var sampleX = (x - HalfWidth) / Scale * frequency + OctaveOffsets[i].x;
			var sampleY = (y - HalfHeight) / Scale * frequency + OctaveOffsets[i].y;
			var sampleZ = (z - HalfDepth) / Scale * frequency + OctaveOffsets[i].z;

			var perlinValue = noise.cnoise(new float3(sampleX, sampleY, sampleZ)) * 2 - 1;

			noiseHeight += perlinValue * amplitude;

			amplitude *= Persistance;
			frequency *= Lacunarity;
		}

		Result[index] = noiseHeight;
	}
}