using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Digger : MonoBehaviour
{
	[SerializeField]
	private float radius = 1;

	[SerializeField]
	private float maxDigDistance;

	[SerializeField]
	private Camera camera;

	void Update()
	{
		if (Input.GetMouseButtonDown(0))
		{
			Modify();
		}

		if (Input.GetKey(KeyCode.Space))
		{
			if (Input.GetMouseButton(0))
			{
				Modify();
			}
		}
	}

	private void Modify()
	{
		if (!Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out var hit, maxDigDistance)) return;
		var chunk = hit.collider.gameObject.GetComponent<Chunk>();
		if (chunk == null) return;
		chunk.Modify(hit, radius);
	}
}