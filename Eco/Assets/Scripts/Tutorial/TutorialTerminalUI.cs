using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Eco.Save;

namespace Eco.Tutorial
{
    /// <summary>
    /// Retro-industrial World Space CRT UI rendered directly on the M1 monitor,
    /// matching SaveTerminalUI's exact NEXUS aesthetic (palette, monospace font,
    /// scanlines, subtle glitch, keyboard + mouse navigation).
    /// </summary>
    public class TutorialTerminalUI : MonoBehaviour
    {
        private const float CanvasWidth = 960f;
        private const float CanvasHeight = 680f;

        // Industrial NEXUS palette matching SaveTerminalUI
        private static readonly Color BgDark = new Color(0.043f, 0.082f, 0.114f, 1.00f);         // #0B151D
        private static readonly Color BorderDim = new Color(0.227f, 0.400f, 0.478f, 0.85f);      // #3A667A
        private static readonly Color DividerCol = new Color(0.165f, 0.310f, 0.376f, 0.85f);     // #2A4F60
        private static readonly Color CyanBright = new Color(0.298f, 0.788f, 0.910f, 1.00f);     // #4CC9E8
        private static readonly Color GreenSynced = new Color(0.361f, 0.882f, 0.722f, 1.00f);    // #5CE1B8
        private static readonly Color TextIce = new Color(0.886f, 0.945f, 0.973f, 1.00f);        // #E2F1F8
        private static readonly Color TextBody = new Color(0.722f, 0.831f, 0.890f, 1.00f);       // #B8D4E3
        private static readonly Color TextMuted = new Color(0.420f, 0.560f, 0.635f, 1.00f);      // #6B8FA2
        private static readonly Color SelectFill = new Color(0.063f, 0.216f, 0.278f, 0.95f);     // #103747
        private static readonly Color UnselectFill = new Color(0.043f, 0.082f, 0.114f, 0.01f);

        private static Font sharedMonoFont;
        private static Texture2D sharedScanlineTex;

        private Canvas worldCanvas;
        private RectTransform contentRoot;
        private RawImage scanlineOverlay;
        private CanvasGroup glitchGroup;
        private Text footerStatusText;

        private readonly List<MenuRowVisual> menuRows = new List<MenuRowVisual>();
        private int selectedIndex;
        private bool isFocused;
        private bool sequenceStarted;
        private float glitchTimer;
        private float blinkTimer;

        private Action onStartSequenceRequested;
        private Action onDisconnectRequested;

        public bool IsFocused => isFocused;
        public bool SequenceStarted => sequenceStarted;

        private class MenuRowVisual
        {
            public Image background;
            public Image[] borders;
            public Text label;
            public string baseText;
            public Action onActivate;
        }

        public void Initialize(
            Vector3 screenFrontWorldPos,
            Vector3 screenForwardWorld,
            Vector2 screenWorldSize,
            Action startSequenceCallback,
            Action disconnectCallback)
        {
            onStartSequenceRequested = startSequenceCallback;
            onDisconnectRequested = disconnectCallback;

            EnsureSharedResources();
            BuildWorldSpaceCanvas(screenFrontWorldPos, screenForwardWorld, screenWorldSize);
            BuildScreenLayout();
            RefreshRowHighlights();
        }

        public void SetFocused(bool focused)
        {
            isFocused = focused;
            if (focused)
            {
                selectedIndex = 0;
                RefreshRowHighlights();
            }
        }

        public void MarkSequenceStarted()
        {
            sequenceStarted = true;
            if (menuRows.Count > 0)
            {
                menuRows[0].baseText = "SEQUÊNCIA DE TESTES ATIVA";
            }
            if (footerStatusText != null)
            {
                footerStatusText.text = "> ACESSO LIBERADO // SIGA A TRILHA LUMINOSA";
                footerStatusText.color = GreenSynced;
            }
            RefreshRowHighlights();
        }

        public void TriggerStartFromCode()
        {
            if (!sequenceStarted)
            {
                onStartSequenceRequested?.Invoke();
            }
        }

        private void Update()
        {
            if (worldCanvas != null && worldCanvas.worldCamera == null && Camera.main != null)
            {
                worldCanvas.worldCamera = Camera.main;
            }

            UpdateCrtAndGlitch();
            UpdateFooterBlink();

            if (!isFocused) return;
            HandleKeyboardNavigation();
        }

        private void UpdateCrtAndGlitch()
        {
            if (scanlineOverlay != null)
            {
                Rect uv = scanlineOverlay.uvRect;
                uv.y = (uv.y + Time.unscaledDeltaTime * 0.08f) % 1f;
                scanlineOverlay.uvRect = uv;
            }

            if (contentRoot == null || glitchGroup == null) return;

            glitchTimer -= Time.unscaledDeltaTime;
            if (glitchTimer <= 0f)
            {
                if (UnityEngine.Random.value < 0.14f)
                {
                    contentRoot.anchoredPosition = new Vector2(UnityEngine.Random.Range(-2.2f, 2.2f), 0f);
                    glitchGroup.alpha = UnityEngine.Random.Range(0.93f, 1f);
                    glitchTimer = UnityEngine.Random.Range(0.03f, 0.08f);
                }
                else
                {
                    contentRoot.anchoredPosition = Vector2.zero;
                    glitchGroup.alpha = 1f;
                    glitchTimer = UnityEngine.Random.Range(1.1f, 2.8f);
                }
            }
        }

        private void UpdateFooterBlink()
        {
            if (footerStatusText == null || sequenceStarted) return;

            blinkTimer += Time.unscaledDeltaTime;
            bool showCursor = ((int)(blinkTimer * 2f) % 2) == 0;
            footerStatusText.text = showCursor
                ? "AGUARDANDO OPERADOR..._"
                : "AGUARDANDO OPERADOR... ";
        }

        private void HandleKeyboardNavigation()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex - 1 + menuRows.Count) % menuRows.Count;
                RefreshRowHighlights();
            }
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex + 1) % menuRows.Count;
                RefreshRowHighlights();
            }
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                ActivateSelected();
            }
            else if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
            {
                onDisconnectRequested?.Invoke();
            }
        }

        private void ActivateSelected()
        {
            if (menuRows.Count == 0) return;
            int idx = Mathf.Clamp(selectedIndex, 0, menuRows.Count - 1);
            menuRows[idx].onActivate?.Invoke();
        }

        private void RefreshRowHighlights()
        {
            for (int i = 0; i < menuRows.Count; i++)
            {
                MenuRowVisual r = menuRows[i];
                bool selected = (i == selectedIndex);
                r.background.color = selected ? SelectFill : UnselectFill;
                foreach (Image b in r.borders) b.enabled = selected;

                if (i == 0 && sequenceStarted)
                {
                    r.label.color = GreenSynced;
                    r.label.fontStyle = FontStyle.Bold;
                    r.label.text = $"[OK] {r.baseText}";
                }
                else
                {
                    r.label.color = selected ? CyanBright : TextBody;
                    r.label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                    r.label.text = selected ? $"> {r.baseText}" : $"  {r.baseText}";
                }
            }
        }

        private static void EnsureSharedResources()
        {
            if (sharedMonoFont == null)
            {
                string[] preferred = { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console" };
                try { sharedMonoFont = Font.CreateDynamicFontFromOSFont(preferred, 24); }
                catch { sharedMonoFont = null; }
                if (sharedMonoFont == null)
                    sharedMonoFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        private void BuildWorldSpaceCanvas(Vector3 screenFrontWorldPos, Vector3 screenForwardWorld, Vector2 screenWorldSize)
        {
            GameObject canvasGo = new GameObject("TutorialTerminalCanvas");
            canvasGo.transform.SetParent(transform, true);

            canvasGo.transform.position = screenFrontWorldPos + screenForwardWorld.normalized * 0.008f;
            canvasGo.transform.rotation = Quaternion.LookRotation(-screenForwardWorld.normalized, Vector3.up);

            worldCanvas = canvasGo.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = Camera.main;
            worldCanvas.sortingOrder = 5;

            canvasGo.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            float scaleX = (screenWorldSize.x * 0.99f) / CanvasWidth;
            float scaleY = (screenWorldSize.y * 0.99f) / CanvasHeight;
            canvasGo.transform.localScale = Vector3.one;
            Vector3 pScale = transform.lossyScale;
            canvasGo.transform.localScale = new Vector3(
                scaleX / (Mathf.Approximately(pScale.x, 0f) ? 1f : pScale.x),
                scaleY / (Mathf.Approximately(pScale.y, 0f) ? 1f : pScale.y),
                1f / (Mathf.Approximately(pScale.z, 0f) ? 1f : pScale.z));

            GameObject bgGo = CreateUIObject("FrameBackground", canvasRect);
            StretchFull(bgGo.GetComponent<RectTransform>());
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.color = BgDark;
            AddBorderOutline(bgGo.GetComponent<RectTransform>(), BorderDim, 3f);

            GameObject contentGo = CreateUIObject("ContentRoot", bgGo.GetComponent<RectTransform>());
            contentRoot = contentGo.GetComponent<RectTransform>();
            StretchFull(contentRoot, 32f, 28f, 32f, 28f);
            glitchGroup = contentGo.AddComponent<CanvasGroup>();

            GameObject scanGo = CreateUIObject("CRTScanlines", bgGo.GetComponent<RectTransform>());
            StretchFull(scanGo.GetComponent<RectTransform>());
            scanlineOverlay = scanGo.AddComponent<RawImage>();
            scanlineOverlay.texture = sharedScanlineTex;
            scanlineOverlay.uvRect = new Rect(0f, 0f, 1f, 85f);
            scanlineOverlay.raycastTarget = false;
        }

        private void BuildScreenLayout()
        {
            // Top bar: TELA DO TERMINAL  |  NEXUS OS
            CreateLabel(contentRoot, "TELA DO TERMINAL", 20, TextMuted, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(0f, -4f), new Vector2(600f, 36f));
            CreateLabel(contentRoot, "NEXUS OS", 18, TextMuted, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(620f, -4f), new Vector2(276f, 36f));
            CreateDivider(contentRoot, -48f);

            // COMPLEXO PRIMÁRIO // NEXUS
            CreateLabel(contentRoot, "<b>COMPLEXO PRIMÁRIO // NEXUS</b>", 28, CyanBright, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -74f), new Vector2(896f, 44f));

            // PROTOCOLO DE OPERAÇÃO
            CreateLabel(contentRoot, "<b>PROTOCOLO DE OPERAÇÃO</b>", 40, TextIce, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -130f), new Vector2(896f, 56f));

            // SETOR DE TESTES // UNIDADE 07
            CreateLabel(contentRoot, "SETOR DE TESTES // UNIDADE 07", 25, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -198f), new Vector2(896f, 40f));

            CreateDivider(contentRoot, -264f);

            // Interactive options
            menuRows.Clear();
            menuRows.Add(CreateMenuRow(
                contentRoot,
                "INICIAR SEQUÊNCIA DE TESTES",
                new Vector2(0f, -310f),
                896f,
                74f,
                0,
                () =>
                {
                    if (!sequenceStarted) onStartSequenceRequested?.Invoke();
                }));

            menuRows.Add(CreateMenuRow(
                contentRoot,
                "ENCERRAR CONEXÃO",
                new Vector2(0f, -402f),
                896f,
                60f,
                1,
                () => onDisconnectRequested?.Invoke()));

            CreateDivider(contentRoot, -514f);

            // Footer: AGUARDANDO OPERADOR...
            footerStatusText = CreateLabel(contentRoot, "AGUARDANDO OPERADOR...", 24, CyanBright, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(0f, -544f), new Vector2(896f, 48f));
        }

        private MenuRowVisual CreateMenuRow(RectTransform parent, string text, Vector2 pos, float width, float height, int index, Action onActivate)
        {
            GameObject go = CreateUIObject($"Option_{index}", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetTopLeft(rt, pos, new Vector2(width, height));

            Image bg = go.AddComponent<Image>();
            bg.color = UnselectFill;
            bg.raycastTarget = true;

            TerminalOptionButton btn = go.AddComponent<TerminalOptionButton>();
            btn.Bind(
                index,
                idx =>
                {
                    if (!isFocused) return;
                    selectedIndex = idx;
                    RefreshRowHighlights();
                },
                idx =>
                {
                    if (!isFocused) return;
                    selectedIndex = idx;
                    RefreshRowHighlights();
                    menuRows[idx].onActivate?.Invoke();
                });

            Image[] borders = AddBorderOutline(rt, CyanBright, 2.5f);

            GameObject lblGo = CreateUIObject("Label", rt);
            StretchFull(lblGo.GetComponent<RectTransform>(), 20f, 0f, 20f, 0f);
            Text lbl = lblGo.AddComponent<Text>();
            ConfigureText(lbl, text, index == 0 ? 28 : 22, TextBody, FontStyle.Normal, TextAnchor.MiddleLeft);
            lbl.raycastTarget = false;

            return new MenuRowVisual
            {
                background = bg,
                borders = borders,
                label = lbl,
                baseText = text,
                onActivate = onActivate
            };
        }

        private static void CreateDivider(RectTransform parent, float yPos)
        {
            GameObject go = CreateUIObject("Divider", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetTopLeft(rt, new Vector2(0f, yPos), new Vector2(896f, 2f));
            Image img = go.AddComponent<Image>();
            img.color = DividerCol;
            img.raycastTarget = false;
        }

        private static Text CreateLabel(RectTransform parent, string content, int size, Color color, FontStyle style, TextAnchor align, Vector2 pos, Vector2 dims)
        {
            GameObject go = CreateUIObject("Label", parent);
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
            GameObject go = CreateUIObject(name, parent);
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

        private static GameObject CreateUIObject(string name, RectTransform parent)
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
