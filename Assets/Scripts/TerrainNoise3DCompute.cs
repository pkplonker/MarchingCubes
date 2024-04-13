using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Random = System.Random;

public class TerrainNoise3DCompute : ITerrainNoise3D
{
	private ComputeBuffer resultsBuffer;
	private ComputeBuffer octaveOffsetsBuffer;
	private static readonly int OCTAVE_OFFSETS = Shader.PropertyToID("octaveOffsets");
	private static readonly int RESULT = Shader.PropertyToID("Result");
	private static readonly int PERSISTANCE = Shader.PropertyToID("persistance");
	private static readonly int LACUNARITY = Shader.PropertyToID("lacunarity");
	private static readonly int SCALE = Shader.PropertyToID("scale");
	private static readonly int NOISE_EXTENTS = Shader.PropertyToID("noiseExtents");

	private static readonly int OCTAVES = Shader.PropertyToID("octaves");
	private static readonly int SIZE = Shader.PropertyToID("size");
	private static readonly int WORLD_OFFSET = Shader.PropertyToID("worldOffset");
	private int kernelIndex;
	private Random random;
	private float noiseExtents;
	private readonly ComputeShader noiseShader;

	private const int THREAD_SIZE_X = 8;
	private const int THREAD_SIZE_Y = 8;
	private const int THREAD_SIZE_Z = 8;

	public TerrainNoise3DCompute(ComputeShader noiseShader)
	{
		this.noiseShader = noiseShader;
		kernelIndex = noiseShader.FindKernel("CSMain");
		random = new System.Random();
	}

	public void GenerateNoiseMap(Vector3Int dimensions, Noise noiseData, Vector3 offset, Action<float[]> callback,
		ComputeShaderController computeShaderController)
	{
		random = new System.Random(noiseData.Seed);

		var octaveOffsets = CalculateOctaveOffsets(noiseData.Octaves, offset, random);
		noiseExtents = CalculateExtents(noiseData.Octaves, noiseData.Persistance);

		noiseExtents /= ((float) noiseData.Octaves / 2);
		var size = dimensions.x * dimensions.y * dimensions.z;
		var data = new float[size];

		EnsureBuffersInitialized(noiseData.Octaves, size);

		octaveOffsetsBuffer.SetData(octaveOffsets.ToArray());
		SetShaderParameters(dimensions, noiseData, offset);

		var sizeX = Mathf.CeilToInt((float) dimensions.x / THREAD_SIZE_X);
		var sizeY = Mathf.CeilToInt((float) dimensions.y / THREAD_SIZE_Y);
		var sizeZ = Mathf.CeilToInt((float) dimensions.z / THREAD_SIZE_Z);

		noiseShader.Dispatch(kernelIndex, sizeX, sizeY, sizeZ);
		computeShaderController.Register(() =>
		{
			AsyncGPUReadback.Request(resultsBuffer, request =>
			{
				if (request.hasError)
				{
					Debug.LogError("GPU readback error detected on triCountBuffer.");
					resultsBuffer.Release();
					return;
				}

				resultsBuffer?.Release();
				resultsBuffer = null;
				octaveOffsetsBuffer?.Release();
				octaveOffsetsBuffer = null;
				request.GetData<float>().CopyData(data, size);
				computeShaderController.Release();
				callback?.Invoke(data);
			});
		});
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

	private void EnsureBuffersInitialized(int octaves, int size)
	{
		if (octaveOffsetsBuffer == null || octaveOffsetsBuffer.count != octaves)
		{
			octaveOffsetsBuffer?.Release();
			octaveOffsetsBuffer = new ComputeBuffer(octaves, sizeof(float) * 3);
		}

		if (resultsBuffer == null || resultsBuffer.count != size)
		{
			resultsBuffer?.Release();
			resultsBuffer = new ComputeBuffer(size, sizeof(float));
		}
	}

	private void SetShaderParameters(Vector3Int dimensions, Noise noiseData, Vector3 offset)
	{
		noiseShader.SetBuffer(kernelIndex, RESULT, resultsBuffer);
		noiseShader.SetBuffer(kernelIndex, OCTAVE_OFFSETS, octaveOffsetsBuffer);
		noiseShader.SetFloat(NOISE_EXTENTS, noiseExtents);

		noiseShader.SetFloat(PERSISTANCE, noiseData.Persistance);
		noiseShader.SetFloat(LACUNARITY, noiseData.Lacunarity);
		noiseShader.SetFloat(SCALE, noiseData.Scale);
		noiseShader.SetInt(OCTAVES, noiseData.Octaves);
		noiseShader.SetInts(SIZE, dimensions.x, dimensions.y, dimensions.z);
		noiseShader.SetFloats(WORLD_OFFSET, offset.x, offset.y, offset.z);
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