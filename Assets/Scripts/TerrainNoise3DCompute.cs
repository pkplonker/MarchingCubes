using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = System.Random;

public class TerrainNoise3DCompute : MonoBehaviour, ITerrainNoise3D
{
	[SerializeField]
	private ComputeShader shader;

	private ComputeBuffer resultsBuffer;
	private ComputeBuffer octaveOffsetsBuffer;
	private static readonly int OCTAVE_OFFSETS = Shader.PropertyToID("octaveOffsets");
	private static readonly int RESULT = Shader.PropertyToID("Result");
	private static readonly int PERSISTANCE = Shader.PropertyToID("persistance");
	private static readonly int LACUNARITY = Shader.PropertyToID("lacunarity");
	private static readonly int SCALE = Shader.PropertyToID("scale");
	private static readonly int OCTAVES = Shader.PropertyToID("octaves");
	private static readonly int NOISE_EXTENTS = Shader.PropertyToID("noiseExtents");
	private static readonly int SIZE = Shader.PropertyToID("size");
	private static readonly int WORLD_OFFSET = Shader.PropertyToID("worldOffset");
	private static readonly int THREAD_GROUP_SIZE_X = 16;
	private static readonly int THREAD_GROUP_SIZE_Y = 64;
	private static readonly int THREAD_GROUP_SIZE_Z = 1;

	public void Generate()
	{
		var iter = 100;
		var sw = Stopwatch.StartNew();
		var results = new List<float[]>();
		for (int i = 0; i < iter; i++)
		{
			var r = GenerateNoiseMap(new Vector3Int(16, 16, 16), 0, 12, 4, 1.8f, 0.7f, new Vector3(0, 0, 0));
			Debug.Log($"Max:{r.Max()}, Min: {r.Min()}, Average: {r.Sum() / r.Length}");
		}

		Debug.Log($"{sw.ElapsedMilliseconds}ms, average {sw.ElapsedMilliseconds / iter}");
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
		octaveOffsetsBuffer?.Release();
		octaveOffsetsBuffer = new ComputeBuffer(octaves, sizeof(float) * 3);
		resultsBuffer?.Release();
		resultsBuffer = new ComputeBuffer(size, sizeof(float));
		shader.SetBuffer(index, RESULT, resultsBuffer);
		shader.SetBuffer(index, OCTAVE_OFFSETS, octaveOffsetsBuffer);

		octaveOffsetsBuffer.SetData(octaveOffsets.ToArray());

		shader.SetFloat(PERSISTANCE, persistance);
		shader.SetFloat(LACUNARITY, lacunarity);
		shader.SetFloat(SCALE, scale);
		shader.SetFloat(NOISE_EXTENTS, noiseExtents);
		shader.SetInt(OCTAVES, octaves);

		shader.SetInts(SIZE, new int[3]
		{
			dimensions.x, dimensions.y, dimensions.z
		});
		shader.SetFloats(WORLD_OFFSET, new float[3]
		{
			offset.x, offset.y, offset.z
		});
		int threadGroupsX = Mathf.CeilToInt(dimensions.x / 8.0f);
		int threadGroupsY = Mathf.CeilToInt(dimensions.y / 8.0f);
		int threadGroupsZ = Mathf.CeilToInt(dimensions.z / 8.0f);
		shader.Dispatch(index, 2,2,2);
		resultsBuffer.GetData(data);
		Debug.Log($"Max:{data.Max()}, Min: {data.Min()}, Average: {data.Sum() / data.Length}");
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