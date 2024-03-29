public static class DummyNoise
{
	public static float[] CreateNoiseMap(int size)
	{
		int totalSize = size * size * size;
		float[] noiseMap = new float[totalSize];

		for (int x = 0; x < size; x++)
		{
			for (int y = 0; y < size; y++)
			{
				for (int z = 0; z < size; z++)
				{
					bool isEdge = x == 0 || x == size - 1 || y == 0 || y == size - 1 || z == 0 || z == size - 1;
					noiseMap[x + size * (y + size * z)] = isEdge ? 1.0f : 0.0f;
				}
			}
		}

		return noiseMap;
	}
}