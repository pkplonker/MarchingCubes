using System;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
	[SerializeField]
	private ComputeShader MarchingCubeShader;

	[SerializeField]
	private Material ChunkMaterial;

	private MeshFilter meshFilter;
	private MeshRenderer meshRender;

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
		mesh.RecalculateNormals();
		meshFilter.mesh = mesh;
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