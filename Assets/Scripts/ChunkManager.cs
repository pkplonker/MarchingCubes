using System;
using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

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

		using var t = new Timer(time => Debug.Log($"Generate All took {time/(axis.x * axis.y * axis.z) }ms average"));

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
					chunk.Init(noiseGenerator, ChunkSize, noiseData);
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
}