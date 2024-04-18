using UnityEngine;

[System.Serializable]
public class Noise 
{
	public float IsoLevel ;
	public float VertDistance;
	public float Lacunarity;
	public int Seed;
	public int Octaves;
	public float Persistance;
	public float Scale ;
	public float GroundDistanceFactor;
	public float GroundHeight;
	public float Amplitude;

	public Noise()
	{
		Lacunarity = 0;
		Seed = 0;
		Octaves = 0;
		Persistance = 0;
		IsoLevel = 0;
		VertDistance = 0;
		Scale = 0;
		GroundDistanceFactor = 10;
		GroundHeight = 10f;
		Amplitude = 30f;
	}

}