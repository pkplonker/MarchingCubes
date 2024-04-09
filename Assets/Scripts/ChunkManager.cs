using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

public class ChunkManager : MonoBehaviour
{
	[SerializeField]
	private Vector3Int ChunkSize;

	[SerializeField]
	private Vector3Int MapSize;

	private Chunk[,,] Chunks;

	[SerializeField]
	private GameObject chunkPrefab;

	[SerializeField]
	private ITerrainNoise3D noiseGenerator;

	[SerializeField]
	private Noise noiseData;

	private int factor;

	private void OnEnable()
	{
		noiseGenerator = GetComponent<TerrainNoise3DCompute>();
	}

	public void Start()
	{
		GenerateChunks();
	}

	public void ClearChunks()
	{
		for (int x = 0; x < Chunks.GetLength(0); x++)
		{
			for (int y = 0; y < Chunks.GetLength(1); y++)
			{
				for (int z = 0; z < Chunks.GetLength(2); z++)
				{
					if (Chunks[x, y, z] != null)
						Destroy(Chunks[x, y, z].gameObject);
				}
			}
		}
	}

	public void GenerateChunks()
	{
		var axis = new Vector3Int(Mathf.CeilToInt(MapSize.x / (float) ChunkSize.x),
			Mathf.CeilToInt(MapSize.y / (float) ChunkSize.z), Mathf.CeilToInt(MapSize.z / (float) ChunkSize.z));

		using var t = new Timer(time => Debug.Log($"Generate All took {time / (axis.x * axis.y * axis.z)}ms average"));

		factor = Mathf.CeilToInt(1 / noiseData.VertDistance);

		Chunks = new Chunk[axis.x, axis.y, axis.z];
		for (int x = 0; x < axis.x; x++)
		{
			for (int y = 0; y < axis.y; y++)
			{
				for (int z = 0; z < axis.z; z++)
				{
					//DrawSolidDebugChunk(x, y, z);
					var chunkGO = GameObject.Instantiate(chunkPrefab,
						new Vector3(ChunkSize.x * x, ChunkSize.y * y, ChunkSize.z * z), Quaternion.identity);
					chunkGO.transform.SetParent(transform);
					var chunk = chunkGO.GetComponent<Chunk>();
					Chunks[x, y, z] = chunk;
					chunk.Init(new Vector3Int(x, y, z), this, noiseGenerator, ChunkSize, noiseData);
					chunk.GetComponent<MeshRenderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f),
						UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
				}
			}
		}
	}

	private void DrawSolidDebugChunk(int x, int y, int z)
	{
		var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
		go.transform.localScale = ChunkSize;
		go.transform.position = new Vector3(ChunkSize.x * x, ChunkSize.y * y, ChunkSize.z * z);
		go.GetComponent<MeshRenderer>().material.color = new Color(UnityEngine.Random.value,
			UnityEngine.Random.value, UnityEngine.Random.value);
	}

	public bool Modify(Chunk chunk, RaycastHit hitInfo, float radius)
	{
		Vector3 hitPoint = chunk.transform.InverseTransformPoint(hitInfo.point) * factor;
		var paddedSize = (ChunkSize * factor) + new Vector3Int(1, 1, 1);

		int minX = Mathf.FloorToInt(hitPoint.x - radius * factor);
		int maxX = Mathf.CeilToInt(hitPoint.x + radius * factor);
		int minY = Mathf.FloorToInt(hitPoint.y - radius * factor);
		int maxY = Mathf.CeilToInt(hitPoint.y + radius * factor);
		int minZ = Mathf.FloorToInt(hitPoint.z - radius * factor);
		int maxZ = Mathf.CeilToInt(hitPoint.z + radius * factor);

		var modifications = new Dictionary<Chunk, List<NoiseMapChange>>();
		modifications[chunk] = new List<NoiseMapChange>();
		for (int x = minX; x < maxX; x++)
		{
			for (int y = minY; y < maxY; y++)
			{
				for (int z = minZ; z < maxZ; z++)
				{
					// Overflow to neighbour
					if (x >= paddedSize.x || y >= paddedSize.y || z >= paddedSize.z)
					{
						continue;
					}

					// Overflow to neighbour
					if (x < 0 || y < 0 || z < 0)
					{
						continue;
					}

					ProcessNeighbors(x, y, z, paddedSize, chunk, modifications);

					CreateModification(chunk, modifications, x, y, z, paddedSize);
				}
			}
		}

		foreach (var mod in modifications)
		{
			mod.Key.Modify(mod.Value);
		}

		return true;
	}

	private void ProcessNeighbors(int x, int y, int z, Vector3Int paddedSize, Chunk chunk,
		Dictionary<Chunk, List<NoiseMapChange>> modifications)
	{
		for (int dx = -1; dx <= 1; dx++)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					if (dx == 0 && dy == 0 && dz == 0) continue;

					if (IsEdgeOrCorner(x, y, z, dx, dy, dz, paddedSize))
					{
						ProcessNeighborChunk(x, y, z, dx, dy, dz, paddedSize, chunk, modifications);
					}
				}
			}
		}
	}

	private bool IsEdgeOrCorner(int x, int y, int z, int dx, int dy, int dz, Vector3Int paddedSize)
	{
		return (x == 0 && dx == -1) || (x == paddedSize.x - 1 && dx == 1) ||
		       (y == 0 && dy == -1) || (y == paddedSize.y - 1 && dy == 1) ||
		       (z == 0 && dz == -1) || (z == paddedSize.z - 1 && dz == 1);
	}

	private void ProcessNeighborChunk(int x, int y, int z, int dx, int dy, int dz, Vector3Int paddedSize, Chunk chunk,
		Dictionary<Chunk, List<NoiseMapChange>> modifications)
	{
		var chunkOffset = new Vector3Int(dx, dy, dz);
		var chunkIndex = chunk.ChunkCoord + chunkOffset;

		if (IsValidChunkIndex(chunkIndex, Chunks))
		{
			var neighbourChunk = Chunks[chunkIndex.x, chunkIndex.y, chunkIndex.z];
			var newX = (dx != 0) ? (dx == -1 ? paddedSize.x - 1 : 0) : x;
			var newY = (dy != 0) ? (dy == -1 ? paddedSize.y - 1 : 0) : y;
			var newZ = (dz != 0) ? (dz == -1 ? paddedSize.z - 1 : 0) : z;

			CreateModification(neighbourChunk, modifications, newX, newY, newZ, paddedSize);
		}
	}

	private bool IsValidChunkIndex(Vector3Int chunkIndex, Chunk[,,] chunks)
	{
		return chunkIndex.x >= 0 && chunkIndex.y >= 0 && chunkIndex.z >= 0 &&
		       chunkIndex.x < chunks.GetLength(0) && chunkIndex.y < chunks.GetLength(1) &&
		       chunkIndex.z < chunks.GetLength(2);
	}

	private void CreateModification(Chunk chunk, Dictionary<Chunk, List<NoiseMapChange>> modifications, int x,
		int y, int z, Vector3Int paddedSize)
	{
		if (!modifications.ContainsKey(chunk))
		{
			modifications.Add(chunk, new List<NoiseMapChange>());
		}

		if (GetIndex(x, y, z, paddedSize) > paddedSize.x * paddedSize.y * paddedSize.z)
		{
			Debug.LogError("err");
		}

		modifications[chunk].Add(new NoiseMapChange
		{
			Index = GetIndex(x, y, z, paddedSize),
			Value = GetDigValue(),
		});
	}

	private static int GetIndex(int x, int y, int z, Vector3Int paddedSize) =>
		x + y * paddedSize.x + z * paddedSize.x * paddedSize.y;

	private float GetDigValue() => 0;
}