using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Eco.Save;

namespace Eco.UI
{
    public class MainMenuOptionButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public int Index { get; private set; }
        private Action<int> onHover;
        private Action<int> onClick;

        public void Bind(int index, Action<int> hoverCallback, Action<int> clickCallback)
        {
            Index = index;
            onHover = hoverCallback;
            onClick = clickCallback;
        }

        public void OnPointerEnter(PointerEventData eventData) => onHover?.Invoke(Index);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                onClick?.Invoke(Index);
            }
        }
    }

    /// <summary>
    /// Retro-industrial CRT Main Menu matching ECHO's diegetic save and endgame terminals.
    /// Supports both keyboard (W/S, Up/Down, Enter/Space) and mouse navigation.
    /// Connects directly to EcoSaveManager for New Game, Continue, Tutorial hook, and Quit.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Scene Navigation")]
        [SerializeField] private string gameplaySceneName = "Gameplay";
        [SerializeField] private string tutorialSceneName = "Tutorial";

        private static readonly Color ScreenBg = new Color(0.015f, 0.024f, 0.034f, 1.00f);
        private static readonly Color PanelBg = new Color(0.043f, 0.082f, 0.114f, 0.98f);
        private static readonly Color BorderDim = new Color(0.227f, 0.400f, 0.478f, 0.88f);
        private static readonly Color DividerCol = new Color(0.165f, 0.310f, 0.376f, 0.85f);
        private static readonly Color CyanBright = new Color(0.298f, 0.788f, 0.910f, 1.00f);
        private static readonly Color AmberGold = new Color(0.851f, 0.643f, 0.255f, 1.00f);
        private static readonly Color TextIce = new Color(0.886f, 0.945f, 0.973f, 1.00f);
        private static readonly Color TextBody = new Color(0.722f, 0.831f, 0.890f, 1.00f);
        private static readonly Color TextDisabled = new Color(0.360f, 0.460f, 0.520f, 0.65f);
        private static readonly Color SelectFill = new Color(0.063f, 0.216f, 0.278f, 0.85f);
        private static readonly Color UnselectFill = new Color(0.043f, 0.082f, 0.114f, 0.01f);

        private static Font sharedMonoFont;
        private static Texture2D sharedScanlineTex;

        private RectTransform contentRoot;
        private CanvasGroup glitchGroup;
        private RawImage scanlineOverlay;
        private Text footerStatusText;

        private readonly List<MenuRow> rows = new List<MenuRow>();
        private int selectedIndex;
        private bool hasSavedGame;
        private EcoSaveData cachedSavePreview;
        private float glitchTimer;
        private float statusResetTimer;

        private class MenuRow
        {
            public Image bg;
            public Image[] borders;
            public Text label;
            public string baseText;
            public bool enabled;
            public Action onActivate;
        }

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            EnsureSharedResources();
            RefreshSaveAvailability();
            BuildMainMenuCanvas();
            RefreshRowVisuals();
        }

        private void RefreshSaveAvailability()
        {
            cachedSavePreview = EcoSaveManager.PeekSavedGame();
            hasSavedGame = cachedSavePreview != null;
        }

        private void Update()
        {
            UpdateCrtAndGlitch();

            if (statusResetTimer > 0f)
            {
                statusResetTimer -= Time.unscaledDeltaTime;
                if (statusResetTimer <= 0f)
                {
                    SetDefaultFooterStatus();
                }
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex - 1 + rows.Count) % rows.Count;
                RefreshRowVisuals();
            }
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex + 1) % rows.Count;
                RefreshRowVisuals();
            }
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                ActivateIndex(selectedIndex);
            }
        }

        private void UpdateCrtAndGlitch()
        {
            if (scanlineOverlay != null)
            {
                Rect uv = scanlineOverlay.uvRect;
                uv.y = (uv.y + Time.unscaledDeltaTime * 0.06f) % 1f;
                scanlineOverlay.uvRect = uv;
            }

            if (contentRoot == null || glitchGroup == null) return;

            glitchTimer -= Time.unscaledDeltaTime;
            if (glitchTimer <= 0f)
            {
                if (UnityEngine.Random.value < 0.14f)
                {
                    contentRoot.anchoredPosition = new Vector2(UnityEngine.Random.Range(-2.5f, 2.5f), 0f);
                    glitchGroup.alpha = UnityEngine.Random.Range(0.93f, 1f);
                    glitchTimer = UnityEngine.Random.Range(0.03f, 0.08f);
                }
                else
                {
                    contentRoot.anchoredPosition = Vector2.zero;
                    glitchGroup.alpha = 1f;
                    glitchTimer = UnityEngine.Random.Range(1.4f, 3.2f);
                }
            }
        }

        private void ActivateIndex(int idx)
        {
            if (idx < 0 || idx >= rows.Count) return;
            rows[idx].onActivate?.Invoke();
        }

        private void OnStartNewGame()
        {
            EcoSaveManager.DeleteSavedGameOnDisk();
            EcoSaveManager.PendingLoadOnNextGameplay = false;
            SetTransientFooter("INICIANDO NOVO PROCESSO...", CyanBright, 4f);
            LoadSceneIfExists(gameplaySceneName);
        }

        private void OnContinueRecord()
        {
            RefreshSaveAvailability();
            if (!hasSavedGame)
            {
                SetTransientFooter("NENHUM REGISTRO LOCAL ENCONTRADO.", AmberGold, 2.5f);
                return;
            }

            EcoSaveManager.PendingLoadOnNextGameplay = true;
            string floor = string.IsNullOrEmpty(cachedSavePreview.lastFloorLabel) ? "SETOR 07" : cachedSavePreview.lastFloorLabel;
            SetTransientFooter($"RESTAURANDO REGISTRO // {floor}...", CyanBright, 4f);
            LoadSceneIfExists(gameplaySceneName);
        }

        private void OnOpenTutorial()
        {
            if (Application.CanStreamedLevelBeLoaded(tutorialSceneName))
            {
                EcoSaveManager.PendingLoadOnNextGameplay = false;
                SetTransientFooter("CARREGANDO PROTOCOLO DE OPERAÇÃO...", CyanBright, 4f);
                SceneManager.LoadScene(tutorialSceneName);
            }
            else
            {
                SetTransientFooter("PROTOCOLO DE OPERAÇÃO // MÓDULO DE TUTORIAL EM PREPARAÇÃO.", AmberGold, 3.0f);
            }
        }

        private void OnQuitGame()
        {
            SetTransientFooter("ENCERRANDO SISTEMA...", AmberGold, 2f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void LoadSceneIfExists(string sceneName)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                SetTransientFooter($"ERRO: CENA '{sceneName}' NÃO ENCONTRADA NO BUILD SETTINGS.", AmberGold, 3.5f);
            }
        }

        private void SetTransientFooter(string msg, Color color, float duration)
        {
            if (footerStatusText == null) return;
            footerStatusText.text = msg;
            footerStatusText.color = color;
            statusResetTimer = duration;
        }

        private void SetDefaultFooterStatus()
        {
            if (footerStatusText == null) return;

            if (selectedIndex == 1)
            {
                if (hasSavedGame)
                {
                    string floor = string.IsNullOrEmpty(cachedSavePreview.lastFloorLabel) ? "SETOR 07" : cachedSavePreview.lastFloorLabel;
                    footerStatusText.text = $"REGISTRO DISPONÍVEL // {floor}  (HP: {cachedSavePreview.currentHealth:F0}%)";
                    footerStatusText.color = CyanBright;
                }
                else
                {
                    footerStatusText.text = "NENHUM REGISTRO SALVO DISPONÍVEL";
                    footerStatusText.color = TextDisabled;
                }
            }
            else
            {
                footerStatusText.text = "AGUARDANDO COMANDO...";
                footerStatusText.color = TextBody;
            }
        }

        private void RefreshRowVisuals()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                MenuRow r = rows[i];
                bool selected = (i == selectedIndex);
                r.bg.color = selected ? SelectFill : UnselectFill;
                foreach (Image b in r.borders) b.enabled = selected;

                if (!r.enabled)
                {
                    r.label.color = selected ? AmberGold : TextDisabled;
                    r.label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                    r.label.text = selected ? $"> {r.baseText}  [INDISPONÍVEL]" : $"  {r.baseText}";
                }
                else
                {
                    r.label.color = selected ? CyanBright : TextBody;
                    r.label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                    r.label.text = selected ? $"> {r.baseText}" : $"  {r.baseText}";
                }
            }

            if (statusResetTimer <= 0f)
            {
                SetDefaultFooterStatus();
            }
        }

        // =========================================================================
        // Procedural CRT Main Menu Canvas
        // =========================================================================

        private static void EnsureSharedResources()
        {
            if (sharedMonoFont == null)
            {
                string[] preferred = { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console" };
                try { sharedMonoFont = Font.CreateDynamicFontFromOSFont(preferred, 28); }
                catch { sharedMonoFont = null; }
                if (sharedMonoFont == null) sharedMonoFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (sharedScanlineTex == null)
            {
                sharedScanlineTex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Point
                };
                Color clear = new Color(0f, 0f, 0f, 0f);
                Color line = new Color(0f, 0.04f, 0.06f, 0.24f);
                for (int y = 0; y < 4; y++)
                {
                    Color c = (y == 0) ? line : clear;
                    for (int x = 0; x < 4; x++) sharedScanlineTex.SetPixel(x, y, c);
                }
                sharedScanlineTex.Apply();
            }
        }

        private void BuildMainMenuCanvas()
        {
            GameObject canvasGo = new GameObject("MainMenuCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform rootRt = canvasGo.GetComponent<RectTransform>();

            // Fullscreen dark backdrop
            GameObject backdrop = CreateUI("Backdrop", rootRt);
            StretchFull(backdrop.GetComponent<RectTransform>());
            Image backdropImg = backdrop.AddComponent<Image>();
            backdropImg.color = ScreenBg;
            backdropImg.raycastTarget = false;

            // Centered industrial terminal frame matching reference proportions
            GameObject frameGo = CreateUI("TerminalFrame", rootRt);
            RectTransform frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.5f, 0.5f);
            frameRt.anchorMax = new Vector2(0.5f, 0.5f);
            frameRt.sizeDelta = new Vector2(1240f, 760f);
            frameRt.anchoredPosition = Vector2.zero;

            Image frameImg = frameGo.AddComponent<Image>();
            frameImg.color = PanelBg;
            AddBorderOutline(frameRt, BorderDim, 2.5f);

            // Glitchable content root
            GameObject contentGo = CreateUI("ContentRoot", frameRt);
            contentRoot = contentGo.GetComponent<RectTransform>();
            StretchFull(contentRoot, 48f, 36f, 48f, 36f);
            glitchGroup = contentGo.AddComponent<CanvasGroup>();

            // Header: COMPLEXO PRIMÁRIO // NEXUS
            CreateLabel(contentRoot, "<b>COMPLEXO PRIMÁRIO // NEXUS</b>", 30, CyanBright, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, -6f), new Vector2(1144f, 46f));
            CreateDivider(contentRoot, -74f, 1144f);

            // Title block: ECO + SETOR DE CONTENÇÃO 07
            CreateLabel(contentRoot, "<b>ECO</b>", 62, TextIce, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, -104f), new Vector2(1144f, 76f));
            CreateLabel(contentRoot, "SETOR DE CONTENÇÃO 07", 24, TextBody, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, -184f), new Vector2(1144f, 38f));
            CreateDivider(contentRoot, -248f, 1144f);

            // 4 Menu Options
            rows.Clear();
            (string text, bool isEnabled, Action action)[] defs =
            {
                ("INICIAR PROCESSO", true, OnStartNewGame),
                ("CONTINUAR REGISTRO", hasSavedGame, OnContinueRecord),
                ("PROTOCOLO DE OPERAÇÃO", true, OnOpenTutorial),
                ("ENCERRAR SISTEMA", true, OnQuitGame)
            };

            for (int i = 0; i < defs.Length; i++)
            {
                float y = -282f - (i * 76f);
                rows.Add(CreateMenuRow(contentRoot, defs[i].text, defs[i].isEnabled, defs[i].action, i, new Vector2(0f, y), 1144f, 64f));
            }

            // Bottom Divider + Status
            CreateDivider(contentRoot, -604f, 1144f);
            footerStatusText = CreateLabel(contentRoot, "AGUARDANDO COMANDO...", 22, TextBody, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, -626f), new Vector2(1144f, 44f));

            // CRT Scanlines overlay
            GameObject scanGo = CreateUI("CRTScanlines", frameRt);
            StretchFull(scanGo.GetComponent<RectTransform>());
            scanlineOverlay = scanGo.AddComponent<RawImage>();
            scanlineOverlay.texture = sharedScanlineTex;
            scanlineOverlay.uvRect = new Rect(0f, 0f, 1f, 95f);
            scanlineOverlay.raycastTarget = false;
        }

        private MenuRow CreateMenuRow(RectTransform parent, string text, bool isEnabled, Action onActivate, int index, Vector2 pos, float width, float height)
        {
            GameObject go = CreateUI($"Option_{index}", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetTopLeft(rt, pos, new Vector2(width, height));

            Image bg = go.AddComponent<Image>();
            bg.color = UnselectFill;
            bg.raycastTarget = true;

            MainMenuOptionButton btn = go.AddComponent<MainMenuOptionButton>();
            btn.Bind(
                index,
                idx =>
                {
                    selectedIndex = idx;
                    RefreshRowVisuals();
                },
                idx =>
                {
                    selectedIndex = idx;
                    RefreshRowVisuals();
                    ActivateIndex(idx);
                });

            Image[] borders = AddBorderOutline(rt, CyanBright, 2f);

            GameObject lblGo = CreateUI("Label", rt);
            StretchFull(lblGo.GetComponent<RectTransform>(), 24f, 0f, 24f, 0f);
            Text lbl = lblGo.AddComponent<Text>();
            ConfigureText(lbl, text, 28, TextBody, FontStyle.Normal, TextAnchor.MiddleLeft);
            lbl.raycastTarget = false;

            return new MenuRow
            {
                bg = bg,
                borders = borders,
                label = lbl,
                baseText = text,
                enabled = isEnabled,
                onActivate = onActivate
            };
        }

        private static void CreateDivider(RectTransform parent, float yPos, float width)
        {
            GameObject go = CreateUI("Divider", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetTopLeft(rt, new Vector2(0f, yPos), new Vector2(width, 2f));
            Image img = go.AddComponent<Image>();
            img.color = DividerCol;
            img.raycastTarget = false;
        }

        private static Text CreateLabel(RectTransform parent, string content, int size, Color color, FontStyle style, TextAnchor align, Vector2 pos, Vector2 dims)
        {
            GameObject go = CreateUI("Label", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetTopLeft(rt, pos, dims);
            Text txt = go.AddComponent<Text>();
            ConfigureText(txt, content, size, color, style, align);
            txt.raycastTarget = false;
            return txt;
        }

        private static Image[] AddBorderOutline(RectTransform parent, Color color, float thickness)
        {
            Image top = CreateBorderEdge("BorderTop", parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
            Image bottom = CreateBorderEdge("BorderBottom", parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
            Image left = CreateBorderEdge("BorderLeft", parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
            Image right = CreateBorderEdge("BorderRight", parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero);
            return new[] { top, bottom, left, right };
        }

        private static Image CreateBorderEdge(string name, RectTransform parent, Color color, Vector2 min, Vector2 max, Vector2 oMin, Vector2 oMax)
        {
            GameObject go = CreateUI(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
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
        }

        private static GameObject CreateUI(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopLeft(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }
    }
}
