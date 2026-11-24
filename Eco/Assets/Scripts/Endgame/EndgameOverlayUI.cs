using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Eco.Endgame
{
    /// <summary>
    /// Screen-space overlay used exclusively during the Endgame sequence:
    /// - NEXUS / ECO atmospheric subtitles (Etapa 3)
    /// - Uninterpretable forearm numeric readout (Etapa 5)
    /// - Hard cut to black, "ECOS DA MEMÓRIA / O fim da transmissão." title card
    /// - Scrolling credits (Luana Meneghini + Antigravity coding assistance)
    /// - Post-credits [ RECOMEÇAR / SAIR ] menu (easily switchable to Main Menu scene later)
    /// </summary>
    public class EndgameOverlayUI : MonoBehaviour
    {
        [Header("Credits Configuration")]
        [SerializeField] private float creditsScrollSpeed = 72f;
        [SerializeField] private string restartSceneName = "Gameplay";
        [Tooltip("Cena do Menu Principal carregada após os créditos.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [TextArea(16, 32)]
        [SerializeField] private string creditsContent =
            "<b>ECO</b>\n\n\n\n" +
            "<color=#4CC9E8>MODELAGEM 3D</color>\n" +
            "Luana Meneghini\n\n\n" +
            "<color=#4CC9E8>ANIMAÇÃO</color>\n" +
            "Luana Meneghini\n\n\n" +
            "<color=#4CC9E8>LEVEL DESIGN</color>\n" +
            "Luana Meneghini\n\n\n" +
            "<color=#4CC9E8>GAME DESIGN</color>\n" +
            "Luana Meneghini\n\n\n" +
            "<color=#4CC9E8>GAME TESTER</color>\n" +
            "Luana Meneghini\n\n\n" +
            "<color=#4CC9E8>PROGRAMAÇÃO</color>\n" +
            "Luana Meneghini *\n\n\n\n" +
            "<color=#D9A441>* Programação realizada com assistência nos códigos por Antigravity.</color>\n\n\n\n\n" +
            "<color=#6B8FA2>OBRIGADA POR JOGAR.</color>";

        private static readonly Color CyanBright = new Color(0.298f, 0.788f, 0.910f, 1f);
        private static readonly Color AmberGold = new Color(0.851f, 0.643f, 0.255f, 1f);
        private static readonly Color TextIce = new Color(0.886f, 0.945f, 0.973f, 1f);
        private static readonly Color TextMuted = new Color(0.420f, 0.560f, 0.635f, 1f);
        private static readonly Color SelectFill = new Color(0.063f, 0.216f, 0.278f, 0.95f);

        private static Font sharedMonoFont;

        private Canvas overlayCanvas;
        private CanvasGroup subtitleGroup;
        private Text subtitleText;

        private CanvasGroup forearmCodeGroup;
        private Text forearmCodeText;

        private Image blackScreenImage;

        private CanvasGroup titleCardGroup;
        private Text titleMainText;
        private Text titleSubText;

        private GameObject creditsRoot;
        private RectTransform creditsScrollRect;
        private Text creditsTextComponent;

        private GameObject postCreditsPanel;
        private Button restartButton;
        private Button quitButton;
        private Image restartBg;
        private Image quitBg;
        private Text restartLabel;
        private Text quitLabel;
        private bool postCreditsActive;
        private int postCreditsIndex;

        private void Awake()
        {
            EnsureFont();
            BuildOverlayCanvas();
            HideAllImmediate();
        }

        public void HideAllImmediate()
        {
            if (overlayCanvas != null) overlayCanvas.enabled = false;
            if (subtitleGroup != null) subtitleGroup.alpha = 0f;
            if (forearmCodeGroup != null) forearmCodeGroup.alpha = 0f;
            if (blackScreenImage != null) blackScreenImage.color = new Color(0f, 0f, 0f, 0f);
            if (titleCardGroup != null) titleCardGroup.alpha = 0f;
            if (creditsRoot != null) creditsRoot.SetActive(false);
            if (postCreditsPanel != null) postCreditsPanel.SetActive(false);
            postCreditsActive = false;
        }

        public void ShowSubtitle(string speaker, string line, bool isNexus)
        {
            if (overlayCanvas != null) overlayCanvas.enabled = true;
            string speakerHex = isNexus ? "#D9A441" : "#4CC9E8";
            subtitleText.text = $"<color={speakerHex}><b>{speaker}:</b></color> \"{line}\"";
            subtitleGroup.alpha = 1f;
        }

        public void HideSubtitle()
        {
            if (subtitleGroup != null) subtitleGroup.alpha = 0f;
        }

        public void ShowForearmCrypticSequence(string codeText, float alpha)
        {
            if (overlayCanvas != null) overlayCanvas.enabled = true;
            if (forearmCodeText != null) forearmCodeText.text = codeText;
            if (forearmCodeGroup != null) forearmCodeGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void CutToBlack()
        {
            if (overlayCanvas != null) overlayCanvas.enabled = true;
            HideSubtitle();
            ShowForearmCrypticSequence(string.Empty, 0f);
            if (blackScreenImage != null) blackScreenImage.color = Color.black;
        }

        public IEnumerator PlayTitleAndCreditsRoutine()
        {
            CutToBlack();

            // Silence in pure black
            yield return new WaitForSecondsRealtime(2.4f);

            // Fade in "ECOS DA MEMÓRIA / O fim da transmissão."
            yield return FadeCanvasGroup(titleCardGroup, 0f, 1f, 1.4f);
            yield return new WaitForSecondsRealtime(3.2f);
            yield return FadeCanvasGroup(titleCardGroup, 1f, 0f, 1.2f);

            yield return new WaitForSecondsRealtime(0.8f);

            // Scroll credits smoothly
            if (creditsRoot != null && creditsScrollRect != null)
            {
                creditsTextComponent.text = creditsContent;
                creditsRoot.SetActive(true);

                float startY = -640f;
                float endY = 1280f;
                creditsScrollRect.anchoredPosition = new Vector2(0f, startY);

                while (creditsScrollRect.anchoredPosition.y < endY)
                {
                    float speedMult = (Keyboard.current != null && (Keyboard.current.spaceKey.isPressed || Keyboard.current.enterKey.isPressed)) ? 2.6f : 1f;
                    creditsScrollRect.anchoredPosition += Vector2.up * (creditsScrollSpeed * speedMult * Time.unscaledDeltaTime);
                    yield return null;
                }

                creditsRoot.SetActive(false);
            }

            // Show post-credits options [ RECOMEÇAR / SAIR ]
            ShowPostCreditsMenu();
        }

        private void ShowPostCreditsMenu()
        {
            if (postCreditsPanel != null) postCreditsPanel.SetActive(true);
            postCreditsActive = true;
            postCreditsIndex = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshPostCreditsHighlight();
        }

        private void Update()
        {
            if (!postCreditsActive) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            {
                postCreditsIndex = (postCreditsIndex + 1) % 2;
                RefreshPostCreditsHighlight();
            }
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            {
                postCreditsIndex = (postCreditsIndex + 1) % 2;
                RefreshPostCreditsHighlight();
            }
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                if (postCreditsIndex == 0) HandleRestartOrMenu();
                else HandleQuit();
            }
        }

        private void RefreshPostCreditsHighlight()
        {
            if (restartBg != null) restartBg.color = postCreditsIndex == 0 ? SelectFill : new Color(0.04f, 0.08f, 0.11f, 0.65f);
            if (quitBg != null) quitBg.color = postCreditsIndex == 1 ? SelectFill : new Color(0.04f, 0.08f, 0.11f, 0.65f);
            if (restartLabel != null) restartLabel.color = postCreditsIndex == 0 ? CyanBright : TextIce;
            if (quitLabel != null) quitLabel.color = postCreditsIndex == 1 ? CyanBright : TextIce;
        }

        private void HandleRestartOrMenu()
        {
            string targetScene = !string.IsNullOrWhiteSpace(mainMenuSceneName) ? mainMenuSceneName : restartSceneName;
            if (string.IsNullOrWhiteSpace(targetScene)) targetScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(targetScene);
        }

        private void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;
            float elapsed = 0f;
            cg.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            cg.alpha = to;
        }

        private static void EnsureFont()
        {
            if (sharedMonoFont != null) return;
            string[] preferred = { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console" };
            try { sharedMonoFont = Font.CreateDynamicFontFromOSFont(preferred, 28); }
            catch { sharedMonoFont = null; }
            if (sharedMonoFont == null) sharedMonoFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void BuildOverlayCanvas()
        {
            GameObject canvasGo = new GameObject("EndgameOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);

            overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform rootRt = canvasGo.GetComponent<RectTransform>();

            // Blackout screen background
            GameObject blackGo = CreateUI("BlackScreen", rootRt);
            StretchFull(blackGo.GetComponent<RectTransform>());
            blackScreenImage = blackGo.AddComponent<Image>();
            blackScreenImage.color = new Color(0f, 0f, 0f, 0f);
            blackScreenImage.raycastTarget = false;

            // Subtitles bar (bottom letterbox, strictly outside the 3D monitor frame)
            GameObject subGo = CreateUI("SubtitlesPanel", rootRt);
            RectTransform subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.08f, 0.012f);
            subRt.anchorMax = new Vector2(0.92f, 0.088f);
            subRt.offsetMin = Vector2.zero;
            subRt.offsetMax = Vector2.zero;
            subtitleGroup = subGo.AddComponent<CanvasGroup>();
            Image subBg = subGo.AddComponent<Image>();
            subBg.color = new Color(0.01f, 0.02f, 0.03f, 0.88f);
            subBg.raycastTarget = false;

            GameObject subTxtGo = CreateUI("SubtitleText", subRt);
            StretchFull(subTxtGo.GetComponent<RectTransform>());
            subtitleText = subTxtGo.AddComponent<Text>();
            ConfigureText(subtitleText, string.Empty, 28, TextIce, FontStyle.Normal, TextAnchor.MiddleCenter);

            // Forearm cryptic numeric readout (lower center-right during Etapa 5)
            GameObject codeGo = CreateUI("ForearmCrypticReadout", rootRt);
            RectTransform codeRt = codeGo.GetComponent<RectTransform>();
            codeRt.anchorMin = new Vector2(0.30f, 0.20f);
            codeRt.anchorMax = new Vector2(0.70f, 0.34f);
            codeRt.offsetMin = Vector2.zero;
            codeRt.offsetMax = Vector2.zero;
            forearmCodeGroup = codeGo.AddComponent<CanvasGroup>();
            forearmCodeText = codeGo.AddComponent<Text>();
            ConfigureText(forearmCodeText, string.Empty, 28, CyanBright, FontStyle.Bold, TextAnchor.MiddleCenter);

            // Final Title Card ("ECOS DA MEMÓRIA / O fim da transmissão.")
            GameObject titleGo = CreateUI("FinalTitleCard", rootRt);
            StretchFull(titleGo.GetComponent<RectTransform>());
            titleCardGroup = titleGo.AddComponent<CanvasGroup>();

            GameObject mainT = CreateUI("MainTitle", titleGo.GetComponent<RectTransform>());
            RectTransform mainRt = mainT.GetComponent<RectTransform>();
            mainRt.anchorMin = new Vector2(0.1f, 0.48f);
            mainRt.anchorMax = new Vector2(0.9f, 0.62f);
            mainRt.offsetMin = Vector2.zero;
            mainRt.offsetMax = Vector2.zero;
            titleMainText = mainT.AddComponent<Text>();
            ConfigureText(titleMainText, "<b>ECOS DA MEMÓRIA</b>", 64, TextIce, FontStyle.Bold, TextAnchor.MiddleCenter);

            GameObject subT = CreateUI("SubTitle", titleGo.GetComponent<RectTransform>());
            RectTransform subTRt = subT.GetComponent<RectTransform>();
            subTRt.anchorMin = new Vector2(0.1f, 0.38f);
            subTRt.anchorMax = new Vector2(0.9f, 0.47f);
            subTRt.offsetMin = Vector2.zero;
            subTRt.offsetMax = Vector2.zero;
            titleSubText = subT.AddComponent<Text>();
            ConfigureText(titleSubText, "O fim da transmissão.", 30, TextMuted, FontStyle.Italic, TextAnchor.MiddleCenter);

            // Scrolling Credits
            creditsRoot = CreateUI("CreditsRoot", rootRt);
            StretchFull(creditsRoot.GetComponent<RectTransform>());
            GameObject scrollGo = CreateUI("CreditsText", creditsRoot.GetComponent<RectTransform>());
            creditsScrollRect = scrollGo.GetComponent<RectTransform>();
            creditsScrollRect.anchorMin = new Vector2(0.5f, 0.5f);
            creditsScrollRect.anchorMax = new Vector2(0.5f, 0.5f);
            creditsScrollRect.sizeDelta = new Vector2(1280f, 1600f);
            creditsTextComponent = scrollGo.AddComponent<Text>();
            ConfigureText(creditsTextComponent, creditsContent, 30, TextIce, FontStyle.Normal, TextAnchor.UpperCenter);
            creditsTextComponent.lineSpacing = 1.35f;

            // Post-Credits Menu ([ RECOMEÇAR / SAIR ])
            postCreditsPanel = CreateUI("PostCreditsPanel", rootRt);
            StretchFull(postCreditsPanel.GetComponent<RectTransform>());

            GameObject pcTitle = CreateUI("EndHeader", postCreditsPanel.GetComponent<RectTransform>());
            RectTransform pcTitleRt = pcTitle.GetComponent<RectTransform>();
            pcTitleRt.anchorMin = new Vector2(0.2f, 0.56f);
            pcTitleRt.anchorMax = new Vector2(0.8f, 0.66f);
            pcTitleRt.offsetMin = Vector2.zero;
            pcTitleRt.offsetMax = Vector2.zero;
            Text pcTitleTxt = pcTitle.AddComponent<Text>();
            ConfigureText(pcTitleTxt, "<b>ECO</b>", 54, TextIce, FontStyle.Bold, TextAnchor.MiddleCenter);

            string restartCaption = string.IsNullOrWhiteSpace(mainMenuSceneName) ? "[ RECOMEÇAR ]" : "[ MENU PRINCIPAL ]";
            (restartButton, restartBg, restartLabel) = CreateActionButton(
                postCreditsPanel.GetComponent<RectTransform>(),
                "RestartButton",
                restartCaption,
                new Vector2(-210f, -40f),
                HandleRestartOrMenu);

            (quitButton, quitBg, quitLabel) = CreateActionButton(
                postCreditsPanel.GetComponent<RectTransform>(),
                "QuitButton",
                "[ SAIR ]",
                new Vector2(210f, -40f),
                HandleQuit);
        }

        private static (Button, Image, Text) CreateActionButton(
            RectTransform parent,
            string name,
            string caption,
            Vector2 anchoredPos,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = CreateUI(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(340f, 74f);
            rt.anchoredPosition = anchoredPos;

            Image img = go.AddComponent<Image>();
            img.color = SelectFill;
            Button btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            GameObject lblGo = CreateUI("Label", rt);
            StretchFull(lblGo.GetComponent<RectTransform>());
            Text lbl = lblGo.AddComponent<Text>();
            ConfigureText(lbl, caption, 26, CyanBright, FontStyle.Bold, TextAnchor.MiddleCenter);
            return (btn, img, lbl);
        }

        private static void ConfigureText(Text txt, string content, int fontSize, Color color, FontStyle style, TextAnchor alignment)
        {
            txt.font = sharedMonoFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.fontStyle = style;
            txt.alignment = alignment;
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
        }

        private static GameObject CreateUI(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
