using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Eco.Save;

namespace Eco.Tutorial
{
    /// <summary>
    /// Retro-industrial World Space CRT UI for the final M7 monitor ('monitor M7__exp'),
    /// matching SaveTerminalUI and TutorialTerminalUI's exact NEXUS aesthetic.
    /// </summary>
    public class TutorialM7TerminalUI : MonoBehaviour
    {
        private const float CanvasWidth = 960f;
        private const float CanvasHeight = 680f;

        private static readonly Color BgDark = new Color(0.043f, 0.082f, 0.114f, 1.00f);
        private static readonly Color BorderDim = new Color(0.227f, 0.400f, 0.478f, 0.85f);
        private static readonly Color DividerCol = new Color(0.165f, 0.310f, 0.376f, 0.85f);
        private static readonly Color CyanBright = new Color(0.298f, 0.788f, 0.910f, 1.00f);
        private static readonly Color GreenSynced = new Color(0.361f, 0.882f, 0.722f, 1.00f);
        private static readonly Color TextIce = new Color(0.886f, 0.945f, 0.973f, 1.00f);
        private static readonly Color TextBody = new Color(0.722f, 0.831f, 0.890f, 1.00f);
        private static readonly Color TextMuted = new Color(0.420f, 0.560f, 0.635f, 1.00f);
        private static readonly Color SelectFill = new Color(0.063f, 0.216f, 0.278f, 0.95f);
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
        private bool protocolFinished;
        private float glitchTimer;

        private Action onEndProtocolRequested;
        private Action onDisconnectRequested;

        public bool IsFocused => isFocused;
        public bool ProtocolFinished => protocolFinished;

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
            Action endProtocolCallback,
            Action disconnectCallback)
        {
            onEndProtocolRequested = endProtocolCallback;
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

        public void MarkProtocolFinished()
        {
            protocolFinished = true;
            if (menuRows.Count > 0)
            {
                menuRows[0].baseText = "PROTOCOLO ENCERRADO";
            }
            if (footerStatusText != null)
            {
                footerStatusText.text = "> SAÍDA LIBERADA // RETORNANDO AO MENU PRINCIPAL...";
                footerStatusText.color = GreenSynced;
            }
            RefreshRowHighlights();
        }

        public void TriggerEndProtocolFromCode()
        {
            if (!protocolFinished)
            {
                onEndProtocolRequested?.Invoke();
            }
        }

        private void Update()
        {
            if (worldCanvas != null && worldCanvas.worldCamera == null && Camera.main != null)
            {
                worldCanvas.worldCamera = Camera.main;
            }

            UpdateCrtAndGlitch();

            if (!isFocused || protocolFinished) return;
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
                if (menuRows.Count > 0)
                {
                    menuRows[Mathf.Clamp(selectedIndex, 0, menuRows.Count - 1)].onActivate?.Invoke();
                }
            }
            else if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
            {
                onDisconnectRequested?.Invoke();
            }
        }

        private void RefreshRowHighlights()
        {
            for (int i = 0; i < menuRows.Count; i++)
            {
                MenuRowVisual r = menuRows[i];
                bool selected = (i == selectedIndex);
                r.background.color = selected ? SelectFill : UnselectFill;
                foreach (Image b in r.borders) b.enabled = selected;

                if (i == 0 && protocolFinished)
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
            GameObject canvasGo = new GameObject("TutorialM7TerminalCanvas");
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
            StretchFull(contentRoot, 32f, 26f, 32f, 26f);
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
            CreateLabel(contentRoot, "<b>COMPLEXO PRIMÁRIO // NEXUS</b>", 24, CyanBright, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(0f, -4f), new Vector2(680f, 38f));
            CreateLabel(contentRoot, "NEXUS OS", 18, TextMuted, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(680f, -4f), new Vector2(216f, 38f));
            CreateDivider(contentRoot, -48f);

            CreateLabel(contentRoot, "<b>PROTOCOLO DE OPERAÇÃO</b>", 36, TextIce, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -72f), new Vector2(896f, 48f));

            CreateLabel(contentRoot, "<b>SEQUÊNCIA DE TESTES CONCLUÍDA</b>", 26, GreenSynced, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -132f), new Vector2(896f, 38f));

            CreateDivider(contentRoot, -184f);

            CreateLabel(contentRoot, "REGISTRO DE DESEMPENHO: <color=#5CE1B8><b>APROVADO</b></color>", 24, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -206f), new Vector2(896f, 36f));

            CreateLabel(contentRoot, "OPERADOR APTO PARA PROSSEGUIR.", 24, TextIce, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -252f), new Vector2(896f, 36f));

            CreateDivider(contentRoot, -308f);

            menuRows.Clear();
            menuRows.Add(CreateMenuRow(
                contentRoot,
                "ENCERRAR PROTOCOLO",
                new Vector2(0f, -342f),
                896f,
                72f,
                0,
                () =>
                {
                    if (!protocolFinished) onEndProtocolRequested?.Invoke();
                }));

            menuRows.Add(CreateMenuRow(
                contentRoot,
                "VOLTAR",
                new Vector2(0f, -430f),
                896f,
                56f,
                1,
                () => onDisconnectRequested?.Invoke()));

            CreateDivider(contentRoot, -514f);

            footerStatusText = CreateLabel(contentRoot, "SISTEMA NEXUS // PRONTO PARA LIBERAÇÃO DE SAÍDA", 20, TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(0f, -544f), new Vector2(896f, 44f));
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
                    if (!isFocused || protocolFinished) return;
                    selectedIndex = idx;
                    RefreshRowHighlights();
                },
                idx =>
                {
                    if (!isFocused || protocolFinished) return;
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
            Image right = CreateBorderEdge("BorderRight", parent, color, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero);
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
