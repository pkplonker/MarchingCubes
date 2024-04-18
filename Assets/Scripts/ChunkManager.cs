using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ChunkManager : MonoBehaviour
{
	[SerializeField]
	private Vector3Int ChunkSize;

	[SerializeField]
	private Vector3Int MapSize;

	private Chunk[,,] chunks;

	[SerializeField]
	private GameObject ChunkPrefab;

	[SerializeField]
	private Noise NoiseData;

	private readonly Dictionary<Chunk, List<NoiseMapChange>> modifications = new();

	private int factor;
	private AsyncQueue computeShaderQueue;

	[SerializeField]
	private ComputeShader NoiseShader;

	[SerializeField]
	private int MaxConcurrentGPUActions = 10;

	[SerializeField]
	private int MaxConcurrentGPUReadbackActions = 5;

	private AsyncQueue gpuAsyncReadbackqueue;
	public Vector3Int maxChunkCoord { get; private set; }

	private void OnEnable()
	{
		computeShaderQueue = new AsyncQueue("computeShaderQueue", () => MaxConcurrentGPUActions);
		gpuAsyncReadbackqueue = new AsyncQueue("gpuAsyncReadbackqueue", () => MaxConcurrentGPUReadbackActions);
	}

	public void Start()
	{
		GenerateChunks();
	}

	public void ClearChunks()
	{
		if (chunks == null) return;
		for (int x = 0; x < chunks.GetLength(0); x++)
		{
			for (int y = 0; y < chunks.GetLength(1); y++)
			{
				for (int z = 0; z < chunks.GetLength(2); z++)
				{
					if (chunks[x, y, z] != null)
						Destroy(chunks[x, y, z].gameObject);
				}
			}
		}
	}

	public void GenerateChunks()
	{
		maxChunkCoord = new Vector3Int(Mathf.CeilToInt(MapSize.x / (float) ChunkSize.x),
			Mathf.CeilToInt(MapSize.y / (float) ChunkSize.z), Mathf.CeilToInt(MapSize.z / (float) ChunkSize.z));

		using var t = new Timer(time => Debug.Log($"Generate All took {time / (maxChunkCoord.x * maxChunkCoord.y * maxChunkCoord.z)}ms average"));

		factor = Mathf.CeilToInt(1 / NoiseData.VertDistance);

		chunks = new Chunk[maxChunkCoord.x, maxChunkCoord.y, maxChunkCoord.z];
		for (int x = 0; x < maxChunkCoord.x; x++)
		{
			for (int y = 0; y < maxChunkCoord.y; y++)
			{
				for (int z = 0; z < maxChunkCoord.z; z++)
				{
					//DrawSolidDebugChunk(x, y, z);
					var chunkGO = GameObject.Instantiate(ChunkPrefab,
						new Vector3(ChunkSize.x * x, ChunkSize.y * y, ChunkSize.z * z), Quaternion.identity);
					chunkGO.transform.SetParent(transform);
					var chunk = chunkGO.GetComponent<Chunk>();
					chunks[x, y, z] = chunk;
					chunk.Init(new Vector3Int(x, y, z),this, new TerrainNoise3DCompute(NoiseShader), ChunkSize,
						NoiseData, computeShaderQueue, gpuAsyncReadbackqueue);
					chunk.GetComponent<MeshRenderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f),
						UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
				}
			}
		}
	}

	private void Update()
	{
		computeShaderQueue.Tick();
		gpuAsyncReadbackqueue.Tick();
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

		modifications.Clear();
		return true;
	}

	private void ProcessNeighbors(int x, int y, int z, Vector3Int paddedSize, Chunk chunk,
		Dictionary<Chunk, List<NoiseMapChange>> modifications)
 	{
		if (!IsOnEdgeOrCorner(x, y, z, paddedSize))
			return;

		int minX = x == 0 ? -1 : 0, maxX = x == paddedSize.x - 1 ? 1 : 0;
		int minY = y == 0 ? -1 : 0, maxY = y == paddedSize.y - 1 ? 1 : 0;
		int minZ = z == 0 ? -1 : 0, maxZ = z == paddedSize.z - 1 ? 1 : 0;

		for (int dx = minX; dx <= maxX; dx++)
		{
			for (int dy = minY; dy <= maxY; dy++)
			{
				for (int dz = minZ; dz <= maxZ; dz++)
				{
					if (dx == 0 && dy == 0 && dz == 0) continue;
					ProcessNeighborChunk(x, y, z, dx, dy, dz, paddedSize, chunk, modifications);
				}
			}
		}
	}

	private bool IsOnEdgeOrCorner(int x, int y, int z, Vector3Int paddedSize)
	{
		return x == 0 || x == paddedSize.x - 1 ||
		       y == 0 || y == paddedSize.y - 1 ||
		       z == 0 || z == paddedSize.z - 1;
	}

	private void ProcessNeighborChunk(int x, int y, int z, int dx, int dy, int dz, Vector3Int paddedSize, Chunk chunk,
		Dictionary<Chunk, List<NoiseMapChange>> modifications)
	{
		var chunkOffset = new Vector3Int(dx, dy, dz);
		var chunkIndex = chunk.ChunkCoord + chunkOffset;

		if (IsValidChunkIndex(chunkIndex, chunks))
		{
			var neighbourChunk = chunks[chunkIndex.x, chunkIndex.y, chunkIndex.z];
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
			modifications.Add(chunk, new List<NoiseMapChange>(20));
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

	private float GetDigValue() => -TerrainNoise3DCompute.CalculateExtents(NoiseData);

}