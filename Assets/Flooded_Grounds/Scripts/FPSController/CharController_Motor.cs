using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharController_Motor : MonoBehaviour {

	public float speed = 10.0f;
	public float sensitivity = 30.0f;
	public float turnSpeed = 14.0f;
	public float WaterHeight = 15.5f;
	public float groundStickForce = 2.0f;
	CharacterController character;
	public GameObject cam;
	public Animator animator;
	public string animatorSpeedParameter = "Speed";
	public float animatorDampTime = 0.08f;
	bool hasAnimatorSpeedParameter;
	float moveFB, moveLR;
	public bool webGLRightClickRotation = true;
	float gravity = -9.8f;
	float verticalVelocity;


	void Start(){
		character = GetComponent<CharacterController> ();
		if (animator == null) {
			animator = GetComponentInChildren<Animator> ();
		}
		hasAnimatorSpeedParameter = AnimatorHasFloatParameter (animator, animatorSpeedParameter);

		if (Application.isEditor) {
			webGLRightClickRotation = false;
			sensitivity = sensitivity * 1.5f;
		}
	}


	void CheckForWaterHeight(){
		if (transform.position.y < WaterHeight) {
			gravity = 0f;			
		} else {
			gravity = -9.8f;
		}
	}



	void Update(){
		moveFB = Input.GetAxis ("Horizontal");
		moveLR = Input.GetAxis ("Vertical");

		CheckForWaterHeight ();

		Vector3 inputDirection = new Vector3 (moveFB, 0f, moveLR);
		inputDirection = Vector3.ClampMagnitude (inputDirection, 1f);

		Vector3 movement = GetCameraRelativeMove (inputDirection);

		if (movement.sqrMagnitude > 0.0001f) {
			Quaternion targetRotation = Quaternion.LookRotation (movement, Vector3.up);
			transform.rotation = Quaternion.Slerp (transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
		}

		if (character.isGrounded && verticalVelocity < 0f) {
			verticalVelocity = -groundStickForce;
		}

		verticalVelocity += gravity * Time.deltaTime;
		Vector3 finalMovement = (movement * speed) + (Vector3.up * verticalVelocity);
		character.Move (finalMovement * Time.deltaTime);

		UpdateAnimator (movement.magnitude);
	}


	Vector3 GetCameraRelativeMove(Vector3 inputDirection){
		if (cam == null) {
			return transform.TransformDirection (inputDirection);
		}

		Transform cameraTransform = cam.transform;
		Vector3 cameraForward = cameraTransform.forward;
		Vector3 cameraRight = cameraTransform.right;
		cameraForward.y = 0f;
		cameraRight.y = 0f;

		cameraForward.Normalize ();
		cameraRight.Normalize ();

		Vector3 movement = (cameraForward * inputDirection.z) + (cameraRight * inputDirection.x);
		return Vector3.ClampMagnitude (movement, 1f);
	}

	void UpdateAnimator(float moveAmount){
		if (animator == null || !hasAnimatorSpeedParameter) {
			return;
		}

		// 移動量が小さい場合は Idle（Speed = 0）に、そうでない場合は Walk/Run（Speed = moveAmount）に
		float targetSpeed = moveAmount > 0.1f ? moveAmount : 0f;
		animator.SetFloat (animatorSpeedParameter, targetSpeed, animatorDampTime, Time.deltaTime);
	}

	bool AnimatorHasFloatParameter(Animator targetAnimator, string parameterName){
		if (targetAnimator == null || string.IsNullOrEmpty (parameterName)) {
			return false;
		}

		AnimatorControllerParameter[] parameters = targetAnimator.parameters;
		for (int i = 0; i < parameters.Length; i++) {
			if (parameters[i].name == parameterName && parameters[i].type == AnimatorControllerParameterType.Float) {
				return true;
			}
		}

		return false;
	}



}
