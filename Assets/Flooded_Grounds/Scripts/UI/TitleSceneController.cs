using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneController : MonoBehaviour
{
    private const string KeyVisualResourcePath = "Materials/BATTLES_IN_FLOODED_GROUNDS_keyvisual";
    private const string NextSceneName = "Scene_A";
    private const string ButtonFontResourcePath = "Fonts/Square-Black";

    [SerializeField] private float fadeDuration = 0.6f;

    private Texture2D keyVisual;
    private Font buttonFont;
    private GUIStyle buttonStyle;
    private bool isTransitioning;
    private float fadeAlpha;

    private void Awake()
    {
        keyVisual = Resources.Load<Texture2D>(KeyVisualResourcePath);
        buttonFont = Resources.Load<Font>(ButtonFontResourcePath);
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

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeAlpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        fadeAlpha = 1f;
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
}
