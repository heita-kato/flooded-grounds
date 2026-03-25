using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BgmCrossfadeController : MonoBehaviour
{
    [Header("BGM")]
    [SerializeField] private string mainBgmResourcePath = "Music/main-bgm";
    [SerializeField] private string battleBgmResourcePath = "Music/battle-bgm";
    [SerializeField] private float masterVolume = 0.65f;
    [SerializeField] private float crossfadeSeconds = 1.4f;

    [Header("Battle Detection")]
    [SerializeField] private float battleEnterDistance = 14f;
    [SerializeField] private float battleExitDistance = 20f;
    [SerializeField] private float enemyRefreshSeconds = 0.8f;
    [SerializeField] private bool logWarnings = true;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private AudioClip mainClip;
    private AudioClip battleClip;

    private Transform player;
    private readonly List<Transform> enemyTargets = new List<Transform>();
    private float enemyRefreshTimer;
    private bool isBattleMode;
    private float fadeElapsed;
    private bool isFading;
    private AudioSource fadeOutSource;
    private AudioSource fadeInSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<BgmCrossfadeController>() != null)
            return;

        GameObject go = new GameObject("BgmCrossfadeController_Runtime");
        go.AddComponent<BgmCrossfadeController>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();
        ConfigureSource(sourceA);
        ConfigureSource(sourceB);
        activeSource = sourceA;

        LoadClips();
        TryFindPlayer();

        if (mainClip != null)
            StartImmediate(mainClip);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (mainClip == null || battleClip == null)
            return;

        if (player == null)
            TryFindPlayer();

        enemyRefreshTimer -= Time.deltaTime;
        if (enemyRefreshTimer <= 0f)
        {
            enemyRefreshTimer = Mathf.Max(0.1f, enemyRefreshSeconds);
            RefreshEnemyTargets();
        }

        bool hasNearbyEnemy = HasNearbyEnemy();

        if (!isBattleMode && hasNearbyEnemy)
        {
            isBattleMode = true;
            StartCrossfadeTo(battleClip);
        }
        else if (isBattleMode && !hasNearbyEnemy)
        {
            isBattleMode = false;
            StartCrossfadeTo(mainClip);
        }

        if (isFading)
            UpdateCrossfade();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryFindPlayer();
        RefreshEnemyTargets();

        if (activeSource == null || activeSource.clip == null)
        {
            StartImmediate(mainClip);
            isBattleMode = false;
            return;
        }

        if (activeSource.clip == battleClip)
            isBattleMode = true;
        else if (activeSource.clip == mainClip)
            isBattleMode = false;
    }

    private void ConfigureSource(AudioSource src)
    {
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;
        src.volume = 0f;
    }

    private void LoadClips()
    {
        mainClip = Resources.Load<AudioClip>(mainBgmResourcePath);
        battleClip = Resources.Load<AudioClip>(battleBgmResourcePath);

        if (logWarnings && mainClip == null)
            Debug.LogWarning("[BgmCrossfadeController] Missing clip in Resources: " + mainBgmResourcePath);

        if (logWarnings && battleClip == null)
            Debug.LogWarning("[BgmCrossfadeController] Missing clip in Resources: " + battleBgmResourcePath);
    }

    private void TryFindPlayer()
    {
        CharController_Motor motor = FindObjectOfType<CharController_Motor>();
        if (motor != null)
        {
            player = motor.transform;
            return;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    private void RefreshEnemyTargets()
    {
        enemyTargets.Clear();

        DungeonSkeletonEnemyAI[] enemies = FindObjectsOfType<DungeonSkeletonEnemyAI>();
        for (int i = 0; i < enemies.Length; i++)
        {
            DungeonSkeletonEnemyAI enemy = enemies[i];
            if (enemy != null)
                enemyTargets.Add(enemy.transform);
        }
    }

    private bool HasNearbyEnemy()
    {
        if (player == null || enemyTargets.Count == 0)
            return false;

        float threshold = isBattleMode ? battleExitDistance : battleEnterDistance;
        float thresholdSq = Mathf.Max(0.01f, threshold) * Mathf.Max(0.01f, threshold);

        Vector3 playerPos = player.position;
        for (int i = enemyTargets.Count - 1; i >= 0; i--)
        {
            Transform enemy = enemyTargets[i];
            if (enemy == null)
            {
                enemyTargets.RemoveAt(i);
                continue;
            }

            if ((enemy.position - playerPos).sqrMagnitude <= thresholdSq)
                return true;
        }

        return false;
    }

    private void StartImmediate(AudioClip clip)
    {
        if (clip == null || activeSource == null)
            return;

        activeSource.Stop();
        activeSource.clip = clip;
        activeSource.volume = Mathf.Clamp01(masterVolume);
        activeSource.Play();

        AudioSource idle = activeSource == sourceA ? sourceB : sourceA;
        idle.Stop();
        idle.clip = null;
        idle.volume = 0f;

        isFading = false;
    }

    private void StartCrossfadeTo(AudioClip nextClip)
    {
        if (nextClip == null)
            return;

        if (activeSource != null && activeSource.clip == nextClip && activeSource.isPlaying)
            return;

        fadeOutSource = activeSource;
        fadeInSource = activeSource == sourceA ? sourceB : sourceA;

        fadeInSource.Stop();
        fadeInSource.clip = nextClip;
        fadeInSource.volume = 0f;
        fadeInSource.Play();

        fadeElapsed = 0f;
        isFading = true;
    }

    private void UpdateCrossfade()
    {
        if (fadeInSource == null || fadeOutSource == null)
        {
            isFading = false;
            return;
        }

        float duration = Mathf.Max(0.01f, crossfadeSeconds);
        fadeElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(fadeElapsed / duration);

        float target = Mathf.Clamp01(masterVolume);
        fadeInSource.volume = Mathf.Lerp(0f, target, t);
        fadeOutSource.volume = Mathf.Lerp(target, 0f, t);

        if (t < 1f)
            return;

        fadeOutSource.Stop();
        fadeOutSource.clip = null;
        fadeOutSource.volume = 0f;

        activeSource = fadeInSource;
        fadeInSource = null;
        fadeOutSource = null;
        isFading = false;
    }
}
