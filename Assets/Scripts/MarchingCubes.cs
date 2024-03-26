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

	[SerializeField]
	private Vector3Int Size = new(50, 50, 50);

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
	private float[,,] noiseMap;
	private List<Vector3> vertices = new List<Vector3>();
	private List<int> triangles = new List<int>();
	private MeshFilter meshFilter;
	private MeshRenderer meshRender;

	[Space]
	[SerializeField]
	private bool runOnUpdate = true;

	private void OnValidate()
	{
		meshFilter = GetComponent<MeshFilter>();
		meshRender = GetComponent<MeshRenderer>();
		meshRender.material = Material;
	}

	private void Start()
	{
		CreateNoise();
	}

	private void Update()
	{
		if (!runOnUpdate) return;
		CreateNoise();
		March();
	}

	public void CreateNoise()
	{
		noiseMap = NoiseTerrain3D.GenerateNoiseMap(Size, NoiseSeed, NoiseScale, NoiseOctaves, NoisePersistance,
			NoiseLacunarity, transform.position / NoiseScale);

		#region Debug

		var min = float.MaxValue;
		var max = float.MinValue;
		for (int x = 0; x < noiseMap.GetLength(0); x++)
		{
			for (int y = 0; y < noiseMap.GetLength(1); y++)
			{
				for (int z = 0; z < noiseMap.GetLength(2); z++)
				{
					var noise = noiseMap[x, y, z];
					if (noise > max) max = noise;
					if (noise < min) min = noise;
				}
			}
		}

		Debug.Log($"{min} : {max}");

		#endregion
	}

	public void March()
	{
		vertices = new List<Vector3>();
		triangles = new List<int>();
		for (int x = 0; x < noiseMap.GetLength(0) - 1; x++)
		{
			for (int y = 0; y < noiseMap.GetLength(1) - 1; y++)
			{
				for (int z = 0; z < noiseMap.GetLength(2) - 1; z++)
				{
					float[] corners = new float[8];
					for (int i = 0; i < 8; i++)
					{
						Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];
						corners[i] = noiseMap[corner.x, corner.y, corner.z];
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

	private void OnDrawGizmos()
	{
		if (!drawDebug) return;
		float max = float.MinValue;
		float min = float.MaxValue;

		var pos = transform.position;
		for (int x = 0; x < noiseMap.GetLength(0); x++)
		{
			for (int y = 0; y < noiseMap.GetLength(1); y++)
			{
				for (int z = 0; z < noiseMap.GetLength(2); z++)
				{
					var noise = noiseMap[x, y, z];

					if (noise > max)
					{
						max = noise;
					}

					if (noise < min)
					{
						min = noise;
					}

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