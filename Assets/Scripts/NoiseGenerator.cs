using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class NoiseGenerator
{
	public static float[] CreateNoise(Vector3[] positions, out Vector2 minMax)
	{
		var pointCloud = NoiseS3D.NoiseArrayGPU(positions);
		minMax = new Vector2(pointCloud.Min(), pointCloud.Max());
		return pointCloud;
	}

	public static int Position1d(Vector3Int size, int i, int j, int k) => i + j * size.y + k * size.y * size.z;

	public static void SetSeed(int seed)
	{
		NoiseS3D.seed = seed;
	}

	public static void SetOctaves(int octaves)
	{
		NoiseS3D.octaves = octaves;
	}
}