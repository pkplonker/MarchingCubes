using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

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

	private Vector3Int paddedSize => Size + new Vector3Int(1,1,1);

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
	private List<Vector3> vertices = new List<Vector3>();
	private List<int> triangles = new List<int>();
	private MeshFilter meshFilter;
	private MeshRenderer meshRender;
	private ITerrainNoise3D noise;

	[Space]
	[SerializeField]
	private bool runOnUpdate = true;

	private void OnValidate()
	{
		meshFilter = GetComponent<MeshFilter>();
		meshRender = GetComponent<MeshRenderer>();
		meshRender.material = Material;
		noise = GetComponent<ITerrainNoise3D>();
	}

	private void Start()
	{
		CreateNoise();
	}

	private void Update()
	{
		if (!runOnUpdate) return;
		CreateNoise();
		if (!drawDebug)
			March();
	}

	public void CreateNoise()
	{
		noiseMap = noise.GenerateNoiseMap(Size+ new Vector3Int(1,1,1), NoiseSeed, NoiseScale, NoiseOctaves, NoisePersistance,
			NoiseLacunarity, transform.position / NoiseScale);
		// test to confirm iteration is correct
		//noiseMap = CreateNoiseMap(16);
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
		vertices = new List<Vector3>();
		triangles = new List<int>();
		for (int x = 0; x < Size.x; x++)
		{
			for (int y = 0; y < Size.y; y++)
			{
				for (int z = 0; z < Size.z; z++)
				{
					float[] corners = new float[8];
					for (int i = 0; i < 8; i++)
					{
						Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];
						corners[i] = noiseMap[Index1D(corner, paddedSize)];
					}

					MarchCube(new Vector3(x, y, z), GetConfigIndex(corners));
				}
			}
		}

		GenerateMesh();
	}

	private void GenerateMesh()
	{
		var mesh = new Mesh();
		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
		mesh.RecalculateNormals();
		meshFilter.mesh = null;
		meshFilter.mesh = mesh;
	}

	private void MarchCube(Vector3 vertPos, int index)
	{
		if (index == 0 || index == 255)
		{
			return;
		}

		int edgeIndex = 0;

		for (int tri = 0; tri < 5; tri++)
		{
			for (int vert = 0; vert < 3; vert++)
			{
				int val = MarchingTable.Triangles[index, edgeIndex];
				if (val == -1)
				{
					return;
				}

				Vector3 vertex = CalculateVertexPosition(vertPos + MarchingTable.Edges[val, 0],
					vertPos + MarchingTable.Edges[val, 1]);
				vertex *= VertDistance;
				vertices.Add(vertex);
				triangles.Add(vertices.Count - 1);
				edgeIndex++;
			}
		}
	}

	private Vector3 CalculateVertexPosition(Vector3 start, Vector3 end)
	{
		return (start + end) / 2;
	}

	private int GetConfigIndex(float[] corners)
	{
		int index = 0;
		for (int i = 0; i < 8; i++)
		{
			if (corners[i] > IsoLevel)
			{
				index |= 1 << i;
			}
		}

		return index;
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