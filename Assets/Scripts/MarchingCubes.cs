using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubes : MonoBehaviour
{
	private enum GizmoMode
	{
		All,
		Above,
		Below
	}

	[Header("Debug")]
	[SerializeField]
	private GizmoMode gizmoMode = GizmoMode.All;

	[SerializeField]
	private float gizmoRadius = 0.2f;

	[SerializeField]
	private int3 Size = new(50, 50, 50);

	[Range(-1.0f, 1.0f)]
	[SerializeField]
	private float IsoLevel = 0.2f;

	[Range(0.0f, 0.5f)]
	[SerializeField]
	private float VertDistance = 1f;

	[SerializeField]
	private Material Material;

	private float[,,] scalarField;

	[Header("Noise")]
	[Range(0.0f, 1.0f)]
	[SerializeField]
	private float NoiseLacunarity;

	[SerializeField]
	private float3 NoiseOffset;

	[SerializeField]
	private int NoiseSeed;

	[Range(1, 8)]
	[SerializeField]
	private int NoiseOctaves;

	[Range(0f, 2f)]
	[SerializeField]
	private float NoisePersistance;

	[Range(0f, 1000f)]
	[SerializeField]
	private float NoiseScale = 16f;

	private float[] scalerField;
	private float[,,] noiseMap;
	private float[] flattenedScalerField;

	private void Start()
	{
		CreateNoise();
	}

	public void CreateNoise()
	{
		noiseMap = NoiseTerrain3D.GenerateNoiseMap(Size, NoiseSeed, NoiseScale, NoiseOctaves, NoisePersistance,
			NoiseLacunarity, NoiseOffset);
	}

	private void OnDrawGizmosSelected()
	{
		float max = float.MinValue;
		float min = float.MaxValue;

		var pos = transform.position;
		for (int x = 0; x < noiseMap.GetLength(0); x++)
		{
			for (int y = 0; y < noiseMap.GetLength(1); y++)
			{
				for (int z = 0; z < noiseMap.GetLength(2); z++)
				{
					var noise = noiseMap[x, y, z];

					if (noise > max)
					{
						max = noise;
					}

					if (noise < min)
					{
						min = noise;
					}
					Gizmos.color = Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(0, 1, noise));

					switch (gizmoMode)
					{
						case GizmoMode.All:
							Gizmos.color = Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(0, 1, noise));
							Gizmos.DrawSphere(pos + (new Vector3(x, y, z) * VertDistance), gizmoRadius);
							break;
						case GizmoMode.Above:
							if (noise > IsoLevel)
							{
								Gizmos.DrawSphere(pos + (new Vector3(x, y, z) * VertDistance),
									gizmoRadius);
							}

							break;
						case GizmoMode.Below:
							if (noise < IsoLevel)
							{
								Gizmos.DrawSphere(pos + (new Vector3(x, y, z) * VertDistance),
									gizmoRadius);
							}

							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}
			}
		}
		//Debug.Log($"{min} : {max}");
	}

	private void OnDrawGizmos() { }

	public void March()
	{
		for (int i = 0; i < flattenedScalerField.Length; i++)
		{
			float[] cubeCorners = new float[8];
			for (int j = 0; j < 8; j++) { }
		}
	}
}