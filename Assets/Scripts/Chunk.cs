using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
	[SerializeField]
	private ComputeShader MarchingCubeShader;

	[SerializeField]
	private Material ChunkMaterial;

	private MeshFilter meshFilter;
	private MeshRenderer meshRender;
	private MeshCollider meshCollider;

	[SerializeField]
	private ITerrainNoise3D noiseGenerator;

	private float[] noiseMap;
	private Noise noiseData;
	private Vector3[] vertices = Array.Empty<Vector3>();
	private int[] indices = Array.Empty<int>();
	private Vector3Int size;
	private MarchingCubes marchingCubes;
	private int factor;

	public void Init(ITerrainNoise3D noiseGenerator, Vector3Int size, Noise noiseData)
	{
		this.noiseData = noiseData;
		this.noiseGenerator = noiseGenerator;
		this.size = size;

		Generate();
	}

	public void Generate()
	{
		factor = Mathf.CeilToInt(1 / noiseData.VertDistance);
		var factoredSize = (size * factor) + new Vector3Int(1, 1, 1);
		noiseMap = this.noiseGenerator.GenerateNoiseMap(factoredSize, noiseData.NoiseSeed,
			noiseData.NoiseScale, noiseData.NoiseOctaves, noiseData.NoisePersistance, noiseData.NoiseLacunarity,
			transform.position / (noiseData.NoiseScale*noiseData.VertDistance));
		marchingCubes = new MarchingCubes(MarchingCubeShader, noiseMap, noiseData.IsoLevel, size,factor );

		BuildMesh();
	}

	public void BuildMesh()
	{
		marchingCubes.March(GenerateMesh);
	}

	private void OnEnable()
	{
		meshFilter = GetComponent<MeshFilter>();
		meshRender = GetComponent<MeshRenderer>();
		meshRender.material = ChunkMaterial;
		noiseGenerator = GetComponent<ITerrainNoise3D>();
		meshCollider = GetComponent<MeshCollider>();
	}

	private void OnDisable()
	{
		if (meshFilter != null)
		{
			meshFilter.mesh = null;
		}
	}

	private void GenerateMesh(TriangleData triangleData)
	{
		if (meshFilter == null) return;
		vertices = new Vector3[triangleData.count * 3];
		indices = new int[triangleData.count * 3];
		var mesh = new Mesh();
		mesh.indexFormat = IndexFormat.UInt32;
		for (var i = 0; i < triangleData.count; i++)
		{
			for (var j = 0; j < 3; j++)
			{
				indices[i * 3 + j] = i * 3 + j;
				vertices[i * 3 + j] = triangleData.triangles[i][j];
			}
		}

		mesh.SetVertices(vertices);
		mesh.SetTriangles(indices, 0);
		meshFilter.mesh = mesh;
		var id = mesh.GetInstanceID();
		mesh.RecalculateNormals();

		Physics.BakeMesh(id, false);

		meshCollider.sharedMesh = mesh;
	}

	public bool Modify(RaycastHit hitInfo, float radius)
	{
		Vector3 hitPoint = transform.InverseTransformPoint(hitInfo.point) * factor;
		float sqrRadius = (radius * factor) * (radius * factor);
		var paddedSize = (size * factor) + new Vector3Int(1, 1, 1);

		int minX = Mathf.Max(0, Mathf.FloorToInt(hitPoint.x - radius * factor));
		int maxX = Mathf.Min(paddedSize.x, Mathf.CeilToInt(hitPoint.x + radius * factor));
		int minY = Mathf.Max(0, Mathf.FloorToInt(hitPoint.y - radius * factor));
		int maxY = Mathf.Min(paddedSize.y, Mathf.CeilToInt(hitPoint.y + radius * factor));
		int minZ = Mathf.Max(0, Mathf.FloorToInt(hitPoint.z - radius * factor));
		int maxZ = Mathf.Min(paddedSize.z, Mathf.CeilToInt(hitPoint.z + radius * factor));

		var noiseMapChanges = new List<NoiseMapChange>();

		for (int x = minX; x < maxX; x++)
		{
			for (int y = minY; y < maxY; y++)
			{
				for (int z = minZ; z < maxZ; z++)
				{
					Vector3 voxelCenter = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
					float sqrDistance = (voxelCenter - hitPoint).sqrMagnitude;

					if (sqrDistance < sqrRadius)
					{
						int index = x + y * paddedSize.x + z * paddedSize.x * paddedSize.y;
						noiseMap[index] = 0;
						noiseMapChanges.Add(new NoiseMapChange
						{
							Index = index,
							Value = 0,
						});
					}
				}
			}
		}

		if (noiseMapChanges.Count > 0)
		{
			marchingCubes.UpdatePointCloud(noiseMapChanges);
			marchingCubes.March(GenerateMesh);
		}

		return true;
	}
}

public struct NoiseMapChange
{
	public int Value { get; set; }
	public int Index { get; set; }
}

public struct Triangle
{
	public Vector3 A;
	public Vector3 B;
	public Vector3 C;

	public Vector3 this[int i]
	{
		get
		{
			return i switch
			{
				0 => A,
				1 => B,
				2 => C,
				_ => throw new ArgumentOutOfRangeException(nameof(i), "Index must be in the range 0-2.")
			};
		}
	}
}

public struct TriangleData
{
	public readonly Triangle[] triangles;
	public readonly int count;

	public TriangleData(Triangle[] results, int count)
	{
		this.triangles = results;
		this.count = count;
	}
}