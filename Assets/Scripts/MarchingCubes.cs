using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
	private readonly int factor;
	private readonly Triangle[] triangleArray;
	private int kernel => shader.FindKernel("CSMain");

	private const int THREAD_SIZE_X = 8;
	private const int THREAD_SIZE_Y = 8;
	private const int THREAD_SIZE_Z = 8;

	public MarchingCubes(ComputeShader shader, float[] noiseMap, float isoLevel, Vector3Int chunkSize, int factor,
		Action buildMeshCallback)
	{
		this.factor = factor;
		this.chunkSize = chunkSize;
		paddedSize = (this.chunkSize * this.factor) + new Vector3Int(1, 1, 1);
		length = paddedSize.x * paddedSize.y * paddedSize.z;
		triangleArray = new Triangle[length];
		this.shader = shader;
		this.isoLevel = isoLevel;
		shader.SetFloat(ISO_LEVEL, isoLevel);
		shader.SetInts(SIZE, new int[3] {paddedSize.x, paddedSize.y, paddedSize.z});
		CreateInputDataFromPointCloud(noiseMap, buildMeshCallback);
	}

	public void March(Action<Triangle[], int> callback)
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

		var sizeX = Mathf.CeilToInt((float) paddedSize.x / THREAD_SIZE_X);
		var sizeY = Mathf.CeilToInt((float) paddedSize.y / THREAD_SIZE_Y);
		var sizeZ = Mathf.CeilToInt((float) paddedSize.z / THREAD_SIZE_Z);

		shader.Dispatch(kernel, sizeX, sizeY, sizeZ);
		ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);

		inputPositionsBuffer.Release();

		AsyncGPUReadback.Request(triCountBuffer, triCountRequest =>
		{
			if (triCountRequest.hasError)
			{
				Debug.LogError("GPU readback error detected on triCountBuffer.");
				ReleaseBuffers();
				return;
			}

			int numTris = triCountRequest.GetData<int>()[0];

			AsyncGPUReadback.Request(triangleBuffer, triangleRequest =>
			{
				if (triangleRequest.hasError)
				{
					Debug.LogError("GPU readback error detected on triangleBuffer.");
					ReleaseBuffers();
					return;
				}

				triangleRequest.GetData<Triangle>().CopyData(triangleArray, numTris);
				ReleaseBuffers();
				callback(triangleArray, numTris);
			});
		});

		void ReleaseBuffers()
		{
			triangleBuffer?.Release();
			triCountBuffer?.Release();
			inputPositionsBuffer?.Release();
			triCountBuffer = null;
			triangleBuffer = null;
			inputPositionsBuffer = null;
		}
	}

	public void UpdatePointCloud(List<NoiseMapChange> noiseMapChanges)
	{
		foreach (var change in noiseMapChanges)
		{
			inputData[change.Index].w = change.Value;
		}
	}

	[BurstCompile]
	public struct PointCloudJob : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<float> noiseMap;

		[WriteOnly]
		public NativeArray<float4> inputData;

		public float invFactor;
		public int3 paddedSize;

		public void Execute(int index)
		{
			int x = index % paddedSize.x;
			int y = (index / paddedSize.x) % paddedSize.y;
			int z = index / (paddedSize.x * paddedSize.y);

			inputData[index] = new float4(x * invFactor, y * invFactor, z * invFactor, noiseMap[index]);
		}
	}

	private void CreateInputDataFromPointCloud(float[] noiseMap, Action buildMeshCallback)
	{
		NativeArray<float4> nativeInputData;
		inputData = new float4[length];

		nativeInputData = new NativeArray<float4>(length, Allocator.TempJob);

		var job = new PointCloudJob
		{
			noiseMap = new NativeArray<float>(noiseMap, Allocator.TempJob),
			inputData = nativeInputData,
			invFactor = 1.0f / factor,
			paddedSize = new int3(paddedSize.x, paddedSize.y, paddedSize.z)
		};

		JobHandle handle = job.Schedule(length, 64);
		MainThreadDispatcher.Instance.StartCoroutineOnMain(
			WaitForJobCompletion(handle, nativeInputData, buildMeshCallback)
		);
	}

	private IEnumerator WaitForJobCompletion(JobHandle handle, NativeArray<float4> nativeInputData,
		Action callback)
	{
		yield return new WaitUntil(() => handle.IsCompleted);

		handle.Complete();
		nativeInputData.CopyData(inputData, inputData.Length);

		nativeInputData.Dispose();

		callback?.Invoke();
	}
}