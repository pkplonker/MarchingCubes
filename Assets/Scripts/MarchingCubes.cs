using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
//[ExecuteInEditMode]
public class MarchingCubes : MonoBehaviour
{
	private enum GizmoMode
	{
		All,
		Above,
		Below
	}

	[Header("Debug")]
	[SerializeField]
	private GizmoMode gizmoMode = GizmoMode.All;

	[SerializeField]
	private float gizmoRadius = 0.2f;

	[SerializeField]
	private bool drawDebug = false;

	private Vector3Int paddedSize => Size + new Vector3Int(1, 1, 1);

	public Vector3Int Size;

	//[Range(-1.0f, 1.0f)]
	[SerializeField]
	private float IsoLevel = 0.2f;

	[Range(0.0f, 1.0f)]
	[SerializeField]
	private float VertDistance = 1f;

	[SerializeField]
	private Material Material;

	private float[,,] scalarField;

	[Header("Noise")]
	[Range(0.0f, 1.0f)]
	[SerializeField]
	private float NoiseLacunarity;

	[SerializeField]
	private Vector3 NoiseOffset;

	[SerializeField]
	private int NoiseSeed;

	[Range(1, 8)]
	[SerializeField]
	private int NoiseOctaves;

	[Range(0f, 2f)]
	[SerializeField]
	private float NoisePersistance;

	[Range(0f, 1000f)]
	[SerializeField]
	private float NoiseScale = 16f;

	private float[] scalerField;
	private float[] noiseMap;
	private Vector3[] vertices = Array.Empty<Vector3>();
	private int[] indices = Array.Empty<int>();
	private MeshFilter meshFilter;
	private MeshRenderer meshRender;

	[Space]
	[SerializeField]
	private bool runOnUpdate = true;

	[SerializeField]
	private ComputeShader marchingCubesShader;

	private ComputeBuffer triangleBuffer;
	private ComputeBuffer inputPositionsBuffer;
	private ComputeBuffer triCountBuffer;
	private ITerrainNoise3D noise;
	private void OnValidate()
	{
		meshFilter = GetComponent<MeshFilter>();
		meshRender = GetComponent<MeshRenderer>();
		meshRender.material = Material;
		noise = GetComponent<ITerrainNoise3D>();
	}

	private void Start()
	{
		triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

		CreateNoise();
	}

	private void Update()
	{
		if (!runOnUpdate) return;
		CreateNoise();
		if (!drawDebug)
			March();
	}

	public void OnDisable()
	{
		triangleBuffer?.Release();
		triangleBuffer = null;
		inputPositionsBuffer?.Release();
		inputPositionsBuffer = null;
		triCountBuffer?.Release();
		triCountBuffer = null;
	}

	public void CreateNoise()
	{
		noiseMap = noise.GenerateNoiseMap(Size + new Vector3Int(1, 1, 1), NoiseSeed, NoiseScale, NoiseOctaves,
			NoisePersistance,
			NoiseLacunarity, transform.position / NoiseScale);
	
	}
	public void CreateDebugNoise()
	{
		noiseMap = CreateNoiseMap(32);
	}

	public float[] CreateNoiseMap(int size)
	{
		int totalSize = size * size * size;
		float[] noiseMap = new float[totalSize];

		for (int x = 0; x < size; x++)
		{
			for (int y = 0; y < size; y++)
			{
				for (int z = 0; z < size; z++)
				{
					bool isEdge = x == 0 || x == size - 1 || y == 0 || y == size - 1 || z == 0 || z == size - 1;
					noiseMap[x + size * (y + size * z)] = isEdge ? 1.0f : 0.0f;
				}
			}
		}

		return noiseMap;
	}

	public void March()
	{
		var sw = Stopwatch.StartNew();
		var index = marchingCubesShader.FindKernel("CSMain");
		marchingCubesShader.SetFloat("isoLevel", IsoLevel);
		marchingCubesShader.SetInts("size", new int[3]
		{
			paddedSize.x, paddedSize.y, paddedSize.z
		});

		var length = paddedSize.x * paddedSize.y * paddedSize.z;

		triangleBuffer?.Release();
		triangleBuffer = new ComputeBuffer(length * 5, sizeof (float) * 3 * 3, ComputeBufferType.Append);
		triangleBuffer.SetCounterValue(0);
		marchingCubesShader.SetBuffer(index, "triangles", triangleBuffer);

		inputPositionsBuffer?.Release();
		inputPositionsBuffer =
			new ComputeBuffer(length, sizeof(float) * 4, ComputeBufferType.Default, ComputeBufferMode.Immutable);
		marchingCubesShader.SetBuffer(index, "inputPositions", inputPositionsBuffer);

		var inputData = new float4[length];
		for (var i = 0; i < length; i++)
		{
			var p = Index3D(i, paddedSize);
			inputData[i] = (new float4(p.x, p.y, p.z, noiseMap[i]));
		}

		inputPositionsBuffer.SetData(inputData);
		marchingCubesShader.Dispatch(index, 4,4,4);
		ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
		int[] triCountArray = new int[1];
		triCountBuffer.GetData(triCountArray);
		int numTris = triCountArray[0];

		var results = new Triangle[numTris];
		triangleBuffer.GetData(results, 0, 0, numTris);
		GenerateMesh(results, numTris);
		Debug.Log($"{sw.ElapsedMilliseconds}ms");
	}

	struct Triangle
	{
		public Vector3 a;
		public Vector3 b;
		public Vector3 c;

		private Vector3[] Vertices => new[] { a, b, c };

		public Vector3 this[int i] => Vertices[i];
	}

	private void GenerateMesh(Triangle[] results, int numTris)
	{
		vertices = new Vector3[numTris * 3];
		indices = new int[numTris * 3];
		Mesh mesh = new Mesh();
		

		for (int i = 0; i < numTris; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				indices[i * 3 + j] = i * 3 + j;
				vertices[i * 3 + j] = results[i][j];
			}
		}

		mesh.SetVertices(vertices);
		mesh.SetTriangles(indices, 0);
		mesh.RecalculateNormals();
		meshFilter.mesh = mesh;
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

	private void OnDrawGizmos()
	{
		if (!drawDebug) return;

		var pos = transform.position;
		for (int x = 0; x < paddedSize.x; x++)
		{
			for (int y = 0; y < paddedSize.y; y++)
			{
				for (int z = 0; z < paddedSize.z; z++)
				{
					var noise = noiseMap[Index1D(new Vector3Int(x, y, z), paddedSize)];

					Gizmos.color = Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(0, 1, noise));

					switch (gizmoMode)
					{
						case GizmoMode.All:
							Gizmos.DrawSphere(pos + (new Vector3(x, y, z) * VertDistance), gizmoRadius);
							break;
						case GizmoMode.Above:
							if (noise > IsoLevel)
							{
								Gizmos.DrawSphere(pos + (new Vector3(x, y, z) * VertDistance),
									gizmoRadius);
							}

							break;
						case GizmoMode.Below:
							if (noise < IsoLevel)
							{
								Gizmos.DrawSphere(pos + (new Vector3(x, y, z) * VertDistance),
									gizmoRadius);
							}

							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}
			}
		}
	}
}