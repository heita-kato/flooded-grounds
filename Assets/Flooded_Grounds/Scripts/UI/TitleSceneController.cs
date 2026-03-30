using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneController : MonoBehaviour
{
    private const string KeyVisualResourcePath = "Materials/BATTLES_IN_FLOODED_GROUNDS_keyvisual";
    private const string NextSceneName = "Scene_A";
    private const string ButtonFontResourcePath = "Fonts/Square-Black";
    private const string TitleBgmResourcePath = "Music/title-bgm";
    private const string StartSeResourcePath = "Sounds/game_start";

    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField, Range(0f, 1f)] private float titleBgmVolume = 0.72f;
    [SerializeField, Range(0f, 1f)] private float startSeVolume = 1f;

    private Texture2D keyVisual;
    private Font buttonFont;
    private AudioClip titleBgmClip;
    private AudioClip startSeClip;
    private AudioSource bgmAudioSource;
    private AudioSource oneShotAudioSource;
    private AudioListener runtimeAudioListener;
    private GUIStyle buttonStyle;
    private bool isTransitioning;
    private float fadeAlpha;

    private void Awake()
    {
        keyVisual = Resources.Load<Texture2D>(KeyVisualResourcePath);
        buttonFont = Resources.Load<Font>(ButtonFontResourcePath);
        titleBgmClip = Resources.Load<AudioClip>(TitleBgmResourcePath);
        startSeClip = Resources.Load<AudioClip>(StartSeResourcePath);

        EnsureAudioListenerExists();
        AudioListener.pause = false;

        bgmAudioSource = gameObject.AddComponent<AudioSource>();
        bgmAudioSource.playOnAwake = false;
        bgmAudioSource.loop = true;
        bgmAudioSource.spatialBlend = 0f;
        bgmAudioSource.ignoreListenerPause = true;

        oneShotAudioSource = gameObject.AddComponent<AudioSource>();
        oneShotAudioSource.playOnAwake = false;
        oneShotAudioSource.loop = false;
        oneShotAudioSource.spatialBlend = 0f;
        oneShotAudioSource.ignoreListenerPause = true;
    }

    private void Start()
    {
        if (titleBgmClip == null)
        {
            Debug.LogWarning("[TitleSceneController] title-bgm clip not found at Resources/" + TitleBgmResourcePath);
            return;
        }

        bgmAudioSource.clip = titleBgmClip;
        bgmAudioSource.volume = Mathf.Clamp01(titleBgmVolume);
        bgmAudioSource.Play();
    }

    private void SetupButtonStyle()
    {
        // Use a text-only button style so no frame/background is drawn.
        buttonStyle = new GUIStyle(GUIStyle.none)
        {
            fontSize = 24,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };

        if (buttonFont != null)
        {
            buttonStyle.font = buttonFont;
        }

        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;
        buttonStyle.focused.textColor = Color.white;
        buttonStyle.onNormal.textColor = Color.white;
        buttonStyle.onHover.textColor = Color.white;
        buttonStyle.onActive.textColor = Color.white;
        buttonStyle.onFocused.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (buttonStyle == null)
        {
            SetupButtonStyle();
        }

        DrawBackground();

        const float buttonWidth = 260f;
        const float buttonHeight = 72f;
        float x = (Screen.width - buttonWidth) * 0.5f;
        float y = Screen.height - buttonHeight - 72f;

        if (!isTransitioning && GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Start", buttonStyle))
        {
            StartCoroutine(FadeAndLoadNextScene());
        }

        DrawFadeOverlay();
    }

    private IEnumerator FadeAndLoadNextScene()
    {
        isTransitioning = true;
        fadeAlpha = 0f;

        if (startSeClip != null)
            oneShotAudioSource.PlayOneShot(startSeClip, Mathf.Clamp01(startSeVolume));
        else
            Debug.LogWarning("[TitleSceneController] game_start clip not found at Resources/" + StartSeResourcePath);

        float elapsed = 0f;
        float startBgmVolume = bgmAudioSource != null ? bgmAudioSource.volume : 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeAlpha = Mathf.Clamp01(elapsed / fadeDuration);

            if (bgmAudioSource != null)
                bgmAudioSource.volume = Mathf.Lerp(startBgmVolume, 0f, fadeAlpha);

            yield return null;
        }

        fadeAlpha = 1f;
        if (bgmAudioSource != null)
            bgmAudioSource.Stop();

        SceneManager.LoadScene(NextSceneName);
    }

    private void DrawFadeOverlay()
    {
        if (fadeAlpha <= 0f)
        {
            return;
        }

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = previousColor;
    }

    private void DrawBackground()
    {
        if (keyVisual != null)
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), keyVisual, ScaleMode.ScaleAndCrop);
            return;
        }

        GUI.backgroundColor = Color.black;
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
    }

    private void EnsureAudioListenerExists()
    {
        AudioListener existing = FindObjectOfType<AudioListener>();
        if (existing != null)
            return;

        runtimeAudioListener = gameObject.AddComponent<AudioListener>();
    }
}
