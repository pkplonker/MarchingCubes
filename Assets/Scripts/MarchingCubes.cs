using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public class MarchingCubes : MonoBehaviour
{
	[SerializeField]
	private float threshHold = 0.5f;

	[SerializeField]
	private bool drawDebug = true;
	[SerializeField]
	private PointCloudRenderType type = PointCloudRenderType.All;

	private List<Vector3> vertices = new();
	private List<int> indicies = new();
	public enum PointCloudRenderType
	{
		All,
		Positive,
		Negative
	}

	[SerializeField]
	private Vector3Int size = new Vector3Int(16, 16, 16);

	[SerializeField]
	private float stepSize = 1f;

	private float[] pointCloud;
	private float minimum = float.MaxValue;
	private float maximum = float.MinValue;

	[SerializeField]
	private float sphereRadius = 0.2f;

	private int arraySize;
	private Vector3[] positions;

	[SerializeField]
	private int seed;

	[SerializeField]
	private int octaves;

	[SerializeField]
	private bool useUpdate = true;

	private void Start()
	{
		Create();
	}

	public void Create()
	{
		arraySize = size.x * size.y * size.z;
		NoiseGenerator.SetSeed(seed);
		NoiseGenerator.SetOctaves(octaves);
		positions = CreatePointCloudPositions(size, transform);
		CreateNoise();
	}

	private void CreateNoise()
	{
		pointCloud = NoiseGenerator.CreateNoise(positions, out var minMax);
		minimum = minMax.x;
		maximum = minMax.y;
	}

	public void Update()
	{
		if (!useUpdate) return;
		CreateNoise();
	}

	private Vector3[] CreatePointCloudPositions(Vector3Int size, Transform transform)
	{
		positions = new Vector3[size.x * size.y * size.z];
		var pos = transform.position;

		for (int i = 0; i < size.x; i++)
		{
			for (int j = 0; j < size.y; j++)
			{
				for (int k = 0; k < size.z; k++)
				{
					positions[NoiseGenerator.Position1d(this.size, i, j, k)] = new Vector3(pos.x + (i * stepSize),
						pos.y + (j * stepSize),
						pos.z + (k * stepSize));
				}
			}
		}

		return positions;
	}

	private void OnDrawGizmos()
	{
		if (!drawDebug) return;
		for (int i = 0; i < size.x; i++)
		{
			for (int j = 0; j < size.y; j++)
			{
				for (int k = 0; k < size.z; k++)
				{
					var index1d = NoiseGenerator.Position1d(size, i, j, k);
					var val = pointCloud[index1d];
					var normalizedVal = Mathf.InverseLerp(minimum, maximum, val);
					Gizmos.color = Color.Lerp(Color.white, Color.black,
						normalizedVal);
					var draw = false;
					switch (type)
					{
						case PointCloudRenderType.All:
							draw = true;
							break;
						case PointCloudRenderType.Positive:
							draw = normalizedVal > threshHold;

							break;
						case PointCloudRenderType.Negative:
							draw = normalizedVal < threshHold;
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}

					if (draw)
						Gizmos.DrawSphere(positions[index1d], sphereRadius);
				}
			}
		}
	}

	public void March()
	{
		
	}
}