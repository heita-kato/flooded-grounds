using UnityEngine;

public class ThirdPersonOrbitCamera : MonoBehaviour {
	public Transform target;
	public float distance = 4.5f;
	public float height = 1.7f;
	public float sensitivity = 120f;
	public float minPitch = -20f;
	public float maxPitch = 60f;
	public bool lockCursor = false;
	public bool webGLRightClickRotation = true;

	float yaw;
	float pitch = 12f;

	void Start(){
		Vector3 angles = transform.eulerAngles;
		yaw = angles.y;
		pitch = angles.x;

		if (Application.isEditor) {
			webGLRightClickRotation = false;
		}

		if (lockCursor) {
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	void LateUpdate(){
		if (target == null) {
			return;
		}

		bool canRotate = !webGLRightClickRotation || Input.GetMouseButton(0);
		if (canRotate) {
			yaw += Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
			pitch -= Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;
			pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
		}

		Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
		Vector3 focusPoint = target.position + Vector3.up * height;
		Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * distance);

		transform.position = desiredPosition;
		transform.rotation = rotation;
	}
}
