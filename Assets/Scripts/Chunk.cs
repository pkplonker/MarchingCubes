using System;
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
		GenerateMesh(marchingCubes.March());
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
		for (int x = 0; x < paddedSize.x; x++)
		{
			for (int y = 0; y < paddedSize.y; y++)
			{
				for (int z = 0; z < paddedSize.z; z++)
				{
					Vector3 voxelCenter = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
					float sqrDistance = (voxelCenter - hitPoint).sqrMagnitude;

					if (sqrDistance < sqrRadius)
					{
						int index = x + y * paddedSize.x + z * paddedSize.x * paddedSize.y;
						noiseMap[index] = 0;
					}
				}
			}
		}

		marchingCubes.UpdatePointCloud(noiseMap);
		GenerateMesh(marchingCubes.March());

		return true;
	}
}

public struct Triangle
{
	public Vector3 A;
	public Vector3 B;
	public Vector3 C;

	private Vector3[] Vertices => new[] {A, B, C};

	public Vector3 this[int i] => Vertices[i];
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