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
	private MeshFilter meshFilter;

	[SerializeField]
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
	private AsyncQueue computerShaderQueue;
	private AsyncQueue readbackQueue;
	public Vector3Int ChunkCoord { get; private set; }

	public void Init(Vector3Int chunkCoord, ChunkManager chunkManager, ITerrainNoise3D noiseGenerator, Vector3Int size,
		Noise noiseData, AsyncQueue computerShaderQueue, AsyncQueue readbackQueue)
	{
		this.ChunkCoord = chunkCoord;
		this.noiseData = noiseData;
		this.noiseGenerator = noiseGenerator;
		this.size = size;
		this.chunkManager = chunkManager;
		this.computerShaderQueue = computerShaderQueue;
		this.readbackQueue = readbackQueue;
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
				marchingCubes = new MarchingCubes(MarchingCubeShader, data, noiseData.IsoLevel, size, factor,
					BuildMesh);
			}), computerShaderQueue, readbackQueue);
	}

	private void BuildMesh(MarchingCubes mCubes) => mCubes.March(GenerateMesh);

	private void OnEnable() => noiseGenerator = GetComponent<ITerrainNoise3D>();

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

	public bool Modify(RaycastHit hitInfo, float radius) => chunkManager.Modify(this, hitInfo, radius);

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
	public float Value { get; set; }
	public int Index { get; set; }

	public NoiseMapChange(int index, float value)
	{
		this.Index = index;
		this.Value = value;
	}
}

public struct Triangle
{
	public Vector3 A;
	public Vector3 B;
	public Vector3 C;
}