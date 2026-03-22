using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharController_Motor : MonoBehaviour {

    // --- 移動 ---
    public float walkSpeed = 10.0f;
    public float runSpeed = 20.0f;
    public float sensitivity = 30.0f;
    public float turnSpeed = 14.0f;
    public float moveStartLockDuration = 0.1f;
    public float acceleration = 1.0f;
    public float deceleration = 18.0f;
    public float WaterHeight = 15.5f;
    public float groundStickForce = 2.0f;
    public float inputDeadZone = 0.2f;

    // --- ジャンプ ---
    public float jumpForce = 6.0f;

    // --- アニメーション ---
    public float walkAnimationSpeed = 0.5f;
    public float runAnimationSpeed = 1.0f;
    CharacterController character;
    public GameObject cam;
    public Animator animator;

    // アニメーションクリップ名（Inspectorで変更可）
    public string idleStateName   = "Unreal Take 0";
    public string walkStateName   = "Unreal Take 5";
    public string runStateName    = "Unreal Take 3";
    public string jumpStateName   = "Unreal Take 1";
    public string fallStateName   = "Unreal Take 6";

    // ブレンド時間
    public float idleBlend  = 0.15f;
    public float walkBlend  = 0.10f;
    public float runBlend   = 0.10f;
    public float jumpBlend  = 0.05f;
    public float fallBlend  = 0.10f;

    public bool webGLRightClickRotation = true;

    // --- 内部状態 ---
    float moveFB, moveLR;
    float gravity = -9.8f;
    float verticalVelocity;
    float horizontalSpeed;
    float moveStartLockTimer;
    bool hadMoveInputLastFrame;
    bool isJumping;     // ジャンプ上昇中フラグ

    // アニメーション状態
    enum MoveState { Idle, Walk, Run, Jump, Fall }
    MoveState currentAnim = MoveState.Idle;

    // ハッシュキャッシュ
    int idleHash, walkHash, runHash, jumpHash, fallHash;

    void Start(){
        character = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        idleHash = Animator.StringToHash(idleStateName);
        walkHash = Animator.StringToHash(walkStateName);
        runHash  = Animator.StringToHash(runStateName);
        jumpHash = Animator.StringToHash(jumpStateName);
        fallHash = Animator.StringToHash(fallStateName);

        if (Application.isEditor){
            webGLRightClickRotation = false;
            sensitivity *= 1.5f;
        }
    }

    void CheckForWaterHeight(){
        gravity = (transform.position.y < WaterHeight) ? 0f : -9.8f;
    }

    void Update(){
        // --- 入力 ---
        Vector2 rawInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (rawInput.sqrMagnitude < inputDeadZone * inputDeadZone)
            rawInput = Vector2.zero;

        moveFB = rawInput.x;
        moveLR = rawInput.y;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool hasInput  = rawInput.sqrMagnitude > 0.0001f;

        if (hasInput && !hadMoveInputLastFrame)
            moveStartLockTimer = moveStartLockDuration;
        hadMoveInputLastFrame = hasInput;

        if (!hasInput)
            moveStartLockTimer = 0f;

        bool isMoveStartLocked = moveStartLockTimer > 0f;
        if (isMoveStartLocked)
            moveStartLockTimer = Mathf.Max(0f, moveStartLockTimer - Time.deltaTime);

        float baseSpeed = isRunning ? runSpeed : walkSpeed;
        float targetSpeed = hasInput ? baseSpeed * Mathf.Clamp01(rawInput.magnitude) : 0f;

        float speedChangeRate = targetSpeed > horizontalSpeed ? acceleration : deceleration;
        if (speedChangeRate > 0f)
            horizontalSpeed = Mathf.MoveTowards(horizontalSpeed, targetSpeed, speedChangeRate * Time.deltaTime);
        else
            horizontalSpeed = targetSpeed;

        if (isMoveStartLocked)
            horizontalSpeed = 0f;

        CheckForWaterHeight();

        // --- 水平移動 ---
        Vector3 inputDir = Vector3.ClampMagnitude(new Vector3(moveFB, 0f, moveLR), 1f);
        Vector3 movement = GetCameraRelativeMove(inputDir);

        if (!isMoveStartLocked && movement.sqrMagnitude > 0.0001f){
            Quaternion targetRot = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        // --- 垂直速度（重力・ジャンプ） ---
        if (character.isGrounded){
            if (verticalVelocity < 0f){
                verticalVelocity = -groundStickForce;
                isJumping = false;
            }
            // ジャンプ入力
            if (Input.GetButtonDown("Jump")){
                verticalVelocity = jumpForce;
                isJumping = true;
            }
        } else {
            // 上昇が終わったらジャンプフラグ解除（落下へ）
            if (verticalVelocity < 0f)
                isJumping = false;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 finalMovement = (movement * horizontalSpeed) + (Vector3.up * verticalVelocity);
        character.Move(finalMovement * Time.deltaTime);

        // --- アニメーション更新 ---
        UpdateAnimator(hasInput, isRunning);
    }

    Vector3 GetCameraRelativeMove(Vector3 inputDirection){
        if (cam == null)
            return transform.TransformDirection(inputDirection);

        Vector3 fwd   = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = cam.transform.right;   right.y = 0f; right.Normalize();
        return Vector3.ClampMagnitude(fwd * inputDirection.z + right * inputDirection.x, 1f);
    }

    void UpdateAnimator(bool hasInput, bool isRunning){
        if (animator == null) return;

        // 優先度：空中(ジャンプ/落下) > 走る > 歩く > 待機
        MoveState targetAnim;
        if (!character.isGrounded){
            targetAnim = isJumping ? MoveState.Jump : MoveState.Fall;
        } else if (hasInput && isRunning){
            targetAnim = MoveState.Run;
        } else if (hasInput){
            targetAnim = MoveState.Walk;
        } else {
            targetAnim = MoveState.Idle;
        }

        // 同じ状態なら遷移しない（毎フレームCrossFadeを呼ばない）
        if (targetAnim == currentAnim) return;

        // ステートが無効な場合はフォールバックして、遷移不能で固まるのを防ぐ。
        bool transitioned = false;
        MoveState resolvedAnim = targetAnim;

        if (TryCrossFade(targetAnim)){
            transitioned = true;
            resolvedAnim = targetAnim;
        }

        if (!transitioned && targetAnim == MoveState.Walk){
            if (TryCrossFade(MoveState.Run)){
                transitioned = true;
                resolvedAnim = MoveState.Run;
            } else if (TryCrossFade(MoveState.Idle)){
                transitioned = true;
                resolvedAnim = MoveState.Idle;
            }
        }

        if (!transitioned && targetAnim == MoveState.Run){
            if (TryCrossFade(MoveState.Walk)){
                transitioned = true;
                resolvedAnim = MoveState.Walk;
            } else if (TryCrossFade(MoveState.Idle)){
                transitioned = true;
                resolvedAnim = MoveState.Idle;
            }
        }

        if (!transitioned && targetAnim == MoveState.Jump){
            if (TryCrossFade(MoveState.Fall)){
                transitioned = true;
                resolvedAnim = MoveState.Fall;
            } else if (TryCrossFade(MoveState.Idle)){
                transitioned = true;
                resolvedAnim = MoveState.Idle;
            }
        }

        if (!transitioned && targetAnim == MoveState.Fall){
            if (TryCrossFade(MoveState.Jump)){
                transitioned = true;
                resolvedAnim = MoveState.Jump;
            } else if (TryCrossFade(MoveState.Idle)){
                transitioned = true;
                resolvedAnim = MoveState.Idle;
            }
        }

        if (!transitioned && targetAnim != MoveState.Idle && TryCrossFade(MoveState.Idle)){
            transitioned = true;
            resolvedAnim = MoveState.Idle;
        }

        if (transitioned)
            currentAnim = resolvedAnim;
    }

    bool TryCrossFade(MoveState state){
        int stateHash;
        float blendTime;

        switch (state){
            case MoveState.Idle:
                stateHash = idleHash;
                blendTime = idleBlend;
                break;
            case MoveState.Walk:
                stateHash = walkHash;
                blendTime = walkBlend;
                break;
            case MoveState.Run:
                stateHash = runHash;
                blendTime = runBlend;
                break;
            case MoveState.Jump:
                stateHash = jumpHash;
                blendTime = jumpBlend;
                break;
            default:
                stateHash = fallHash;
                blendTime = fallBlend;
                break;
        }

        // 対象ステートが存在するときだけ遷移
        if (!animator.HasState(0, stateHash))
            return false;

            animator.CrossFade(stateHash, blendTime, 0);

        return true;
    }
}