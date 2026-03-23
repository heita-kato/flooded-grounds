using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharController_Motor : MonoBehaviour {

    class DamagePopup {
        public int damage;
        public Vector2 startPos;
        public Vector2 velocity;
        public float elapsed;
        public float duration;
    }

    public enum MoveState { Idle, Walk, Run, Jump, Fall }

    [System.Serializable]
    public class TransitionBlendSetting {
        public MoveState from = MoveState.Idle;
        public MoveState to = MoveState.Walk;
        public float blendTime = 0.1f;
    }

    // --- 移動 ---
    [HideInInspector] public float walkSpeed = 2.0f;
    [HideInInspector] public float runSpeed = 8.0f;
    [HideInInspector] public float sensitivity = 30.0f;
    [HideInInspector] public float turnSpeed = 7.0f;
    [HideInInspector] public float moveStartLockDuration = 0.1f;
    [HideInInspector] public float acceleration = 5.0f;
    [HideInInspector] public float deceleration = 10.0f;
    [HideInInspector] public float WaterHeight = 15.5f;
    [HideInInspector] public float waterSurfaceSupportDepth = 1.2f;
    [HideInInspector] public float groundStickForce = 2.0f;
    [HideInInspector] public float inputDeadZone = 0.2f;

    // --- ジャンプ ---
    [HideInInspector] public float jumpForce = 6.0f;

    // --- アニメーション ---
    [HideInInspector] public float walkAnimationSpeed = 3.0f;
    [HideInInspector] public float runAnimationSpeed = 1.0f;
    CharacterController character;
    [HideInInspector] public GameObject cam;
    [HideInInspector] public Animator animator;

    // アニメーションクリップ名（Inspectorで変更可）
    [HideInInspector] public string idleStateName   = "Unreal Take 0";
    [HideInInspector] public string walkStateName   = "Unreal Take 5";
    [HideInInspector] public string runStateName    = "Unreal Take 3";
    [HideInInspector] public string jumpStateName   = "Unreal Take 1";
    [HideInInspector] public string fallStateName   = "Unreal Take 6";

    // ブレンド時間
    [HideInInspector] public float idleBlend  = 0.50f;
    [HideInInspector] public float walkBlend  = 0.10f;
    [HideInInspector] public float runBlend   = 0.10f;
    [HideInInspector] public float jumpBlend  = 0.05f;
    [HideInInspector] public float fallBlend  = 0.10f;

    // from -> to ごとのブレンド時間上書き（未設定時は各ステートの既定値を使用）
    public List<TransitionBlendSetting> transitionBlendOverrides = new List<TransitionBlendSetting>();

    [HideInInspector] public bool webGLRightClickRotation = true;

    // --- ゴースト会話 ---
    [HideInInspector] public string ghostObjectName = "Little_Ghost_ZOMbi (8)";
    [HideInInspector] public float ghostInteractDistance = 3.0f;
    [HideInInspector, Range(0f, 180f)] public float ghostFrontAngle = 60f;
    public string ghostMessage = "Hey, you there! You can become invisible by pressing the X key.";
    [HideInInspector] public float ghostMessageDuration = 2.5f;

    // --- プレイヤー体力 ---
    [Header("Player Status")]
    public int maxHealth = 30;
    public Font guiFont;

    // --- 内部状態 ---
    float moveFB, moveLR;
    float gravity = -9.8f;
    float verticalVelocity;
    float horizontalSpeed;
    float moveStartLockTimer;
    bool hadMoveInputLastFrame;
    bool isJumping;     // ジャンプ上昇中フラグ
    float ghostMessageTimer;
    float forcedIdleTimer;
    Transform ghostTransform;
    bool ghostLookupFailedLogged;
    int currentHealth;
    readonly List<DamagePopup> damagePopups = new List<DamagePopup>();
    Vector3 spawnPosition;
    Quaternion spawnRotation;

    // アニメーション状態
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

        currentHealth = Mathf.Max(1, maxHealth);
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    void CheckForWaterHeight(){
        gravity = (transform.position.y < WaterHeight) ? 0f : -9.8f;
    }

    void Update(){
        if (forcedIdleTimer > 0f)
            forcedIdleTimer = Mathf.Max(0f, forcedIdleTimer - Time.deltaTime);

        bool isForcedIdle = forcedIdleTimer > 0f;

        // --- 入力 ---
        Vector2 rawInput = GetArrowKeyInput();
        if (isForcedIdle)
            rawInput = Vector2.zero;

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
        bool isGroundedLike = character.isGrounded || IsWithinWaterSurfaceSupportRange();

        // --- 水平移動 ---
        Vector3 inputDir = Vector3.ClampMagnitude(new Vector3(moveFB, 0f, moveLR), 1f);
        Vector3 movement = GetCameraRelativeMove(inputDir);

        if (!isMoveStartLocked && movement.sqrMagnitude > 0.0001f){
            Quaternion targetRot = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        // --- 垂直速度（重力・ジャンプ） ---
        if (isGroundedLike){
            if (verticalVelocity < 0f){
                if (character.isGrounded)
                    verticalVelocity = -groundStickForce;
                else
                    verticalVelocity = 0f;

                isJumping = false;
            }
            // ジャンプ入力
            if (!isForcedIdle && Input.GetButtonDown("Jump")){
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
        UpdateAnimator(hasInput, isRunning, isGroundedLike);

        // --- ゴースト会話入力 ---
        if (IsGhostInteractPressed()){
            TryShowGhostMessage();
        }

        if (ghostMessageTimer > 0f)
            ghostMessageTimer = Mathf.Max(0f, ghostMessageTimer - Time.deltaTime);
    }

    Vector2 GetArrowKeyInput(){
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
            horizontal -= 1f;
        if (Input.GetKey(KeyCode.RightArrow))
            horizontal += 1f;
        if (Input.GetKey(KeyCode.DownArrow))
            vertical -= 1f;
        if (Input.GetKey(KeyCode.UpArrow))
            vertical += 1f;

        return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    bool IsGhostInteractPressed(){
        return Input.GetKeyDown(KeyCode.A);
    }

    void TryShowGhostMessage(){
        Transform ghost = GetGhostTransform();
        if (ghost == null)
            return;

        Vector3 toPlayer = transform.position - ghost.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        if (distance > ghostInteractDistance)
            return;

        if (distance <= 0.0001f)
            return;

        Vector3 ghostForward = ghost.forward;
        ghostForward.y = 0f;
        if (ghostForward.sqrMagnitude <= 0.0001f)
            return;

        float angle = Vector3.Angle(ghostForward.normalized, toPlayer.normalized);
        if (angle > ghostFrontAngle)
            return;

        ghostMessageTimer = ghostMessageDuration;
    }

    Transform GetGhostTransform(){
        if (ghostTransform != null)
            return ghostTransform;

        GameObject ghostObj = GameObject.Find(ghostObjectName);
        if (ghostObj == null){
            if (!ghostLookupFailedLogged){
                Debug.LogWarning("Ghost object not found: " + ghostObjectName);
                ghostLookupFailedLogged = true;
            }
            return null;
        }

        ghostTransform = ghostObj.transform;
        return ghostTransform;
    }

    Vector3 GetCameraRelativeMove(Vector3 inputDirection){
        if (cam == null)
            return transform.TransformDirection(inputDirection);

        Vector3 fwd   = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = cam.transform.right;   right.y = 0f; right.Normalize();
        return Vector3.ClampMagnitude(fwd * inputDirection.z + right * inputDirection.x, 1f);
    }

    void UpdateAnimator(bool hasInput, bool isRunning, bool isGroundedLike){
        if (animator == null) return;

        // 優先度：空中(ジャンプ/落下) > 走る > 歩く > 待機
        MoveState targetAnim;
        if (!isGroundedLike){
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

        if (TryCrossFade(currentAnim, targetAnim)){
            transitioned = true;
            resolvedAnim = targetAnim;
        }

        if (!transitioned && targetAnim == MoveState.Walk){
            if (TryCrossFade(currentAnim, MoveState.Run)){
                transitioned = true;
                resolvedAnim = MoveState.Run;
            } else if (TryCrossFade(currentAnim, MoveState.Idle)){
                transitioned = true;
                resolvedAnim = MoveState.Idle;
            }
        }

        if (!transitioned && targetAnim == MoveState.Run){
            if (TryCrossFade(currentAnim, MoveState.Walk)){
                transitioned = true;
                resolvedAnim = MoveState.Walk;
            } else if (TryCrossFade(currentAnim, MoveState.Idle)){
                transitioned = true;
                resolvedAnim = MoveState.Idle;
            }
        }

        if (!transitioned && targetAnim == MoveState.Jump){
            if (TryCrossFade(currentAnim, MoveState.Fall)){
                transitioned = true;
                resolvedAnim = MoveState.Fall;
            } else if (TryCrossFade(currentAnim, MoveState.Idle)){
                transitioned = true;
                resolvedAnim = MoveState.Idle;
            }
        }

        if (!transitioned && targetAnim == MoveState.Fall){
            if (TryCrossFade(currentAnim, MoveState.Jump)){
                transitioned = true;
                resolvedAnim = MoveState.Jump;
            } else if (TryCrossFade(currentAnim, MoveState.Idle)){
                transitioned = true;
                resolvedAnim = MoveState.Idle;
            }
        }

        if (!transitioned && targetAnim != MoveState.Idle && TryCrossFade(currentAnim, MoveState.Idle)){
            transitioned = true;
            resolvedAnim = MoveState.Idle;
        }

        if (transitioned)
            currentAnim = resolvedAnim;
    }

    bool IsWithinWaterSurfaceSupportRange(){
        if (waterSurfaceSupportDepth <= 0f)
            return false;

        if (transform.position.y >= WaterHeight)
            return false;

        float depthFromSurface = WaterHeight - transform.position.y;
        return depthFromSurface <= waterSurfaceSupportDepth;
    }

    bool TryCrossFade(MoveState fromState, MoveState toState){
        int stateHash;
        float blendTime = GetBlendTime(fromState, toState);

        switch (toState){
            case MoveState.Idle:
                stateHash = idleHash;
                break;
            case MoveState.Walk:
                stateHash = walkHash;
                break;
            case MoveState.Run:
                stateHash = runHash;
                break;
            case MoveState.Jump:
                stateHash = jumpHash;
                break;
            default:
                stateHash = fallHash;
                break;
        }

        // 対象ステートが存在するときだけ遷移
        if (!animator.HasState(0, stateHash))
            return false;

            animator.CrossFade(stateHash, blendTime, 0);

        return true;
    }

    float GetBlendTime(MoveState fromState, MoveState toState){
        if (transitionBlendOverrides != null){
            for (int i = 0; i < transitionBlendOverrides.Count; i++){
                TransitionBlendSetting setting = transitionBlendOverrides[i];
                if (setting == null)
                    continue;

                if (setting.from == fromState && setting.to == toState)
                    return Mathf.Max(0f, setting.blendTime);
            }
        }

        switch (toState){
            case MoveState.Idle:
                return idleBlend;
            case MoveState.Walk:
                return walkBlend;
            case MoveState.Run:
                return runBlend;
            case MoveState.Jump:
                return jumpBlend;
            default:
                return fallBlend;
        }
    }

    void OnGUI(){
        // プレイヤー座標をデバッグ表示
        Vector3 playerPos = transform.position;
        string debugText = $"Player Position: X: {playerPos.x:F2}, Y: {playerPos.y:F2}, Z: {playerPos.z:F2}";

        GUIStyle debugLabelStyle = new GUIStyle(GUI.skin.label);
        debugLabelStyle.normal.textColor = Color.white;
        if (guiFont != null)
            debugLabelStyle.font = guiFont;
        
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 400, 30), debugText, debugLabelStyle);
        
        // 追加情報：速度、状態
        string stateText = $"State: {currentAnim} | Speed: {horizontalSpeed:F2} m/s | Grounded: {character.isGrounded}";
        GUI.Label(new Rect(10, 40, 400, 30), stateText, debugLabelStyle);

        string hpText = $"HP: {currentHealth}/{Mathf.Max(1, maxHealth)}";
        GUI.Label(new Rect(10, 70, 400, 30), hpText, debugLabelStyle);

        DrawHealthGaugeTopRight();
        DrawDamagePopups();

        if (ghostMessageTimer > 0f){
            Transform ghost = GetGhostTransform();
            Camera dialogueCamera = Camera.main;
            if (dialogueCamera == null && cam != null)
                dialogueCamera = cam.GetComponent<Camera>();

            if (ghost != null && dialogueCamera != null){
                Vector3 bubbleWorldPos = ghost.position + Vector3.up * 2.0f;
                Vector3 screenPos = dialogueCamera.WorldToScreenPoint(bubbleWorldPos);

                if (screenPos.z > 0f){
                    float bubbleWidth = 380f;
                    float bubbleHeight = 70f;
                    float bubbleX = screenPos.x - (bubbleWidth * 0.5f);
                    float bubbleY = Screen.height - screenPos.y - bubbleHeight - 20f;

                    Rect bubbleRect = new Rect(bubbleX, bubbleY, bubbleWidth, bubbleHeight);
                    GUI.color = new Color(1f, 1f, 1f, 0.95f);
                    GUI.Box(bubbleRect, "");
                    GUI.color = Color.white;
                    GUIStyle ghostMessageStyle = new GUIStyle(GUI.skin.label);
                    ghostMessageStyle.alignment = TextAnchor.MiddleLeft;
                    ghostMessageStyle.normal.textColor = Color.white;
                    if (guiFont != null)
                        ghostMessageStyle.font = guiFont;
                    GUI.Label(new Rect(bubbleX + 10f, bubbleY + 10f, bubbleWidth - 20f, bubbleHeight - 20f), ghostMessage, ghostMessageStyle);
                }
            }
        }

        GUI.color = Color.white;
    }

    public int GetCurrentHealth(){
        return currentHealth;
    }

    public void ApplySkeletonHit(int damage, float forceIdleSeconds){
        int finalDamage = Mathf.Clamp(damage, 0, 9999);
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        forcedIdleTimer = Mathf.Max(forcedIdleTimer, Mathf.Max(0f, forceIdleSeconds));

        if (finalDamage > 0)
            AddDamagePopup(finalDamage);

        if (currentHealth <= 0)
            RespawnPlayer();
    }

    void RespawnPlayer(){
        if (character != null)
            character.enabled = false;

        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        if (character != null)
            character.enabled = true;

        currentHealth = Mathf.Max(1, maxHealth);
        verticalVelocity = 0f;
        horizontalSpeed = 0f;
        forcedIdleTimer = 0f;
        damagePopups.Clear();
    }

    void DrawHealthGaugeTopRight(){
        float gaugeWidth = 250f;
        float gaugeHeight = 24f;
        float margin = 20f;

        float x = Screen.width - gaugeWidth - margin;
        float y = margin;
        Rect gaugeRect = new Rect(x, y, gaugeWidth, gaugeHeight);
        Rect fillRect = new Rect(x + 2f, y + 2f, (gaugeWidth - 4f) * Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth)), gaugeHeight - 4f);

        Color prev = GUI.color;
        GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        GUI.DrawTexture(gaugeRect, Texture2D.whiteTexture);

        GUI.color = new Color(0.15f, 0.85f, 0.2f, 0.95f);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUIStyle hpStyle = new GUIStyle(GUI.skin.label);
        hpStyle.alignment = TextAnchor.MiddleCenter;
        hpStyle.fontStyle = FontStyle.Bold;
        hpStyle.normal.textColor = Color.white;
        if (guiFont != null)
            hpStyle.font = guiFont;
        GUI.Label(gaugeRect, $"HP {currentHealth}/{Mathf.Max(1, maxHealth)}", hpStyle);
        GUI.color = prev;
    }

    void AddDamagePopup(int damage){
        DamagePopup popup = new DamagePopup();
        popup.damage = damage;
        popup.startPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
        popup.velocity = new Vector2(Random.Range(-40f, 40f), Random.Range(-130f, -90f));
        popup.elapsed = 0f;
        popup.duration = 0.7f;
        damagePopups.Add(popup);
    }

    void DrawDamagePopups(){
        if (damagePopups.Count == 0)
            return;

        GUIStyle damageStyle = new GUIStyle(GUI.skin.label);
        damageStyle.alignment = TextAnchor.MiddleCenter;
        damageStyle.fontSize = 30;
        damageStyle.fontStyle = FontStyle.Bold;
        if (guiFont != null)
            damageStyle.font = guiFont;

        Matrix4x4 prevMatrix = GUI.matrix;

        for (int i = damagePopups.Count - 1; i >= 0; i--){
            DamagePopup popup = damagePopups[i];
            popup.elapsed += Time.deltaTime;
            float t = popup.duration <= 0f ? 1f : Mathf.Clamp01(popup.elapsed / popup.duration);

            if (t >= 1f){
                damagePopups.RemoveAt(i);
                continue;
            }

            Vector2 pos = popup.startPos + popup.velocity * t;
            float popScale = 1f + Mathf.Sin(Mathf.Clamp01(t / 0.25f) * Mathf.PI) * 0.55f;
            float alpha = 1f - t;

            damageStyle.normal.textColor = new Color(1f, 0.15f, 0.15f, alpha);

            Rect textRect = new Rect(pos.x - 70f, pos.y - 25f, 140f, 50f);
            Vector2 pivot = new Vector2(textRect.x + textRect.width * 0.5f, textRect.y + textRect.height * 0.5f);

            GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.identity, Vector3.one) *
                         Matrix4x4.Scale(new Vector3(popScale, popScale, 1f)) *
                         Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);

            GUI.Label(textRect, $"-{popup.damage}", damageStyle);
            GUI.matrix = prevMatrix;
        }
    }
}