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

        ResolvePlayerReference();
        BeginIdle();
    }

    private void Update()
    {
        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);
        }

        ResolvePlayerReference();
        ResolveOverlapWithNearbySkeletons();

        if (IsPlayerInvisible())
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
