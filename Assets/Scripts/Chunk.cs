using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

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

	public void Init(ITerrainNoise3D noiseGenerator, Vector3Int size, Noise noiseData)
	{
		this.noiseData = noiseData;
		this.noiseGenerator = noiseGenerator;
		this.size = size;

		Generate();
	}

	public void Generate()
	{
		noiseMap = this.noiseGenerator.GenerateNoiseMap(size + new Vector3Int(1, 1, 1), noiseData.NoiseSeed,
			noiseData.NoiseScale, noiseData.NoiseOctaves, noiseData.NoisePersistance, noiseData.NoiseLacunarity,
			transform.position / noiseData.NoiseScale);
		marchingCubes = new MarchingCubes(MarchingCubeShader, noiseMap, noiseData.IsoLevel, size);

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
		vertices = new Vector3[triangleData.count * 3];
		indices = new int[triangleData.count * 3];
		var mesh = new Mesh();

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
		Vector3 hitPoint = transform.InverseTransformPoint(hitInfo.point);
		float sqrRadius = radius * radius;
		var paddedSize = size + new Vector3Int(1, 1, 1);

		int minX = Mathf.Max(0, (int) (hitPoint.x - radius));
		int maxX = Mathf.Min(paddedSize.x, (int) (hitPoint.x + radius) + 1);
		int minY = Mathf.Max(0, (int) (hitPoint.y - radius));
		int maxY = Mathf.Min(paddedSize.y, (int) (hitPoint.y + radius) + 1);
		int minZ = Mathf.Max(0, (int) (hitPoint.z - radius));
		int maxZ = Mathf.Min(paddedSize.z, (int) (hitPoint.z + radius) + 1);
		var noiseMapChanges = new List<NoiseMapChange>((int) radius * 4);
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

		marchingCubes.UpdatePointCloud(noiseMapChanges);
		marchingCubes.March(GenerateMesh);
		

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