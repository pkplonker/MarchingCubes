
using UnityEngine;

public class Fly : MonoBehaviour
{
	public float speedMultiplier = 5.0f;

	public float speed = 5.0f;
	public float sensitivity = 2.0f;
	private Vector3 lastMousePosition;
	private float currentSpeed;

	private void Start()
	{
		lastMousePosition = Input.mousePosition;
	}

	void Update()
	{
		if (Input.GetMouseButtonDown(1))
		{
			lastMousePosition = Input.mousePosition;
		}

		if (Input.GetMouseButton(1))
		{
			Vector3 delta = Input.mousePosition - lastMousePosition;
			lastMousePosition = Input.mousePosition;

			transform.eulerAngles += new Vector3(-delta.y * sensitivity, delta.x * sensitivity, 0);
		}

		// Movement
		Vector3 dir = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
		if (Input.GetKey(KeyCode.E)) dir.y = 1;
		if (Input.GetKey(KeyCode.Q)) dir.y = -1;
		if (Input.GetKey(KeyCode.LeftShift)) currentSpeed = speed * speedMultiplier;
		else currentSpeed = speed;

		transform.Translate(dir * (currentSpeed * Time.deltaTime));
	}
}