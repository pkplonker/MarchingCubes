using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Random = System.Random;

public class TerrainNoise3DCompute : ITerrainNoise3D, IDisposable
{
	private ComputeBuffer resultsBuffer;
	private ComputeBuffer octaveOffsetsBuffer;
	private static readonly int OCTAVE_OFFSETS = Shader.PropertyToID("octaveOffsets");
	private static readonly int RESULT = Shader.PropertyToID("Result");
	private static readonly int PERSISTANCE = Shader.PropertyToID("persistance");
	private static readonly int LACUNARITY = Shader.PropertyToID("lacunarity");
	private static readonly int SCALE = Shader.PropertyToID("scale");
	private static readonly int NOISE_EXTENTS = Shader.PropertyToID("noiseExtents");
	private static readonly int VERTDISTANCE = Shader.PropertyToID("vertDistance");

	private static readonly int OCTAVES = Shader.PropertyToID("octaves");
	private static readonly int SIZE = Shader.PropertyToID("size");
	private static readonly int WORLD_OFFSET = Shader.PropertyToID("worldOffset");
	private static readonly int WORLD_CHUNK = Shader.PropertyToID("worldChunk");
	private static readonly int MAX_CHUNK = Shader.PropertyToID("maxChunk");
	private static readonly int GROUND_DISTANCE_FACTOR = Shader.PropertyToID("distanceFactor");
	private static readonly int GROUND_HEIGHT = Shader.PropertyToID("groundHeight");

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

	public void GenerateNoiseMap(Vector3Int dimensions, Noise noiseData, Vector3 offset, Action<float4[]> callback,
		AsyncQueue computeShaderQueue, AsyncQueue computeShaderReadbackQueue, Vector3Int worldChunk,
		Vector3Int maxChunk)
	{
		random = new System.Random(noiseData.Seed);

		var octaveOffsets = CalculateOctaveOffsets(noiseData.Octaves, offset, random);
		noiseExtents = CalculateExtents(noiseData.Octaves, noiseData.Persistance);

		noiseExtents /= ((float) noiseData.Octaves / 2);
		var size = dimensions.x * dimensions.y * dimensions.z;
		var data = new float4[size];
		EnsureBuffersInitialized(noiseData.Octaves, size);
		foreach (var o in octaveOffsets)
			octaveOffsetsBuffer.SetData(octaveOffsets.ToArray());
		SetShaderParameters(dimensions, noiseData, offset);
		noiseShader.SetFloats(WORLD_CHUNK, worldChunk.x, worldChunk.y, worldChunk.z);
		noiseShader.SetFloats(MAX_CHUNK, maxChunk.x, maxChunk.y, maxChunk.z);

		var sizeX = Mathf.CeilToInt((float) dimensions.x / THREAD_SIZE_X);
		var sizeY = Mathf.CeilToInt((float) dimensions.y / THREAD_SIZE_Y);
		var sizeZ = Mathf.CeilToInt((float) dimensions.z / THREAD_SIZE_Z);

		noiseShader.Dispatch(kernelIndex, sizeX, sizeY, sizeZ);
		computeShaderQueue.Register(() =>
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
				request.GetData<float4>().CopyData(data, size);
				computeShaderQueue.Release();
				
				computeShaderReadbackQueue.Register(() =>
				{
					callback?.Invoke(data);
					computeShaderReadbackQueue.Release();
				});
			});
		});
	}

	public static float CalculateExtents(int octaves, float persistance)
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
			resultsBuffer = new ComputeBuffer(size, sizeof(float) * 4);
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
		noiseShader.SetFloat(VERTDISTANCE, noiseData.VertDistance);

		noiseShader.SetInt(OCTAVES, noiseData.Octaves);
		noiseShader.SetInts(SIZE, dimensions.x, dimensions.y, dimensions.z);
		noiseShader.SetFloats(WORLD_OFFSET, offset.x, offset.y, offset.z);
		noiseShader.SetFloat(GROUND_DISTANCE_FACTOR, noiseData.GroundDistanceFactor);
		noiseShader.SetFloat(GROUND_HEIGHT, noiseData.GroundHeight);
	}

	private static List<Vector3> CalculateOctaveOffsets(int octaves, Vector3 offset, Random random)
	{
		// Debug.Log(offset.x.ToString("f10"));
		// Debug.Log(offset.y.ToString("f10"));
		// Debug.Log(offset.z.ToString("f10"));

		var octaveOffsets = new List<Vector3>(octaves);
		for (var i = 0; i < octaves; i++)
		{
			var offsetX = random.Next(-100000, 100000) + offset.x;
			var offsetY = random.Next(-100000, 100000) + offset.y;
			var offsetZ = random.Next(-100000, 100000) + offset.z;
			octaveOffsets.Add(new Vector3(offsetX, offsetY, offsetZ));
			// Debug.Log(offsetX.ToString("f10"));
			// Debug.Log(offsetY.ToString("f10"));
			// Debug.Log(offsetZ.ToString("f10"));
		}

		return octaveOffsets;
	}

	public void Dispose()
	{
		resultsBuffer?.Release();
		resultsBuffer = null;
		octaveOffsetsBuffer?.Release();
		octaveOffsetsBuffer = null;
	}
}