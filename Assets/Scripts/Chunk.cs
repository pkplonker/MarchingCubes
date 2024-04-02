using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
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

	private Noise noiseData;
	private Vector3[] vertices = Array.Empty<Vector3>();
	private int[] indices = Array.Empty<int>();
	private Vector3Int size;
	private MarchingCubes marchingCubes;
	private int factor;
	private ChunkManager chunkManager;
	public Vector3Int ChunkCoord { get; private set; }

	public void Init(Vector3Int chunkCoord, ChunkManager chunkManager, ITerrainNoise3D noiseGenerator, Vector3Int size,
		Noise noiseData)
	{
		this.ChunkCoord = chunkCoord;
		this.noiseData = noiseData;
		this.noiseGenerator = noiseGenerator;
		this.size = size;
		this.chunkManager = chunkManager;
		Generate();
	}

	private void Generate()
	{
		factor = Mathf.CeilToInt(1 / noiseData.VertDistance);
		var factoredSize = (size * factor) + new Vector3Int(1, 1, 1);
		noiseGenerator.GenerateNoiseMap(factoredSize, noiseData,
			transform.position / (noiseData.Scale * noiseData.VertDistance),
			(data =>
			{
				marchingCubes = new MarchingCubes(MarchingCubeShader, data, noiseData.IsoLevel, size, factor,BuildMesh);
				
			}));
		;
	}

	private void BuildMesh()
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

	private void GenerateMesh(Triangle[] triangles, int count)
	{
		if (meshFilter == null) return;

		int triangleCount = count;
		int vertexCount = triangleCount * 3;
		var mesh = new Mesh();
		if (vertices.Length != vertexCount)
		{
			vertices = new Vector3[vertexCount];
			indices = new int[vertexCount];
		}

		int vertexIndex = 0;
		for (int i = 0; i < triangleCount; i++)
		{
			vertices[vertexIndex] = triangles[i].A;
			indices[vertexIndex] = vertexIndex;
			vertexIndex++;

			vertices[vertexIndex] = triangles[i].B;
			indices[vertexIndex] = vertexIndex;
			vertexIndex++;

			vertices[vertexIndex] = triangles[i].C;
			indices[vertexIndex] = vertexIndex;
			vertexIndex++;
		}

		mesh.SetVertices(vertices);
		mesh.SetTriangles(indices, 0);
		meshFilter.mesh = mesh;

		var id = mesh.GetInstanceID();
		mesh.RecalculateNormals();
		var t = Task.Run(() => Physics.BakeMesh(id, false)).ContinueWith(_ => meshCollider.sharedMesh = mesh,
			new CancellationToken(),
			TaskContinuationOptions.None, MainThreadDispatcher.Instance.Sceduler);
	}

	public bool Modify(RaycastHit hitInfo, float radius)
	{
		return chunkManager.Modify(this, hitInfo, radius);
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

	public void Modify(List<NoiseMapChange> changes)
	{
		if (changes.Count > 0)
		{
			marchingCubes.UpdatePointCloud(changes);
			marchingCubes.March(GenerateMesh);
		}
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
}