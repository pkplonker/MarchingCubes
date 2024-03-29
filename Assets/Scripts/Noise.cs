using UnityEngine;

[System.Serializable]
public class Noise 
{
	public float IsoLevel ;
	public float VertDistance;
	public float NoiseLacunarity;
	public int NoiseSeed;
	public int NoiseOctaves;
	public float NoisePersistance;
	public float NoiseScale ;

	public Noise()
	{
		NoiseLacunarity = 0;
		NoiseSeed = 0;
		NoiseOctaves = 0;
		NoisePersistance = 0;
		IsoLevel = 0;
		VertDistance = 0;
		NoiseScale = 0;
	}
}