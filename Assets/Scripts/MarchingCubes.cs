using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public class MarchingCubes
{
	private Vector3Int chunkSize;
	private Vector3Int paddedSize;

	private MeshFilter meshFilter;
	private MeshRenderer meshRender;

	private ComputeShader shader;
	private ITerrainNoise3D noiseGenerator;

	private static readonly int ISO_LEVEL = Shader.PropertyToID("isoLevel");
	private static readonly int SIZE = Shader.PropertyToID("size");
	private static readonly int TRIANGLES = Shader.PropertyToID("triangles");
	private static readonly int INPUT_POSITIONS = Shader.PropertyToID("inputPositions");
	private float isoLevel;
	private float4[] inputData;
	private int length;
	private int kernel => shader.FindKernel("CSMain");

	public MarchingCubes(ComputeShader shader, float[] noiseMap, float isoLevel, Vector3Int chunkSize)
	{
		this.chunkSize = chunkSize;
		paddedSize = this.chunkSize + new Vector3Int(1, 1, 1);
		length = paddedSize.x * paddedSize.y * paddedSize.z;

		this.shader = shader;
		this.isoLevel = isoLevel;
		shader.SetFloat(ISO_LEVEL, isoLevel);
		shader.SetInts(SIZE, new int[3] {paddedSize.x, paddedSize.y, paddedSize.z});
		CreateInputDataFromPointCloud(noiseMap);
	}

	public void March(Action<TriangleData> callback)
	{
		var triangleBuffer = new ComputeBuffer(length * 5, sizeof(float) * 3 * 3, ComputeBufferType.Append);
		var inputPositionsBuffer = new ComputeBuffer(length, sizeof(float) * 4, ComputeBufferType.Default,
			ComputeBufferMode.Immutable);
		var triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

		triangleBuffer.SetCounterValue(0);
		shader.SetBuffer(kernel, TRIANGLES, triangleBuffer);
		inputPositionsBuffer.SetData(inputData);
		shader.SetBuffer(kernel, INPUT_POSITIONS, inputPositionsBuffer);
		shader.SetFloat(ISO_LEVEL, isoLevel);

		shader.Dispatch(kernel, 4, 4, 4);
		ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);

		inputPositionsBuffer.Release();

		AsyncGPUReadback.Request(triCountBuffer, triCountRequest =>
		{
			void Release()
			{
				triangleBuffer.Release();
				triCountBuffer.Release();
				inputPositionsBuffer?.Release();
			}

			var error = false;
			if (triCountRequest.hasError)
			{
				Debug.LogError("GPU readback error detected on triCountBuffer.");
				error = true;
			}

			if (!error)
			{
				int numTris = triCountRequest.GetData<int>()[0];

				AsyncGPUReadback.Request(triangleBuffer, triangleRequest =>
				{
					if (triangleRequest.hasError)
					{
						Debug.LogError("GPU readback error detected on triangleBuffer.");

						error = true;
					}
					else
					{
						var triangleData = triangleRequest.GetData<Triangle>();

						Triangle[] results = new Triangle[numTris];

						Array.Copy(triangleData.ToArray(), results, numTris);

						callback(new TriangleData(results, numTris));
					}
				});
			}
			Release();
		});
	}

	public void UpdatePointCloud(List<NoiseMapChange> noiseMapChanges)
	{
		foreach (var change in noiseMapChanges)
		{
			inputData[change.Index].w = change.Value;
		}
	}

	private void CreateInputDataFromPointCloud(float[] noiseMap)
	{
		if (inputData == null || inputData.Length != length)
		{
			inputData = new float4[length];
		}

		int index = 0;
		for (int z = 0; z < paddedSize.z; z++)
		{
			for (int y = 0; y < paddedSize.y; y++)
			{
				for (int x = 0; x < paddedSize.x; x++)
				{
					inputData[index] = new float4(x, y, z, noiseMap[index]);
					index++;
				}
			}
		}
	}

	private int GetTriangleCount(ComputeBuffer countBuffer)
	{
		int[] triCountArray = new int[1];
		countBuffer.GetData(triCountArray);
		return triCountArray[0];
	}
}