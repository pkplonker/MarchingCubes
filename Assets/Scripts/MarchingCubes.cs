using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class MarchingCubes
{
	private Vector3Int chunkSize;
	private Vector3Int PaddedSize => chunkSize + new Vector3Int(1, 1, 1);

	private float[] noiseMap;

	private MeshFilter meshFilter;
	private MeshRenderer meshRender;

	private ComputeShader shader;

	private ComputeBuffer triangleBuffer;
	private ComputeBuffer inputPositionsBuffer;
	private ComputeBuffer triCountBuffer;
	private ITerrainNoise3D noiseGenerator;

	private static readonly int ISO_LEVEL = Shader.PropertyToID("isoLevel");
	private static readonly int SIZE = Shader.PropertyToID("size");
	private static readonly int TRIANGLES = Shader.PropertyToID("triangles");
	private static readonly int INPUT_POSITIONS = Shader.PropertyToID("inputPositions");
	private int length => PaddedSize.x * PaddedSize.y * PaddedSize.z;
	private int kernel => shader.FindKernel("CSMain");

	public MarchingCubes(ComputeShader shader, float[] noiseMap, float isoLevel, Vector3Int chunkSize)
	{
		ClearBuffers(); // Req for editor work
		this.chunkSize = chunkSize;
		this.shader = shader;
		triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		this.noiseMap = noiseMap;
		shader.SetFloat(ISO_LEVEL, isoLevel);
		shader.SetInts(SIZE, new int[3] {PaddedSize.x, PaddedSize.y, PaddedSize.z});

		EnsureBuffersInitialized(length);
	}

	public void OnDisable()
	{
		ClearBuffers();
	}

	private void ClearBuffers()
	{
		triangleBuffer?.Release();
		triangleBuffer = null;
		inputPositionsBuffer?.Release();
		inputPositionsBuffer = null;
		triCountBuffer?.Release();
		triCountBuffer = null;
	}

	public TriangleData March()
	{
		//var sw = Stopwatch.StartNew();
		EnsureBuffersInitialized(length);

		shader.Dispatch(kernel, 4, 4, 4);
		ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);

		int numTris = GetTriangleCount(triCountBuffer);
		Triangle[] results = new Triangle[numTris];
		triangleBuffer.GetData(results, 0, 0, numTris);

		//Debug.Log($"{sw.ElapsedMilliseconds}ms");
		return new TriangleData(results, numTris);
	}

	private void EnsureBuffersInitialized(int length)
	{
		if (triangleBuffer == null || triangleBuffer.count != length * 5)
		{
			triangleBuffer?.Release();
			triangleBuffer = new ComputeBuffer(length * 5, sizeof(float) * 3 * 3, ComputeBufferType.Append);
		}

		triangleBuffer.SetCounterValue(0);
		shader.SetBuffer(kernel, TRIANGLES, triangleBuffer);

		if (inputPositionsBuffer == null || inputPositionsBuffer.count != length)
		{
			inputPositionsBuffer?.Release();
			inputPositionsBuffer = new ComputeBuffer(length, sizeof(float) * 4, ComputeBufferType.Default,
				ComputeBufferMode.Immutable);
			UpdateInputData();
		}

		shader.SetBuffer(kernel, INPUT_POSITIONS, inputPositionsBuffer);
	}

	public void UpdatePointCloud(float[] pointCloud)
	{
		this.noiseMap = pointCloud;
		UpdateInputData();
	}

	private void UpdateInputData()
	{
		float4[] inputData = new float4[length];
		for (var i = 0; i < length; i++)
		{
			var p = Index3D(i, PaddedSize);
			inputData[i] = new float4(p.x, p.y, p.z, noiseMap[i]);
		}

		inputPositionsBuffer.SetData(inputData);
	}

	private int GetTriangleCount(ComputeBuffer countBuffer)
	{
		int[] triCountArray = new int[1];
		countBuffer.GetData(triCountArray);
		return triCountArray[0];
	}

	private Vector3Int Index3D(int index, Vector3Int size)
	{
		int z = index / (size.x * size.y);
		index -= (z * size.x * size.y);
		int y = index / size.x;
		int x = index % size.x;
		return new Vector3Int(x, y, z);
	}

	private int Index1D(Vector3Int value, Vector3Int size) =>
		value.x + value.y * size.x + value.z * size.x * size.y;
}