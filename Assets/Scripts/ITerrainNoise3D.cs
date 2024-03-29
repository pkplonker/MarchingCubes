using UnityEngine;

public interface ITerrainNoise3D
{
	float[] GenerateNoiseMap(Vector3Int dimensions, int seed, float scale, int octaves,
		float persistance,
		float lacunarity,Vector3 offset);
}