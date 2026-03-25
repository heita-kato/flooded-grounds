using UnityEngine;

public class DungeonSkeletonEnemyAI : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Wander,
        Chase,
        Attack,
        Cooldown
    }

    [Header("References")]
    public Transform player;
    public Animator animator;
    public GameObject hitEffectPrefab;

    [Header("Animation States")]
    public string idleStateName = "DS_onehand_idle_A";
    public string walkStateName = "DS_onehand_walk";
    public string attackStateName = "DS_onehand_attack_A";

    [Header("Ranges")]
    public float detectRange = 12f;
    public float chaseStopRange = 1.6f;

    [Header("Movement")]
    public float wanderMoveSpeed = 1.2f;
    public float chaseMoveSpeed = 2.2f;
    public float turnSpeed = 8f;
    public float wanderRadiusMin = 1.5f;
    public float wanderRadiusMax = 5.5f;
    public float idleDurationMin = 0.8f;
    public float idleDurationMax = 2.2f;

    [Header("Avoidance")]
    public float separationRadius = 1.2f;
    public float separationWeight = 1.35f;
    public float separationMoveSpeed = 1.6f;

    [Header("Attack")]
    public float attackCooldown = 1.25f;
    public float attackHitDelay = 0.35f;
    public float attackAnimationDuration = 0.8f;
    public float playerForcedIdleSeconds = 0.2f;
    public float hitEffectYOffset = 1.0f;

    [Header("Debug")]
    public bool logAnimationIssues = true;

    [Header("Voice")]
    public float voiceIntervalMin = 4.0f;
    public float voiceIntervalMax = 9.0f;
    public float voiceHearDistance = 22.0f;
    [Range(0f, 1f)] public float voiceMinVolume = 0.05f;
    [Range(0f, 1f)] public float voiceMaxVolume = 0.85f;
    [Range(0f, 1f)] public float voiceSpatialBlend = 1f;
    public float voiceMinDistance = 2f;
    public float voiceMaxDistance = 24f;

    [Header("Attack SFX")]
    public float attackSfxSpatialBlend = 1f;
    public float attackSfxMinDistance = 1.5f;
    public float attackSfxMaxDistance = 20f;
    [Range(0f, 1f)] public float attackSfxVolume = 0.95f;

    [Header("Walk SFX")]
    public float walkSfxHearDistance = 18f;
    [Range(0f, 1f)] public float walkSfxMaxVolume = 0.6f;
    public float walkSfxMinDistance = 1.2f;
    public float walkSfxMaxDistance = 18f;
    [Range(0f, 1f)] public float walkSfxSpatialBlend = 1f;

    [Header("Lost Target Mark")]
    public float lostTargetMarkSeconds = 1.35f;
    public float lostTargetMarkHeight = 2.2f;
    public float lostTargetMarkFloatPixels = 22f;
    public int lostTargetMarkFontSize = 38;
    public Font lostTargetMarkFont;

    private EnemyState currentState = EnemyState.Idle;
    private Vector3 spawnOrigin;
    private Vector3 wanderTarget;
    private float idleTimer;
    private float attackCooldownTimer;
    private bool attackHitApplied;
    private float attackTimer;

    private int idleHash;
    private int walkHash;
    private int attackHash;
    private int idleShortHash;
    private int walkShortHash;
    private int attackShortHash;
    private bool loggedMissingIdle;
    private bool loggedMissingWalk;
    private bool loggedMissingAttack;
    private bool wasPlayerInvisibleLastFrame;
    private float lostTargetMarkTimer;
    private Camera cachedRenderCamera;
    private AudioSource voiceAudioSource;
    private AudioSource attackSfxAudioSource;
    private AudioSource walkLoopAudioSource;
    private AudioClip assignedVoiceClip;
    private AudioClip[] attackClips;
    private AudioClip damageClip;
    private AudioClip walkLoopClip;
    private float voiceTimer;
    private static int skeletonVoiceAssignCounter;

    private CharController_Motor playerMotor;

    private void Awake()
    {
        spawnOrigin = transform.position;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        idleShortHash = Animator.StringToHash(idleStateName);
        walkShortHash = Animator.StringToHash(walkStateName);
        attackShortHash = Animator.StringToHash(attackStateName);

        // Prefer full-path hashes for Animator.HasState/CrossFade stability.
        idleHash = Animator.StringToHash("Base Layer." + idleStateName);
        walkHash = Animator.StringToHash("Base Layer." + walkStateName);
        attackHash = Animator.StringToHash("Base Layer." + attackStateName);

        if (animator != null)
        {
            animator.applyRootMotion = false;
            PlayState(idleHash, 0.05f);
        }

        InitializeCombatAudio();
        InitializeWalkLoopAudio();
        InitializeVoiceAudio();
        ResolvePlayerReference();
        BeginIdle();
    }

    private void Update()
    {
        ResolvePlayerReference();
        bool playerInvisible = IsPlayerInvisible();

        if (lostTargetMarkTimer > 0f)
        {
            lostTargetMarkTimer = Mathf.Max(0f, lostTargetMarkTimer - Time.deltaTime);
        }

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);
        }

        UpdateVoicePlayback();

        ResolveOverlapWithNearbySkeletons();

        if (playerInvisible && !wasPlayerInvisibleLastFrame && (currentState == EnemyState.Chase || currentState == EnemyState.Attack || currentState == EnemyState.Cooldown))
        {
            lostTargetMarkTimer = Mathf.Max(0.05f, lostTargetMarkSeconds);
        }

        wasPlayerInvisibleLastFrame = playerInvisible;

        if (playerInvisible)
        {
            if (currentState == EnemyState.Attack || currentState == EnemyState.Chase || currentState == EnemyState.Cooldown)
            {
                BeginIdle();
            }

            UpdateWanderBehavior();
            return;
        }

        if (currentState == EnemyState.Attack)
        {
            UpdateAttack();
            return;
        }

        float playerDistance = GetPlayerDistance();
        bool playerInRange = player != null && playerDistance <= detectRange;

        if (playerInRange)
        {
            UpdateCombatBehavior(playerDistance);
            return;
        }

        UpdateWanderBehavior();
    }

    private void LateUpdate()
    {
        UpdateWalkLoopAudio();
    }

    private void OnGUI()
    {
        if (lostTargetMarkTimer <= 0f)
        {
            return;
        }

        Camera cam = ResolveRenderCamera();
        if (cam == null)
        {
            return;
        }

        Vector3 worldPos = transform.position + Vector3.up * lostTargetMarkHeight;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0f)
        {
            return;
        }

        float duration = Mathf.Max(0.05f, lostTargetMarkSeconds);
        float progress = 1f - Mathf.Clamp01(lostTargetMarkTimer / duration);
        float yFloat = Mathf.Lerp(0f, lostTargetMarkFloatPixels, progress);
        float alpha = progress >= 0.7f ? Mathf.Lerp(1f, 0f, (progress - 0.7f) / 0.3f) : 1f;

        float size = Mathf.Max(16f, lostTargetMarkFontSize + 10f);
        float drawX = screenPos.x - size * 0.5f;
        float drawY = (Screen.height - screenPos.y) - size * 0.5f - yFloat;
        Rect rect = new Rect(drawX, drawY, size, size);

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = Mathf.Max(12, lostTargetMarkFontSize);
        style.fontStyle = FontStyle.Bold;
        if (lostTargetMarkFont != null)
        {
            style.font = lostTargetMarkFont;
        }

        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), "?", style);
        GUI.color = Color.black;
        GUI.Label(rect, "?", style);
        GUI.color = previous;
    }

    private Camera ResolveRenderCamera()
    {
        if (cachedRenderCamera != null)
        {
            return cachedRenderCamera;
        }

        if (Camera.main != null)
        {
            cachedRenderCamera = Camera.main;
            return cachedRenderCamera;
        }

        if (playerMotor != null && playerMotor.cam != null)
        {
            Camera playerCam = playerMotor.cam.GetComponent<Camera>();
            if (playerCam != null)
            {
                cachedRenderCamera = playerCam;
                return cachedRenderCamera;
            }
        }

        Camera anyCam = FindObjectOfType<Camera>();
        if (anyCam != null)
        {
            cachedRenderCamera = anyCam;
        }

        return cachedRenderCamera;
    }

    private void ResolvePlayerReference()
    {
        if (player != null)
        {
            if (playerMotor == null)
            {
                playerMotor = player.GetComponent<CharController_Motor>();
            }
            return;
        }

        GameObject playerObj = GameObject.Find("FpsController");
        if (playerObj == null)
        {
            return;
        }

        player = playerObj.transform;
        playerMotor = playerObj.GetComponent<CharController_Motor>();
    }

    private void InitializeVoiceAudio()
    {
        if (voiceAudioSource == null)
        {
            voiceAudioSource = gameObject.AddComponent<AudioSource>();
        }

        voiceAudioSource.playOnAwake = false;
        voiceAudioSource.loop = false;
        voiceAudioSource.spatialBlend = Mathf.Clamp01(voiceSpatialBlend);
        voiceAudioSource.minDistance = Mathf.Max(0.1f, voiceMinDistance);
        voiceAudioSource.maxDistance = Mathf.Max(voiceAudioSource.minDistance + 0.1f, voiceMaxDistance);
        voiceAudioSource.rolloffMode = AudioRolloffMode.Linear;

        AudioClip[] voiceClips = new AudioClip[]
        {
            Resources.Load<AudioClip>("Sounds/skelton_voice1"),
            Resources.Load<AudioClip>("Sounds/skelton_voice2"),
            Resources.Load<AudioClip>("Sounds/skelton_voice3")
        };

        int validCount = 0;
        for (int i = 0; i < voiceClips.Length; i++)
        {
            if (voiceClips[i] != null)
                validCount++;
        }

        if (validCount <= 0)
        {
            Debug.LogWarning("[DungeonSkeletonEnemyAI] skelton_voice クリップが見つかりません");
            return;
        }

        AudioClip[] validClips = new AudioClip[validCount];
        int insert = 0;
        for (int i = 0; i < voiceClips.Length; i++)
        {
            if (voiceClips[i] == null)
                continue;

            validClips[insert] = voiceClips[i];
            insert++;
        }

        int assignIndex = skeletonVoiceAssignCounter % validClips.Length;
        skeletonVoiceAssignCounter++;
        assignedVoiceClip = validClips[assignIndex];

        float firstMax = Mathf.Max(0.25f, voiceIntervalMax);
        voiceTimer = Random.Range(0.15f, firstMax);
    }

    private void InitializeCombatAudio()
    {
        if (attackSfxAudioSource == null)
            attackSfxAudioSource = gameObject.AddComponent<AudioSource>();

        attackSfxAudioSource.playOnAwake = false;
        attackSfxAudioSource.loop = false;
        attackSfxAudioSource.spatialBlend = Mathf.Clamp01(attackSfxSpatialBlend);
        attackSfxAudioSource.minDistance = Mathf.Max(0.1f, attackSfxMinDistance);
        attackSfxAudioSource.maxDistance = Mathf.Max(attackSfxAudioSource.minDistance + 0.1f, attackSfxMaxDistance);
        attackSfxAudioSource.rolloffMode = AudioRolloffMode.Linear;

        attackClips = new AudioClip[]
        {
            Resources.Load<AudioClip>("Sounds/skelton_attack1"),
            Resources.Load<AudioClip>("Sounds/skelton_attack2"),
            Resources.Load<AudioClip>("Sounds/skelton_attack3"),
            Resources.Load<AudioClip>("Sounds/skelton_attack4")
        };

        damageClip = Resources.Load<AudioClip>("Sounds/damage");
    }

    private void InitializeWalkLoopAudio()
    {
        if (walkLoopAudioSource == null)
            walkLoopAudioSource = gameObject.AddComponent<AudioSource>();

        walkLoopAudioSource.playOnAwake = false;
        walkLoopAudioSource.loop = true;
        walkLoopAudioSource.spatialBlend = Mathf.Clamp01(walkSfxSpatialBlend);
        walkLoopAudioSource.minDistance = Mathf.Max(0.1f, walkSfxMinDistance);
        walkLoopAudioSource.maxDistance = Mathf.Max(walkLoopAudioSource.minDistance + 0.1f, walkSfxMaxDistance);
        walkLoopAudioSource.rolloffMode = AudioRolloffMode.Linear;
        walkLoopAudioSource.volume = 0f;

        walkLoopClip = Resources.Load<AudioClip>("Sounds/skelton_walk");
        walkLoopAudioSource.clip = walkLoopClip;
    }

    private void UpdateWalkLoopAudio()
    {
        if (walkLoopAudioSource == null || walkLoopClip == null)
            return;

        bool shouldLoop = currentState == EnemyState.Wander || currentState == EnemyState.Chase;
        if (!shouldLoop)
        {
            if (walkLoopAudioSource.isPlaying)
                walkLoopAudioSource.Stop();

            walkLoopAudioSource.volume = 0f;
            return;
        }

        float hearDistance = Mathf.Max(0.1f, walkSfxHearDistance);
        float distanceFactor = 0f;
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            distanceFactor = Mathf.Clamp01(1f - (dist / hearDistance));
        }

        float targetVolume = Mathf.Clamp01(walkSfxMaxVolume) * distanceFactor;
        walkLoopAudioSource.volume = targetVolume;

        if (targetVolume <= 0.001f)
        {
            if (walkLoopAudioSource.isPlaying)
                walkLoopAudioSource.Stop();
            return;
        }

        if (!walkLoopAudioSource.isPlaying)
            walkLoopAudioSource.Play();
    }

    private void PlayAttackHitSounds()
    {
        if (attackSfxAudioSource == null)
            return;

        attackSfxAudioSource.volume = Mathf.Clamp01(attackSfxVolume);

        AudioClip attackClip = null;
        if (attackClips != null && attackClips.Length > 0)
        {
            int validCount = 0;
            for (int i = 0; i < attackClips.Length; i++)
            {
                if (attackClips[i] != null)
                    validCount++;
            }

            if (validCount > 0)
            {
                int pick = Random.Range(0, validCount);
                int cursor = 0;
                for (int i = 0; i < attackClips.Length; i++)
                {
                    if (attackClips[i] == null)
                        continue;

                    if (cursor == pick)
                    {
                        attackClip = attackClips[i];
                        break;
                    }
                    cursor++;
                }
            }
        }

        if (attackClip != null)
            attackSfxAudioSource.PlayOneShot(attackClip, 1f);

        if (damageClip != null)
            attackSfxAudioSource.PlayOneShot(damageClip, 1f);
    }

    private void UpdateVoicePlayback()
    {
        if (voiceAudioSource == null || assignedVoiceClip == null)
            return;

        float intervalMin = Mathf.Max(0.1f, voiceIntervalMin);
        float intervalMax = Mathf.Max(intervalMin, voiceIntervalMax);
        float hearDistance = Mathf.Max(0.1f, voiceHearDistance);

        float distanceFactor = 0f;
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            distanceFactor = Mathf.Clamp01(1f - (dist / hearDistance));
        }

        float minVolume = Mathf.Clamp01(voiceMinVolume);
        float maxVolume = Mathf.Clamp(minVolume, 1f, voiceMaxVolume);
        voiceAudioSource.volume = Mathf.Lerp(minVolume, maxVolume, distanceFactor);

        voiceTimer -= Time.deltaTime;
        if (voiceTimer > 0f)
            return;

        voiceTimer = Random.Range(intervalMin, intervalMax);
        if (distanceFactor <= 0.01f)
            return;

        if (!voiceAudioSource.isPlaying)
            voiceAudioSource.PlayOneShot(assignedVoiceClip, 1f);
    }

    private float GetPlayerDistance()
    {
        if (player == null)
        {
            return float.MaxValue;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        return toPlayer.magnitude;
    }

    private void UpdateCombatBehavior(float playerDistance)
    {
        if (playerDistance > chaseStopRange)
        {
            currentState = attackCooldownTimer > 0f ? EnemyState.Cooldown : EnemyState.Chase;
            MoveTowards(player.position, chaseMoveSpeed);
            PlayState(walkHash, 0.02f);
            return;
        }

        FaceTowards(player.position);

        if (attackCooldownTimer > 0f)
        {
            currentState = EnemyState.Cooldown;
            PlayState(idleHash, 0.08f);
            return;
        }

        StartAttack();
    }

    private void StartAttack()
    {
        currentState = EnemyState.Attack;
        attackTimer = 0f;
        attackHitApplied = false;
        PlayState(attackHash, 0.05f);
    }

    private void UpdateAttack()
    {
        attackTimer += Time.deltaTime;

        if (!attackHitApplied && attackTimer >= attackHitDelay)
        {
            TryHitPlayer();
            attackHitApplied = true;
        }

        if (attackTimer >= attackAnimationDuration)
        {
            attackCooldownTimer = attackCooldown;
            currentState = EnemyState.Cooldown;
            PlayState(idleHash, 0.08f);
        }
    }

    private void TryHitPlayer()
    {
        if (playerMotor == null || player == null)
        {
            return;
        }

        if (IsPlayerInvisible())
        {
            return;
        }

        float distance = GetPlayerDistance();
        if (distance > chaseStopRange + 0.35f)
        {
            return;
        }

        int damage = Random.Range(3, 8);
        playerMotor.ApplySkeletonHit(damage, playerForcedIdleSeconds);
        PlayAttackHitSounds();

        // Instantiate hit effect at player position
        if (hitEffectPrefab != null)
        {
            Vector3 effectPosition = player.position + Vector3.up * hitEffectYOffset;
            Instantiate(hitEffectPrefab, effectPosition, Quaternion.identity);
        }
    }

    private bool IsPlayerInvisible()
    {
        return playerMotor != null && playerMotor.IsInvisible();
    }

    private void UpdateWanderBehavior()
    {
        if (currentState == EnemyState.Wander)
        {
            float distance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(wanderTarget.x, 0f, wanderTarget.z));
            if (distance > 0.2f)
            {
                MoveTowards(wanderTarget, wanderMoveSpeed);
                PlayState(walkHash, 0.02f);
                return;
            }

            BeginIdle();
            return;
        }

        idleTimer -= Time.deltaTime;
        PlayState(idleHash, 0.12f);

        if (idleTimer <= 0f)
        {
            BeginRandomWander();
        }
    }

    private void BeginIdle()
    {
        currentState = EnemyState.Idle;
        idleTimer = Random.Range(idleDurationMin, idleDurationMax);
        PlayState(idleHash, 0.1f);
    }

    private void BeginRandomWander()
    {
        currentState = EnemyState.Wander;

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(wanderRadiusMin, wanderRadiusMax);
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        wanderTarget = spawnOrigin + offset;
    }

    private void MoveTowards(Vector3 targetPosition, float speed)
    {
        Vector3 moveTarget = targetPosition;
        moveTarget.y = transform.position.y;

        Vector3 toTarget = moveTarget - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 moveDir = toTarget.normalized;
        Vector3 separationDir = GetSeparationDirection();
        if (separationDir.sqrMagnitude > 0.0001f)
        {
            moveDir = Vector3.ClampMagnitude(moveDir + separationDir * separationWeight, 1f);
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                moveDir.Normalize();
            }
        }

        transform.position += moveDir * speed * Time.deltaTime;

        Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private void ResolveOverlapWithNearbySkeletons()
    {
        Vector3 separationDir = GetSeparationDirection();
        if (separationDir.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.position += separationDir * separationMoveSpeed * Time.deltaTime;
    }

    private Vector3 GetSeparationDirection()
    {
        if (separationRadius <= 0f)
        {
            return Vector3.zero;
        }

        Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius);
        Vector3 accum = Vector3.zero;

        for (int i = 0; i < nearby.Length; i++)
        {
            Collider c = nearby[i];
            if (c == null)
            {
                continue;
            }

            DungeonSkeletonEnemyAI other = c.GetComponentInParent<DungeonSkeletonEnemyAI>();
            if (other == null || other == this)
            {
                continue;
            }

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist <= 0.0001f || dist > separationRadius)
            {
                continue;
            }

            float weight = (separationRadius - dist) / separationRadius;
            accum += away.normalized * weight;
        }

        if (accum.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        return accum.normalized;
    }

    private void FaceTowards(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private void PlayState(int stateHash, float blendTime)
    {
        if (animator == null)
        {
            return;
        }

        int shortHash = GetShortHash(stateHash);
        int resolvedHash = stateHash;

        if (!animator.HasState(0, resolvedHash))
        {
            resolvedHash = shortHash;
            if (!animator.HasState(0, resolvedHash))
            {
                LogMissingStateOnce(stateHash);
                return;
            }
        }

        AnimatorStateInfo currentInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (currentInfo.shortNameHash == shortHash || currentInfo.fullPathHash == resolvedHash)
        {
            return;
        }

        animator.CrossFade(resolvedHash, blendTime, 0);
    }

    private int GetShortHash(int fullHash)
    {
        if (fullHash == idleHash)
        {
            return idleShortHash;
        }

        if (fullHash == walkHash)
        {
            return walkShortHash;
        }

        return attackShortHash;
    }

    private void LogMissingStateOnce(int stateHash)
    {
        if (!logAnimationIssues)
        {
            return;
        }

        bool alreadyLogged;
        string stateLabel;

        if (stateHash == idleHash)
        {
            alreadyLogged = loggedMissingIdle;
            stateLabel = idleStateName;
            loggedMissingIdle = true;
        }
        else if (stateHash == walkHash)
        {
            alreadyLogged = loggedMissingWalk;
            stateLabel = walkStateName;
            loggedMissingWalk = true;
        }
        else
        {
            alreadyLogged = loggedMissingAttack;
            stateLabel = attackStateName;
            loggedMissingAttack = true;
        }

        if (alreadyLogged)
        {
            return;
        }

        RuntimeAnimatorController rac = animator.runtimeAnimatorController;
        string controllerName = rac != null ? rac.name : "(null)";
        Debug.LogWarning("[DungeonSkeletonEnemyAI] Animator state not found: " + stateLabel + " on controller " + controllerName + " for object " + gameObject.name);
    }
}
