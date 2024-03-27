using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

public class TerrainNoise3DCompute : MonoBehaviour
{
	[SerializeField]
	private ComputeShader shader;

	private ComputeBuffer resultsBuffer;
	private ComputeBuffer octaveOffsetsBuffer;

	public void Generate()
	{
		var result = GenerateNoiseMap(new Vector3Int(16, 16, 16), 0, 12, 4, 1.8f, 0.7f, new Vector3(0, 0, 0));
		Debug.Log(result.Max());
	}

	public TerrainNoise3DCompute() { }

	public float[] GenerateNoiseMap(Vector3Int dimensions, int seed, float scale, int octaves,
		float persistance,
		float lacunarity, Vector3 offset)
	{
		var index = shader.FindKernel("CSMain");
		var random = new System.Random(seed);
		var octaveOffsets = CalculateOctaveOffsets(octaves, offset, random);

		var noiseExtents = CalculateExtents(octaves, persistance);

		noiseExtents /= ((float) octaves / 2);

		var size = dimensions.x * dimensions.y * dimensions.z;
		var data = new float[size];

		octaveOffsetsBuffer ??= new(octaves, sizeof(float) * 3);
		resultsBuffer ??= new(size, sizeof(float));
		shader.SetBuffer(index, "Result", resultsBuffer);
		shader.SetBuffer(index, "octaves", octaveOffsetsBuffer);

		octaveOffsetsBuffer.SetData(octaveOffsets.ToArray());

		shader.SetFloat("persistance", persistance);
		shader.SetFloat("lacunarity", lacunarity);
		shader.SetFloat("scale", scale);
		shader.SetFloat("noiseExtents", noiseExtents);
		shader.SetInts("size", new int[3]
		{
			dimensions.x, dimensions.y, dimensions.z
		});
		shader.SetFloats("worldOffset", new float[3]
		{
			offset.x, offset.y, offset.z
		});

		shader.Dispatch(index, dimensions.x, dimensions.y * dimensions.z, 1);
		resultsBuffer.GetData(data);

		return data;
	}

	private static float CalculateExtents(int octaves, float persistance)
	{
		float noiseExtents = 0f;
		float amp = 1f;

		for (int i = 0; i < octaves; i++)
		{
			noiseExtents += amp;
			amp *= persistance;
		}

		return noiseExtents;
	}

	private static List<Vector3> CalculateOctaveOffsets(int octaves, Vector3 offset, Random random)
	{
		var octaveOffsets = new List<Vector3>(octaves);
		for (var i = 0; i < octaves; i++)
		{
			var offsetX = random.Next(-100000, 100000) + offset.x;
			var offsetY = random.Next(-100000, 100000) + offset.y;
			var offsetZ = random.Next(-100000, 100000) + offset.z;
			octaveOffsets.Add(new Vector3(offsetX, offsetY, offsetZ));
		}

		return octaveOffsets;
	}

	public void OnDisable()
	{
		resultsBuffer?.Release();
		resultsBuffer = null;
		octaveOffsetsBuffer?.Release();
		octaveOffsetsBuffer = null;
	}
}