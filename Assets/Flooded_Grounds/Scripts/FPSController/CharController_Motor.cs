using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    public string[] ghostMessages = new string[] {
        "Hey, you there!",
        "You can become invisible by pressing X key.",
        "Press A key to continue conversations and stay sharp out there.",
        "Good luck!"
    };

    // --- プレイヤー体力 ---
    [Header("Player Status")]
    public int maxHealth = 30;
    public Font guiFont;

    [Header("Invisibility (Dissolve)")]
    public KeyCode invisibilityToggleKey = KeyCode.X;
    public float dissolveTransitionSeconds = 0.45f;
    [Range(0f, 1f)] public float visibleDissolveAmount = 0f;
    [Range(0f, 1f)] public float invisibleDissolveAmount = 1f;
    public Material dissolveFallbackMaterial;
    public bool logInvisibilityIssues = true;

    [Header("HUD")]
    public float invisIconFadeSpeed = 7.5f;
    public float radarSize = 140f;
    [Range(0.2f, 1f)] public float radarDetectionRangeScale = 0.65f;
    public Vector2 radarWorldMinXZ = new Vector2(-120f, -120f);
    public Vector2 radarWorldMaxXZ = new Vector2(120f, 120f);
    public float radarEnemyRefreshSeconds = 0.75f;

    [Header("HUD Status VFX")]
    public GameObject hpGaugeSideVfxPrefab;
    public Vector2 hpGaugeSideVfxOffset = new Vector2(52f, 12f);
    public Vector3 hpGaugeSideVfxScale = new Vector3(0.42f, 0.42f, 0.42f);

    // --- 内部状態 ---
    float moveFB, moveLR;
    float gravity = -9.8f;
    float verticalVelocity;
    float horizontalSpeed;
    float moveStartLockTimer;
    bool hadMoveInputLastFrame;
    bool isJumping;     // ジャンプ上昇中フラグ
    bool isGhostDialogueActive;
    int ghostMessageIndex;
    float forcedIdleTimer;
    Transform ghostTransform;
    ThirdPersonOrbitCamera orbitCamera;
    bool ghostLookupFailedLogged;
    int currentHealth;
    readonly List<DamagePopup> damagePopups = new List<DamagePopup>();
    Vector3 spawnPosition;
    Quaternion spawnRotation;
    readonly List<Material> dissolveMaterials = new List<Material>();
    readonly List<Material> runtimeDissolveInstances = new List<Material>();
    Coroutine dissolveRoutine;
    bool isInvisible;
    float invisibilityIconBlend;
    float currentDissolveAmount;
    const string DissolveAmountProperty = "_DissolveAmount";
    const string MainTexProperty = "_MainTex";
    const string BaseMapProperty = "_BaseMap";
    const string ColorProperty = "_Color";
    const string BaseColorProperty = "_BaseColor";
    Texture2D hpGaugeBackgroundTex;
    Texture2D hpGaugeFillTex;
    Texture2D hpGaugeFrameTex;
    Texture2D hpGaugeFillGlowTex;
    Texture2D hpGaugeFrameGlowTex;
    Texture2D invisIconOnTex;
    Texture2D invisIconOffTex;
    Texture2D invisIconPanelTex;
    Texture2D radarEnemyIconTex;
    Texture2D radarGhostIconTex;
    Texture2D radarPlayerNormalIconTex;
    Texture2D radarPlayerIconTex;
    int hpGaugeTextureWidth = -1;
    int hpGaugeTextureHeight = -1;
    float hpGaugePulseTimer;
    const float HpGaugePulseDuration = 0.45f;
    const string RadarEnemyIconResourcePath = "Icon/daemon-skull";
    const string RadarGhostIconResourcePath = "Icon/fairy";
    const string RadarPlayerNormalIconResourcePath = "Icon/visored-helm-normal";
    const string RadarPlayerIconResourcePath = "Icon/visored-helm";
    const string HpGaugeSideVfxPrefabPath = "Assets/VisualX_Studio/App_UI_Icon_animation_FREE/VFX/Prefabs (color)/06 equalizer_01.prefab";
    readonly List<Transform> radarEnemyTargets = new List<Transform>();
    float radarEnemyRefreshTimer;
    Canvas hudOverlayCanvas;
    bool createdHudOverlayCanvas;
    GameObject hpGaugeSideVfxInstance;
    RectTransform hpGaugeSideVfxRect;

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
        if (cam != null)
            orbitCamera = cam.GetComponent<ThirdPersonOrbitCamera>();

        BuildDissolveMaterialsFromTemplate();
        ApplyDissolveAmountInstant(visibleDissolveAmount);
        invisibilityIconBlend = isInvisible ? 1f : 0f;
        EnsureRadarIconsLoaded();
        TryAssignHudVfxPrefabsInEditor();
        EnsureHudOverlayCanvas();
        EnsureHpGaugeSideVfx();
    }

    void CheckForWaterHeight(){
        gravity = (transform.position.y < WaterHeight) ? 0f : -9.8f;
    }

    void Update(){
        if (Input.GetKeyDown(invisibilityToggleKey))
            SetInvisibility(!isInvisible);

        float iconTarget = isInvisible ? 1f : 0f;
        invisibilityIconBlend = Mathf.MoveTowards(invisibilityIconBlend, iconTarget, Mathf.Max(0.01f, invisIconFadeSpeed) * Time.deltaTime);

        if (hpGaugePulseTimer > 0f)
            hpGaugePulseTimer = Mathf.Max(0f, hpGaugePulseTimer - Time.deltaTime);

        if (forcedIdleTimer > 0f)
            forcedIdleTimer = Mathf.Max(0f, forcedIdleTimer - Time.deltaTime);

        bool isForcedIdle = forcedIdleTimer > 0f || isGhostDialogueActive;

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
            if (isGhostDialogueActive)
                AdvanceGhostDialogue();
            else
                TryStartGhostDialogue();
        }

        UpdateHpGaugeSideVfxPosition();
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

    void TryStartGhostDialogue(){
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

        StartGhostDialogue(ghost);
    }

    void StartGhostDialogue(Transform ghost){
        isGhostDialogueActive = true;
        ghostMessageIndex = 0;

        if (orbitCamera == null && cam != null)
            orbitCamera = cam.GetComponent<ThirdPersonOrbitCamera>();

        if (orbitCamera != null)
            orbitCamera.BeginGhostDialogue(ghost, transform);
    }

    void AdvanceGhostDialogue(){
        if (!isGhostDialogueActive)
            return;

        int messageCount = ghostMessages != null ? ghostMessages.Length : 0;
        if (messageCount <= 0){
            EndGhostDialogue();
            return;
        }

        ghostMessageIndex++;
        if (ghostMessageIndex >= messageCount)
            EndGhostDialogue();
    }

    void EndGhostDialogue(){
        isGhostDialogueActive = false;
        ghostMessageIndex = 0;

        if (orbitCamera != null)
            orbitCamera.EndGhostDialogue();
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
        
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 400, 30), debugText, debugLabelStyle);
        
        // 追加情報：速度、状態
        string stateText = $"State: {currentAnim} | Speed: {horizontalSpeed:F2} m/s | Surface type: ground";
        GUI.Label(new Rect(10, 40, 400, 30), stateText, debugLabelStyle);

        string testText = $"Sound: Background music";
        GUI.Label(new Rect(10, 70, 400, 30), testText, debugLabelStyle);

        DrawRadarTopRight();
        DrawHealthGaugeBottomRight();
        DrawInvisibilityStatusIconBottomLeft();
        DrawDamagePopups();

        if (isGhostDialogueActive){
            string message = GetCurrentGhostMessage();
            if (!string.IsNullOrEmpty(message)){
                float bubbleWidth = Mathf.Min(860f, Screen.width - 80f);
                float bubbleHeight = 120f;
                float bubbleX = (Screen.width - bubbleWidth) * 0.5f;
                float bubbleY = Screen.height - bubbleHeight - 28f;

                Rect bubbleRect = new Rect(bubbleX, bubbleY, bubbleWidth, bubbleHeight);
                GUI.color = new Color(0f, 0f, 0f, 0f);
                GUI.Box(bubbleRect, "");

                GUIStyle ghostMessageStyle = new GUIStyle(GUI.skin.label);
                ghostMessageStyle.alignment = TextAnchor.MiddleLeft;
                ghostMessageStyle.wordWrap = true;
                ghostMessageStyle.fontSize = 22;
                ghostMessageStyle.normal.textColor = Color.white;
                if (guiFont != null)
                    ghostMessageStyle.font = guiFont;

                GUI.color = Color.white;
                GUI.Label(new Rect(bubbleX + 18f, bubbleY + 14f, bubbleWidth - 36f, bubbleHeight - 44f), message, ghostMessageStyle);

                GUIStyle continueStyle = new GUIStyle(GUI.skin.label);
                continueStyle.alignment = TextAnchor.LowerRight;
                continueStyle.fontSize = 16;
                continueStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
                if (guiFont != null)
                    continueStyle.font = guiFont;

                GUI.Label(new Rect(bubbleX + 18f, bubbleY + bubbleHeight - 30f, bubbleWidth - 36f, 20f), "A : Next", continueStyle);
            }
        }

        GUI.color = Color.white;
    }

    public int GetCurrentHealth(){
        return currentHealth;
    }

    public bool IsInvisible(){
        return isInvisible;
    }

    string GetCurrentGhostMessage(){
        if (ghostMessages == null || ghostMessages.Length == 0)
            return string.Empty;

        int index = Mathf.Clamp(ghostMessageIndex, 0, ghostMessages.Length - 1);
        return ghostMessages[index];
    }

    public void ApplySkeletonHit(int damage, float forceIdleSeconds){
        int finalDamage = Mathf.Clamp(damage, 0, 9999);
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        forcedIdleTimer = Mathf.Max(forcedIdleTimer, Mathf.Max(0f, forceIdleSeconds));

        if (finalDamage > 0){
            TriggerHpGaugePulse();
            AddDamagePopup(finalDamage);
        }

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

    void DrawHealthGaugeBottomRight(){
        Rect gaugeRect = GetHealthGaugeRect();
        Rect fillRect = new Rect(
            gaugeRect.x + 2f,
            gaugeRect.y + 2f,
            (gaugeRect.width - 4f) * Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth)),
            gaugeRect.height - 4f);

        EnsureHudTextures(Mathf.RoundToInt(gaugeRect.width), Mathf.RoundToInt(gaugeRect.height));

        Color prev = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(gaugeRect, hpGaugeBackgroundTex);
        GUI.DrawTexture(fillRect, hpGaugeFillTex);
        GUI.DrawTexture(gaugeRect, hpGaugeFrameTex);

        float pulse = GetHpGaugePulseIntensity();
        if (pulse > 0.001f){
            GUI.color = new Color(1f, 1f, 1f, pulse);
            GUI.DrawTexture(fillRect, hpGaugeFillGlowTex);
            GUI.DrawTexture(gaugeRect, hpGaugeFrameGlowTex);
        }
        GUI.color = prev;
    }

    Rect GetHealthGaugeRect(){
        float gaugeWidth = 250f;
        float gaugeHeight = 24f;
        float margin = 20f;

        float x = (Screen.width - gaugeWidth) * 0.5f;
        float y = Screen.height - gaugeHeight - margin;
        return new Rect(x, y, gaugeWidth, gaugeHeight);
    }

    bool EnsureHudOverlayCanvas(){
        if (hudOverlayCanvas != null)
            return true;

        GameObject canvasObj = new GameObject("HUDOverlayCanvas_Runtime");
        hudOverlayCanvas = canvasObj.AddComponent<Canvas>();
        hudOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudOverlayCanvas.sortingOrder = 4000;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        createdHudOverlayCanvas = true;
        return true;
    }

    void EnsureHpGaugeSideVfx(){
        if (hpGaugeSideVfxPrefab == null)
            return;

        if (!EnsureHudOverlayCanvas())
            return;

        if (hpGaugeSideVfxInstance != null)
            return;

        hpGaugeSideVfxInstance = Instantiate(hpGaugeSideVfxPrefab, hudOverlayCanvas.transform, false);
        hpGaugeSideVfxRect = hpGaugeSideVfxInstance.GetComponent<RectTransform>();
        if (hpGaugeSideVfxRect != null)
            hpGaugeSideVfxRect.localScale = hpGaugeSideVfxScale;
    }

    void UpdateHpGaugeSideVfxPosition(){
        EnsureHpGaugeSideVfx();

        if (hpGaugeSideVfxRect == null)
            return;

        Rect gaugeRect = GetHealthGaugeRect();
        float targetGuiX = gaugeRect.xMax + hpGaugeSideVfxOffset.x;
        float targetGuiY = gaugeRect.y + hpGaugeSideVfxOffset.y;
        float targetScreenY = Screen.height - targetGuiY;

        hpGaugeSideVfxRect.anchorMin = new Vector2(0.5f, 0.5f);
        hpGaugeSideVfxRect.anchorMax = new Vector2(0.5f, 0.5f);
        hpGaugeSideVfxRect.pivot = new Vector2(0.5f, 0.5f);
        hpGaugeSideVfxRect.anchoredPosition = new Vector2(
            targetGuiX - Screen.width * 0.5f,
            targetScreenY - Screen.height * 0.5f);
        hpGaugeSideVfxRect.localScale = hpGaugeSideVfxScale;
    }

    void DestroyHpGaugeSideVfx(){
        if (hpGaugeSideVfxInstance != null)
            Destroy(hpGaugeSideVfxInstance);

        hpGaugeSideVfxInstance = null;
        hpGaugeSideVfxRect = null;
    }

    void TryAssignHudVfxPrefabsInEditor(){
        if (hpGaugeSideVfxPrefab != null)
            return;

#if UNITY_EDITOR
        hpGaugeSideVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HpGaugeSideVfxPrefabPath);
#endif
    }

    void DrawInvisibilityStatusIconBottomLeft(){
        float margin = 20f;
        float panelSize = 68f;
        float iconSize = 42f;
        float gaugeWidth = 250f;
        float gaugeHeight = 24f;

        EnsureHudTextures(250, 24);

        float gaugeX = (Screen.width - gaugeWidth) * 0.5f;
        float gaugeY = Screen.height - gaugeHeight - margin;
        float panelX = gaugeX - panelSize - 14f;
        float panelY = gaugeY + (gaugeHeight - panelSize) * 0.5f;

        Rect panelRect = new Rect(panelX, panelY, panelSize, panelSize);
        Rect iconRect = new Rect(
            panelRect.x + (panelRect.width - iconSize) * 0.5f,
            panelRect.y + 8f,
            iconSize,
            iconSize);

        GUI.color = Color.white;
        GUI.DrawTexture(panelRect, invisIconPanelTex);

        Color offColor = new Color(1f, 1f, 1f, 1f - invisibilityIconBlend);
        Color onColor = new Color(1f, 1f, 1f, invisibilityIconBlend);
        GUI.color = offColor;
        GUI.DrawTexture(iconRect, invisIconOffTex);
        GUI.color = onColor;
        GUI.DrawTexture(iconRect, invisIconOnTex);
    }

    void DrawRadarTopRight(){
        EnsureRadarIconsLoaded();

        float size = Mathf.Max(90f, radarSize);
        float margin = 20f;
        Rect radarRect = new Rect(Screen.width - size - margin, margin, size, size);

        GUI.color = new Color(0.04f, 0.07f, 0.10f, 0.86f);
        GUI.DrawTexture(radarRect, Texture2D.whiteTexture);

        GUI.color = new Color(1f, 1f, 1f, 0.08f);
        GUI.DrawTexture(new Rect(radarRect.x + radarRect.width * 0.5f - 1f, radarRect.y + 6f, 2f, radarRect.height - 12f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(radarRect.x + 6f, radarRect.y + radarRect.height * 0.5f - 1f, radarRect.width - 12f, 2f), Texture2D.whiteTexture);

        GUI.color = new Color(0.75f, 0.88f, 1f, 0.26f);
        GUI.DrawTexture(new Rect(radarRect.x, radarRect.y, radarRect.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(radarRect.x, radarRect.yMax - 2f, radarRect.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(radarRect.x, radarRect.y, 2f, radarRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(radarRect.xMax - 2f, radarRect.y, 2f, radarRect.height), Texture2D.whiteTexture);

        Vector2 playerRadarPos = new Vector2(radarRect.x + radarRect.width * 0.5f, radarRect.y + radarRect.height * 0.5f);

        RefreshRadarEnemyTargetsIfNeeded();

        Transform ghost = GetGhostTransform();
        if (ghost != null){
            Vector2 ghostRadarPos;
            if (TryGetRadarRelativePosition(ghost.position, transform.position, radarRect, out ghostRadarPos))
                DrawRadarMarker(ghostRadarPos, 20f, radarGhostIconTex, new Color(0.92f, 0.82f, 1f, 0.95f));
        }

        for (int i = radarEnemyTargets.Count - 1; i >= 0; i--){
            Transform enemy = radarEnemyTargets[i];
            if (enemy == null){
                radarEnemyTargets.RemoveAt(i);
                continue;
            }

            Vector2 enemyRadarPos;
            if (TryGetRadarRelativePosition(enemy.position, transform.position, radarRect, out enemyRadarPos))
                DrawRadarMarker(enemyRadarPos, 19f, radarEnemyIconTex, new Color(1f, 0.35f, 0.35f, 0.95f));
        }

        Texture2D playerRadarIcon = isInvisible ? radarPlayerIconTex : radarPlayerNormalIconTex;
        DrawRadarMarker(playerRadarPos, 22f, playerRadarIcon, new Color(0.25f, 0.95f, 0.70f, 1f));

        Vector3 fwd = transform.forward;
        Vector2 dir = new Vector2(fwd.x, fwd.z).normalized;
        float lineLen = 14f;
        Rect dirRect = new Rect(playerRadarPos.x, playerRadarPos.y, dir.x * lineLen, -dir.y * lineLen);
        DrawRadarLine(dirRect, new Color(0.85f, 1f, 0.92f, 0.95f));

    }

    bool TryGetRadarRelativePosition(Vector3 worldPos, Vector3 playerPos, Rect radarRect, out Vector2 radarPos){
        float minX = Mathf.Min(radarWorldMinXZ.x, radarWorldMaxXZ.x);
        float maxX = Mathf.Max(radarWorldMinXZ.x, radarWorldMaxXZ.x);
        float minZ = Mathf.Min(radarWorldMinXZ.y, radarWorldMaxXZ.y);
        float maxZ = Mathf.Max(radarWorldMinXZ.y, radarWorldMaxXZ.y);

        float detectionScale = Mathf.Clamp(radarDetectionRangeScale, 0.2f, 1f);
        float rangeX = Mathf.Abs(maxX - minX) * 0.5f * detectionScale;
        float rangeZ = Mathf.Abs(maxZ - minZ) * 0.5f * detectionScale;

        if (rangeX <= 0.0001f || rangeZ <= 0.0001f){
            radarPos = Vector2.zero;
            return false;
        }

        float offsetX = Mathf.Clamp((worldPos.x - playerPos.x) / rangeX, -1f, 1f);
        float offsetZ = Mathf.Clamp((worldPos.z - playerPos.z) / rangeZ, -1f, 1f);

        float nx = (offsetX + 1f) * 0.5f;
        float nz = (offsetZ + 1f) * 0.5f;
        radarPos = new Vector2(
            Mathf.Lerp(radarRect.x + 8f, radarRect.xMax - 8f, Mathf.Clamp01(nx)),
            Mathf.Lerp(radarRect.yMax - 8f, radarRect.y + 8f, Mathf.Clamp01(nz)));
        return true;
    }

    void DrawRadarDot(Vector2 position, float size, Color color){
        GUI.color = color;
        GUI.DrawTexture(new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size), Texture2D.whiteTexture);
    }

    void DrawRadarMarker(Vector2 position, float size, Texture2D iconTexture, Color fallbackColor){
        GUI.color = Color.white;

        if (iconTexture != null){
            GUI.DrawTexture(new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size), iconTexture);
            return;
        }

        DrawRadarDot(position, size * 0.55f, fallbackColor);
    }

    void EnsureRadarIconsLoaded(){
        if (radarEnemyIconTex == null)
            radarEnemyIconTex = Resources.Load<Texture2D>(RadarEnemyIconResourcePath);

        if (radarGhostIconTex == null)
            radarGhostIconTex = Resources.Load<Texture2D>(RadarGhostIconResourcePath);

        if (radarPlayerNormalIconTex == null)
            radarPlayerNormalIconTex = Resources.Load<Texture2D>(RadarPlayerNormalIconResourcePath);

        if (radarPlayerIconTex == null)
            radarPlayerIconTex = Resources.Load<Texture2D>(RadarPlayerIconResourcePath);
    }

    void RefreshRadarEnemyTargetsIfNeeded(){
        radarEnemyRefreshTimer -= Time.deltaTime;
        if (radarEnemyRefreshTimer > 0f)
            return;

        radarEnemyRefreshTimer = Mathf.Max(0.1f, radarEnemyRefreshSeconds);
        radarEnemyTargets.Clear();

        DungeonSkeletonEnemyAI[] enemies = FindObjectsOfType<DungeonSkeletonEnemyAI>();
        for (int i = 0; i < enemies.Length; i++){
            DungeonSkeletonEnemyAI enemy = enemies[i];
            if (enemy == null)
                continue;

            radarEnemyTargets.Add(enemy.transform);
        }
    }

    void DrawRadarLine(Rect vectorRect, Color color){
        Vector2 start = new Vector2(vectorRect.x, vectorRect.y);
        Vector2 end = new Vector2(vectorRect.x + vectorRect.width, vectorRect.y + vectorRect.height);
        Vector2 diff = end - start;
        float len = diff.magnitude;
        if (len <= 0.001f)
            return;

        Vector2 dir = diff / len;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        float thickness = 2f;
        float headLength = Mathf.Clamp(len * 0.38f, 6f, 12f);
        float headWidth = headLength * 0.9f;

        Vector2 shaftEnd = end - dir * (headLength * 0.55f);
        Vector2 leftWing = end - dir * headLength + perp * (headWidth * 0.5f);
        Vector2 rightWing = end - dir * headLength - perp * (headWidth * 0.5f);

        DrawGuiLine(start, shaftEnd, color, thickness);
        DrawGuiLine(end, leftWing, color, thickness);
        DrawGuiLine(end, rightWing, color, thickness);
    }

    void DrawGuiLine(Vector2 start, Vector2 end, Color color, float thickness){
        Vector2 diff = end - start;
        float len = diff.magnitude;
        if (len <= 0.001f)
            return;

        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        Matrix4x4 prevMatrix = GUI.matrix;
        Color prevColor = GUI.color;

        GUI.color = color;
        GUI.matrix = Matrix4x4.TRS(start, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        GUI.DrawTexture(new Rect(0f, -thickness * 0.5f, len, thickness), Texture2D.whiteTexture);

        GUI.matrix = prevMatrix;
        GUI.color = prevColor;
    }

    void EnsureHudTextures(int gaugeWidth, int gaugeHeight){
        if (hpGaugeBackgroundTex == null || hpGaugeTextureWidth != gaugeWidth || hpGaugeTextureHeight != gaugeHeight){
            hpGaugeBackgroundTex = CreateRoundedRectTexture(gaugeWidth, gaugeHeight, new Color(0.08f, 0.11f, 0.13f, 0.94f), new Color(0.03f, 0.05f, 0.06f, 0.94f), 10f, 0f, false);
            hpGaugeFillTex = CreateRoundedRectTexture(gaugeWidth, gaugeHeight, new Color(0.10f, 0.45f, 0.30f, 0.98f), new Color(0.02f, 0.25f, 0.18f, 0.98f), 8f, 0f, false);
            hpGaugeFrameTex = CreateRoundedRectTexture(gaugeWidth, gaugeHeight, new Color(0.75f, 0.90f, 1f, 0.20f), new Color(0.55f, 0.75f, 0.95f, 0.20f), 10f, 1.2f, true);
            hpGaugeFillGlowTex = CreateRoundedRectTexture(gaugeWidth, gaugeHeight, new Color(0.90f, 1f, 0.65f, 0.95f), new Color(0.62f, 0.95f, 0.35f, 0.95f), 8f, 0f, false);
            hpGaugeFrameGlowTex = CreateRoundedRectTexture(gaugeWidth, gaugeHeight, new Color(1f, 0.42f, 0.18f, 0.95f), new Color(0.86f, 0.18f, 0.12f, 0.95f), 10f, 2f, true);
            hpGaugeTextureWidth = gaugeWidth;
            hpGaugeTextureHeight = gaugeHeight;
        }

        if (invisIconPanelTex == null)
            invisIconPanelTex = CreateRoundedRectTexture(68, 68, new Color(0.05f, 0.07f, 0.10f, 0.86f), new Color(0.02f, 0.03f, 0.05f, 0.86f), 12f, 0f, false);

        if (invisIconOnTex == null)
            invisIconOnTex = CreateCircleTexture(42, new Color(0.25f, 0.95f, 1f, 0.96f), new Color(0.05f, 0.45f, 0.75f, 0.96f), new Color(0.85f, 1f, 1f, 0.95f), 2f, new Color(1f, 1f, 1f, 0.95f), 0.034f);

        if (invisIconOffTex == null)
            invisIconOffTex = CreateCircleTexture(42, new Color(0.55f, 0.55f, 0.55f, 0.92f), new Color(0.28f, 0.28f, 0.28f, 0.92f), new Color(0.92f, 0.92f, 0.92f, 0.60f), 2f, new Color(0.34f, 0.34f, 0.34f, 0.92f), 0.034f);
    }

    Texture2D CreateRoundedRectTexture(int width, int height, Color topColor, Color bottomColor, float radius, float borderThickness, bool drawBorder){
        Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float maxY = height - 1f;
        float innerRadius = Mathf.Max(0f, radius - borderThickness);

        for (int y = 0; y < height; y++){
            float v = maxY <= 0f ? 0f : y / maxY;
            Color gradColor = Color.Lerp(bottomColor, topColor, v);

            for (int x = 0; x < width; x++){
                bool inOuter = IsInsideRoundedRect(x, y, width, height, radius);
                if (!inOuter){
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                if (drawBorder){
                    bool inInner = IsInsideRoundedRectInset(x, y, width, height, innerRadius, borderThickness);
                    if (inInner){
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }
                }

                tex.SetPixel(x, y, gradColor);
            }
        }

        tex.Apply();
        return tex;
    }

    bool IsInsideRoundedRectInset(int px, int py, int width, int height, float radius, float inset){
        float minX = Mathf.Max(0f, inset);
        float minY = Mathf.Max(0f, inset);
        float maxX = (width - 1f) - Mathf.Max(0f, inset);
        float maxY = (height - 1f) - Mathf.Max(0f, inset);

        if (minX > maxX || minY > maxY)
            return false;

        float x = px;
        float y = py;
        if (x < minX || x > maxX || y < minY || y > maxY)
            return false;

        float effectiveWidth = (maxX - minX) + 1f;
        float effectiveHeight = (maxY - minY) + 1f;
        float r = Mathf.Min(radius, Mathf.Min(effectiveWidth, effectiveHeight) * 0.5f);

        if (r <= 0f)
            return true;

        if (x >= minX + r && x <= maxX - r)
            return true;
        if (y >= minY + r && y <= maxY - r)
            return true;

        float cx = x < minX + r ? minX + r : maxX - r;
        float cy = y < minY + r ? minY + r : maxY - r;
        float dx = x - cx;
        float dy = y - cy;
        return dx * dx + dy * dy <= r * r;
    }

    bool IsInsideRoundedRect(int px, int py, int width, int height, float radius){
        if (radius <= 0f)
            return true;

        float r = Mathf.Min(radius, Mathf.Min(width, height) * 0.5f);
        float x = px;
        float y = py;
        float maxX = width - 1f;
        float maxY = height - 1f;

        if (x >= r && x <= maxX - r)
            return true;
        if (y >= r && y <= maxY - r)
            return true;

        float cx = x < r ? r : maxX - r;
        float cy = y < r ? r : maxY - r;
        float dx = x - cx;
        float dy = y - cy;
        return dx * dx + dy * dy <= r * r;
    }

    Texture2D CreateCircleTexture(int size, Color topColor, Color bottomColor, Color rimColor, float rimThickness, Color stickFigureColor, float stickFigureLineThickness){
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float radius = center;
        float innerRadius = Mathf.Max(0f, radius - rimThickness);

        for (int y = 0; y < size; y++){
            float v = (size <= 1) ? 0f : (float)y / (size - 1);
            Color grad = Color.Lerp(bottomColor, topColor, v);

            for (int x = 0; x < size; x++){
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > radius){
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                Color finalColor = (dist >= innerRadius) ? rimColor : grad;

                if (DrawInvisibilityStickFigurePixel(x, y, size, center, stickFigureLineThickness))
                    finalColor = stickFigureColor;

                tex.SetPixel(x, y, finalColor);
            }
        }

        tex.Apply();
        return tex;
    }

    bool DrawInvisibilityStickFigurePixel(int x, int y, int size, float center, float bodyLineThickness){
        float nx = (x - center) / Mathf.Max(1f, size - 1f);
        float ny = (y - center) / Mathf.Max(1f, size - 1f);

        float headCenterX = 0f;
        float headCenterY = 0.18f;
        float headRadius = 0.11f;
        float bodyThickness = 0.034f;
        float limbThickness = Mathf.Max(0.01f, bodyLineThickness);

        bool isHead = IsPointInCircle(nx, ny, headCenterX, headCenterY, headRadius);
        if (isHead)
            return true;

        bool isBody = IsPointNearLine(nx, ny, 0f, 0.06f, 0f, -0.17f, bodyThickness);
        bool isLeftArm = IsPointNearLine(nx, ny, 0f, -0.01f, -0.14f, -0.09f, limbThickness);
        bool isRightArm = IsPointNearLine(nx, ny, 0f, -0.01f, 0.14f, -0.09f, limbThickness);
        bool isLeftLeg = IsPointNearLine(nx, ny, 0f, -0.17f, -0.13f, -0.32f, limbThickness);
        bool isRightLeg = IsPointNearLine(nx, ny, 0f, -0.17f, 0.13f, -0.32f, limbThickness);

        return isBody || isLeftArm || isRightArm || isLeftLeg || isRightLeg;
    }

    bool IsPointInCircle(float px, float py, float cx, float cy, float radius){
        float dx = px - cx;
        float dy = py - cy;
        return dx * dx + dy * dy <= radius * radius;
    }

    bool IsPointNearLine(float px, float py, float x1, float y1, float x2, float y2, float thickness){
        float vx = x2 - x1;
        float vy = y2 - y1;
        float wx = px - x1;
        float wy = py - y1;

        float lenSq = vx * vx + vy * vy;
        if (lenSq <= 0.000001f)
            return IsPointInCircle(px, py, x1, y1, thickness * 0.5f);

        float t = Mathf.Clamp01((wx * vx + wy * vy) / lenSq);
        float cx = x1 + vx * t;
        float cy = y1 + vy * t;
        float dx = px - cx;
        float dy = py - cy;
        return dx * dx + dy * dy <= thickness * thickness;
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

    void TriggerHpGaugePulse(){
        hpGaugePulseTimer = HpGaugePulseDuration;
    }

    float GetHpGaugePulseIntensity(){
        if (hpGaugePulseTimer <= 0f)
            return 0f;

        float t = 1f - Mathf.Clamp01(hpGaugePulseTimer / HpGaugePulseDuration);
        float fadeIn = Mathf.Clamp01(t / 0.28f);
        float fadeOut = 1f - Mathf.Clamp01((t - 0.28f) / 0.72f);
        float intensity = Mathf.Min(fadeIn, fadeOut);
        return intensity * 0.9f;
    }

    void OnDestroy(){
        DestroyHpGaugeSideVfx();
        if (createdHudOverlayCanvas && hudOverlayCanvas != null)
            Destroy(hudOverlayCanvas.gameObject);

        CleanupRuntimeDissolveInstances();

        if (hpGaugeBackgroundTex != null)
            Destroy(hpGaugeBackgroundTex);
        if (hpGaugeFillTex != null)
            Destroy(hpGaugeFillTex);
        if (hpGaugeFrameTex != null)
            Destroy(hpGaugeFrameTex);
        if (hpGaugeFillGlowTex != null)
            Destroy(hpGaugeFillGlowTex);
        if (hpGaugeFrameGlowTex != null)
            Destroy(hpGaugeFrameGlowTex);
        if (invisIconOnTex != null)
            Destroy(invisIconOnTex);
        if (invisIconOffTex != null)
            Destroy(invisIconOffTex);
        if (invisIconPanelTex != null)
            Destroy(invisIconPanelTex);
    }

    bool BuildDissolveMaterialsFromTemplate(){
        CleanupRuntimeDissolveInstances();
        dissolveMaterials.Clear();

        if (dissolveFallbackMaterial == null){
            if (logInvisibilityIssues)
                Debug.LogWarning("[CharController_Motor] dissolveFallbackMaterial is not assigned.");
            return false;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++){
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] originals = renderer.materials;
            if (originals == null || originals.Length == 0)
                continue;

            Material[] replacements = new Material[originals.Length];
            bool hasDissolveMaterial = false;

            for (int j = 0; j < originals.Length; j++){
                Material original = originals[j];
                if (original == null){
                    replacements[j] = null;
                    continue;
                }

                Material replacement = new Material(dissolveFallbackMaterial);
                CopyCommonMaterialProperties(original, replacement);
                replacements[j] = replacement;
                runtimeDissolveInstances.Add(replacement);

                if (replacement.HasProperty(DissolveAmountProperty) && !dissolveMaterials.Contains(replacement)){
                    dissolveMaterials.Add(replacement);
                    hasDissolveMaterial = true;
                }
            }

            if (!hasDissolveMaterial)
                continue;

            renderer.materials = replacements;
        }

        if (dissolveMaterials.Count == 0 && logInvisibilityIssues)
            Debug.LogWarning("[CharController_Motor] The assigned dissolveFallbackMaterial does not expose _DissolveAmount.");

        return dissolveMaterials.Count > 0;
    }

    void CopyCommonMaterialProperties(Material source, Material destination){
        if (source == null || destination == null)
            return;

        if (source.HasProperty(MainTexProperty) && destination.HasProperty(MainTexProperty))
            destination.SetTexture(MainTexProperty, source.GetTexture(MainTexProperty));
        else if (source.HasProperty(BaseMapProperty) && destination.HasProperty(BaseMapProperty))
            destination.SetTexture(BaseMapProperty, source.GetTexture(BaseMapProperty));

        if (source.HasProperty(ColorProperty) && destination.HasProperty(ColorProperty))
            destination.SetColor(ColorProperty, source.GetColor(ColorProperty));
        else if (source.HasProperty(BaseColorProperty) && destination.HasProperty(BaseColorProperty))
            destination.SetColor(BaseColorProperty, source.GetColor(BaseColorProperty));
    }

    void CleanupRuntimeDissolveInstances(){
        for (int i = 0; i < runtimeDissolveInstances.Count; i++){
            Material mat = runtimeDissolveInstances[i];
            if (mat == null)
                continue;

            Destroy(mat);
        }

        runtimeDissolveInstances.Clear();
    }

    void SetInvisibility(bool makeInvisible){
        if (dissolveMaterials.Count == 0 && !BuildDissolveMaterialsFromTemplate())
            return;

        isInvisible = makeInvisible;
        float target = isInvisible ? invisibleDissolveAmount : visibleDissolveAmount;

        if (dissolveRoutine != null)
            StopCoroutine(dissolveRoutine);

        dissolveRoutine = StartCoroutine(AnimateDissolve(target));
    }

    IEnumerator AnimateDissolve(float target){
        float duration = Mathf.Max(0f, dissolveTransitionSeconds);
        if (duration <= 0f){
            ApplyDissolveAmountInstant(target);
            dissolveRoutine = null;
            yield break;
        }

        float start = currentDissolveAmount;
        float elapsed = 0f;

        while (elapsed < duration){
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float value = Mathf.Lerp(start, target, t);
            ApplyDissolveAmountInstant(value);
            yield return null;
        }

        ApplyDissolveAmountInstant(target);
        dissolveRoutine = null;
    }

    void ApplyDissolveAmountInstant(float amount){
        currentDissolveAmount = Mathf.Clamp01(amount);

        for (int i = 0; i < dissolveMaterials.Count; i++){
            Material mat = dissolveMaterials[i];
            if (mat == null)
                continue;

            mat.SetFloat(DissolveAmountProperty, currentDissolveAmount);
        }
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