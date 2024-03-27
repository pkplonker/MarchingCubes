using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

		var octaveOffsets = new List<Vector3>(octaves);

		for (var i = 0; i < octaves; i++)
		{
			var offsetX = random.Next(-100000, 100000) + offset.x;
			var offsetY = random.Next(-100000, 100000) + offset.y;
			var offsetZ = random.Next(-100000, 100000) + offset.z;
			octaveOffsets.Add(new Vector3(offsetX, offsetY, offsetZ));
		}

		octaveOffsetsBuffer ??= new(octaves, sizeof(float) * 3);
		shader.SetBuffer(index, "octaves", octaveOffsetsBuffer);

		var size = dimensions.x * dimensions.y * dimensions.z;
		resultsBuffer ??= new(size, sizeof(float));
		shader.SetFloat("persistance", persistance);
		shader.SetFloat("lacunarity", lacunarity);
		shader.SetFloat("scale", scale);
		shader.SetInts("size", new int[3]
		{
			dimensions.x, dimensions.y, dimensions.z
		});

		var data = new float[size];
		shader.SetFloats("offset", new float[3]
		{
			offset.x, offset.y, offset.z
		});
		shader.SetBuffer(index, "Result", resultsBuffer);
		shader.Dispatch(index, dimensions.x, dimensions.y, dimensions.z);
		resultsBuffer.GetData(data);
		return data;
	}

	public void OnDisable()
	{
		resultsBuffer?.Release();
		resultsBuffer = null;
		octaveOffsetsBuffer?.Release();
		octaveOffsetsBuffer = null;
	}
}