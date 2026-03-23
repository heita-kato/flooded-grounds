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

	[Header("Ghost Dialogue Camera")]
	public float dialogueSideOffset = 1.8f;
	public float dialogueHeightOffset = 1.6f;
	public float dialogueDistanceBack = 2.6f;
	public float dialogueFocusHeight = 1.35f;
	public float dialogueMoveLerp = 8f;
	public float dialogueLookLerp = 10f;
	public float dialogueFov = 42f;
	public float dialogueFovLerp = 8f;

	float yaw;
	float pitch = 12f;
	bool isDialogueMode;
	Transform dialogueGhost;
	Transform dialoguePlayer;
	Camera cam;
	float defaultFov = 60f;

	void Start(){
		Vector3 angles = transform.eulerAngles;
		yaw = angles.y;
		pitch = angles.x;
		cam = GetComponent<Camera>();
		if (cam != null)
			defaultFov = cam.fieldOfView;

		if (Application.isEditor) {
			webGLRightClickRotation = false;
		}

		if (lockCursor) {
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	void LateUpdate(){
		if (isDialogueMode && dialogueGhost != null && dialoguePlayer != null) {
			UpdateDialogueCamera();
			return;
		}

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

		if (cam != null) {
			cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, defaultFov, dialogueFovLerp * Time.deltaTime);
		}
	}

	void UpdateDialogueCamera() {
		Vector3 toPlayer = dialoguePlayer.position - dialogueGhost.position;
		toPlayer.y = 0f;
		if (toPlayer.sqrMagnitude <= 0.0001f)
			toPlayer = dialogueGhost.forward;

		Vector3 forward = toPlayer.normalized;
		Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

		Vector3 focusPoint = ((dialogueGhost.position + dialoguePlayer.position) * 0.5f) + Vector3.up * dialogueFocusHeight;
		Vector3 desiredPosition = dialogueGhost.position + Vector3.up * dialogueHeightOffset + side * dialogueSideOffset - forward * dialogueDistanceBack;

		transform.position = Vector3.Lerp(transform.position, desiredPosition, dialogueMoveLerp * Time.deltaTime);

		Quaternion targetRot = Quaternion.LookRotation((focusPoint - transform.position).normalized, Vector3.up);
		transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dialogueLookLerp * Time.deltaTime);

		if (cam != null) {
			cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, dialogueFov, dialogueFovLerp * Time.deltaTime);
		}
	}

	public void BeginGhostDialogue(Transform ghost, Transform player) {
		dialogueGhost = ghost;
		dialoguePlayer = player;
		isDialogueMode = dialogueGhost != null && dialoguePlayer != null;
	}

	public void EndGhostDialogue() {
		isDialogueMode = false;
		dialogueGhost = null;
		dialoguePlayer = null;
	}
}
