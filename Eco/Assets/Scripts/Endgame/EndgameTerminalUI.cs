using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Eco.Endgame
{
    public enum EndgameScreenMode
    {
        Offline,
        AccessAvailable,
        FinalRecords,
        ChoicePrompt,
        SyncProgress,
        SyncError,
        CorruptedFinalMessage,
        Blackout
    }

    public class EndgameChoiceClickTarget : MonoBehaviour, IPointerClickHandler
    {
        private Action onClick;
        public void Bind(Action callback) => onClick = callback;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                onClick?.Invoke();
            }
        }
    }

    /// <summary>
    /// Retro-industrial World Space Canvas mounted directly on the Pavimento 01 'final' terminal.
    /// Matches the SaveTerminalUI CRT aesthetic while supporting escalating glitches,
    /// corrupted records, the single-choice prompt, and the interrupted 76% sync error.
    /// </summary>
    public class EndgameTerminalUI : MonoBehaviour
    {
        private const float CanvasWidth = 960f;
        private const float CanvasHeight = 680f;

        private static readonly Color BgOffline = new Color(0.012f, 0.018f, 0.024f, 0.99f);
        private static readonly Color BgDark = new Color(0.043f, 0.082f, 0.114f, 0.98f);
        private static readonly Color BgError = new Color(0.105f, 0.032f, 0.038f, 0.98f);
        private static readonly Color BorderCyan = new Color(0.227f, 0.400f, 0.478f, 0.88f);
        private static readonly Color BorderError = new Color(0.86f, 0.24f, 0.24f, 0.92f);
        private static readonly Color DividerCol = new Color(0.165f, 0.310f, 0.376f, 0.85f);
        private static readonly Color CyanBright = new Color(0.298f, 0.788f, 0.910f, 1.00f);
        private static readonly Color AmberGold = new Color(0.851f, 0.643f, 0.255f, 1.00f);
        private static readonly Color ErrorRed = new Color(0.950f, 0.300f, 0.280f, 1.00f);
        private static readonly Color TextIce = new Color(0.886f, 0.945f, 0.973f, 1.00f);
        private static readonly Color TextBody = new Color(0.722f, 0.831f, 0.890f, 1.00f);
        private static readonly Color TextMuted = new Color(0.420f, 0.560f, 0.635f, 1.00f);
        private static readonly Color SelectFill = new Color(0.063f, 0.216f, 0.278f, 0.95f);
        private static readonly Color BarTrack = new Color(0.063f, 0.216f, 0.278f, 0.95f);
        private static readonly Color BarFillCyan = new Color(0.114f, 0.392f, 0.506f, 0.95f);
        private static readonly Color BarFillError = new Color(0.550f, 0.120f, 0.140f, 0.95f);

        private static Font sharedMonoFont;
        private static Texture2D sharedScanlineTex;

        private Canvas worldCanvas;
        private RectTransform contentRoot;
        private Image frameBackground;
        private Image[] frameBorders;
        private RawImage scanlineOverlay;
        private CanvasGroup glitchGroup;

        private GameObject accessPanel;
        private Text accessCursorText;

        private GameObject recordsPanel;
        private Text recordsLogText;
        private Text recordsFooterText;

        private GameObject choicePanel;
        private Text choiceHintText;

        private GameObject syncPanel;
        private Text syncTitleText;
        private Text syncIntegrityText;
        private RectTransform syncBarFillRect;
        private Image syncBarFillImg;
        private Text syncBarLabelText;
        private Text syncBottomLogText;

        private GameObject corruptedPanel;
        private Text corruptedText;

        private EndgameScreenMode currentMode = EndgameScreenMode.Offline;
        private float glitchIntensity;
        private float glitchTimer;
        private float blinkTimer;
        private bool distortCorruptedMessage;
        private Action onChoiceClicked;

        public EndgameScreenMode CurrentMode => currentMode;

        public void Initialize(
            Vector3 screenFrontWorldPos,
            Vector3 screenForwardWorld,
            Vector2 screenWorldSize,
            Action choiceClickCallback)
        {
            onChoiceClicked = choiceClickCallback;
            EnsureSharedResources();
            BuildWorldSpaceCanvas(screenFrontWorldPos, screenForwardWorld, screenWorldSize);
            BuildPanels();
            SetMode(EndgameScreenMode.Offline);
        }

        public void SetGlitchIntensity(float intensity)
        {
            glitchIntensity = Mathf.Clamp01(intensity);
            if (glitchIntensity <= 0.001f && contentRoot != null && glitchGroup != null)
            {
                contentRoot.anchoredPosition = Vector2.zero;
                glitchGroup.alpha = 1f;
            }
        }

        public void SetMode(EndgameScreenMode mode)
        {
            currentMode = mode;
            distortCorruptedMessage = (mode == EndgameScreenMode.CorruptedFinalMessage);

            bool showCanvas = (mode != EndgameScreenMode.Blackout);
            if (worldCanvas != null) worldCanvas.enabled = showCanvas;

            if (accessPanel != null) accessPanel.SetActive(mode == EndgameScreenMode.AccessAvailable);
            if (recordsPanel != null) recordsPanel.SetActive(mode == EndgameScreenMode.FinalRecords);
            if (choicePanel != null) choicePanel.SetActive(mode == EndgameScreenMode.ChoicePrompt);
            if (syncPanel != null) syncPanel.SetActive(mode == EndgameScreenMode.SyncProgress || mode == EndgameScreenMode.SyncError);
            if (corruptedPanel != null) corruptedPanel.SetActive(mode == EndgameScreenMode.CorruptedFinalMessage);

            if (frameBackground != null)
            {
                frameBackground.color = mode switch
                {
                    EndgameScreenMode.Offline => BgOffline,
                    EndgameScreenMode.SyncError => BgError,
                    EndgameScreenMode.CorruptedFinalMessage => BgError,
                    _ => BgDark
                };
            }

            Color borderColor = (mode == EndgameScreenMode.SyncError || mode == EndgameScreenMode.CorruptedFinalMessage)
                ? BorderError
                : (mode == EndgameScreenMode.Offline ? new Color(0.05f, 0.09f, 0.12f, 0.4f) : BorderCyan);

            if (frameBorders != null)
            {
                foreach (Image b in frameBorders)
                {
                    if (b != null) b.color = borderColor;
                }
            }

            if (scanlineOverlay != null)
            {
                scanlineOverlay.enabled = (mode != EndgameScreenMode.Offline && mode != EndgameScreenMode.Blackout);
            }
        }

        public void ClearRecordsText()
        {
            if (recordsLogText != null) recordsLogText.text = string.Empty;
        }

        public void AppendRecordLine(string line, bool highlightAmber = false)
        {
            if (recordsLogText == null) return;
            string formatted = highlightAmber ? $"<color=#D9A441>{line}</color>" : line;
            if (string.IsNullOrEmpty(recordsLogText.text))
                recordsLogText.text = formatted;
            else
                recordsLogText.text += "\n\n" + formatted;
        }

        public void SetRecordsFooter(string text)
        {
            if (recordsFooterText != null) recordsFooterText.text = text;
        }

        public void UpdateSyncProgress(int percent, string statusHeader, string barLabel, string bottomText, bool isError)
        {
            if (syncTitleText != null)
            {
                syncTitleText.color = isError ? ErrorRed : CyanBright;
                syncTitleText.text = $"<b>{statusHeader}</b>";
            }

            if (syncIntegrityText != null)
            {
                syncIntegrityText.color = isError ? AmberGold : TextBody;
                syncIntegrityText.text = $"INTEGRIDADE DO PROCESSO — {percent}%.";
            }

            if (syncBarFillRect != null)
            {
                syncBarFillRect.anchorMax = new Vector2(Mathf.Clamp01(percent / 100f), 1f);
            }

            if (syncBarFillImg != null)
            {
                syncBarFillImg.color = isError ? BarFillError : BarFillCyan;
            }

            if (syncBarLabelText != null)
            {
                syncBarLabelText.color = isError ? ErrorRed : CyanBright;
                syncBarLabelText.text = $"<b>{barLabel}</b>";
            }

            if (syncBottomLogText != null)
            {
                syncBottomLogText.color = isError ? ErrorRed : TextIce;
                syncBottomLogText.text = bottomText;
            }
        }

        public void SetCorruptedFinalMessage(string text)
        {
            if (corruptedText != null) corruptedText.text = text;
        }

        private void Update()
        {
            if (worldCanvas != null && worldCanvas.worldCamera == null && Camera.main != null)
            {
                worldCanvas.worldCamera = Camera.main;
            }

            if (scanlineOverlay != null && scanlineOverlay.enabled)
            {
                Rect uv = scanlineOverlay.uvRect;
                uv.y = (uv.y + Time.unscaledDeltaTime * (0.08f + glitchIntensity * 0.4f)) % 1f;
                scanlineOverlay.uvRect = uv;
            }

            if (currentMode == EndgameScreenMode.AccessAvailable && accessCursorText != null)
            {
                blinkTimer += Time.unscaledDeltaTime;
                bool show = ((int)(blinkTimer * 2f) % 2) == 0;
                accessCursorText.text = show
                    ? "> PRESSIONE [E] PARA INICIAR CONEXÃO CENTRAL_"
                    : "> PRESSIONE [E] PARA INICIAR CONEXÃO CENTRAL ";
            }

            if (currentMode == EndgameScreenMode.ChoicePrompt && choiceHintText != null)
            {
                blinkTimer += Time.unscaledDeltaTime;
                float a = 0.55f + 0.45f * Mathf.Sin(blinkTimer * 3.5f);
                choiceHintText.color = new Color(CyanBright.r, CyanBright.g, CyanBright.b, a);
            }

            if (glitchIntensity > 0.001f && contentRoot != null && glitchGroup != null)
            {
                glitchTimer -= Time.unscaledDeltaTime;
                if (glitchTimer <= 0f)
                {
                    if (UnityEngine.Random.value < (0.25f + glitchIntensity * 0.65f))
                    {
                        float maxX = Mathf.Lerp(2.5f, 16f, glitchIntensity);
                        float maxY = Mathf.Lerp(0.5f, 5f, glitchIntensity);
                        contentRoot.anchoredPosition = new Vector2(
                            UnityEngine.Random.Range(-maxX, maxX),
                            UnityEngine.Random.Range(-maxY, maxY));
                        glitchGroup.alpha = UnityEngine.Random.Range(1f - glitchIntensity * 0.45f, 1f);
                        glitchTimer = UnityEngine.Random.Range(0.025f, 0.075f);

                        if (distortCorruptedMessage && corruptedText != null)
                        {
                            corruptedText.text = GenerateCorruptedFragment();
                        }
                    }
                    else
                    {
                        contentRoot.anchoredPosition = Vector2.zero;
                        glitchGroup.alpha = 1f;
                        glitchTimer = UnityEngine.Random.Range(0.08f, 0.35f);
                    }
                }
            }
        }

        private static string GenerateCorruptedFragment()
        {
            string[] variants =
            {
                "REGISTRO RESIDUAL // 0x7F-ECO\n\nN█O É POSSÍV█L CONFIRMAR SE A ORIG█M\nAINDA EXIST█ OU SE APENAS O █████\nPERMANEC█ ATIVO.\n\n<color=#D9A441>SINAL PERDIDO // ▓▓▓░░░▓▓</color>",
                "REGISTRO RESIDUAL // 0x??-ECO\n\nNÃO É P█SSÍVEL CONF█RMAR SE A █████\nAINDA EX█STE OU SE APEN█S O ECO\nPERMANECE AT█VO.\n\n<color=#D9A441>SINAL PERDIDO // ░▓▓▓░▓▓▓</color>",
                "REGISTRO R█SIDUAL // ERR_ID\n\n█ ORIGEM NÃO F█I ENCONTRADA.\nMEMÓR█A REMANESCENTE: INDET█RMINADA.\nCONT█NUIDADE: [████]\n\n<color=#E84C4C>TRANSMISSÃO CORROMPIDA // ▓▓▓▓</color>"
            };
            return variants[UnityEngine.Random.Range(0, variants.Length)];
        }

        private static void EnsureSharedResources()
        {
            if (sharedMonoFont == null)
            {
                string[] preferred = { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console" };
                try { sharedMonoFont = Font.CreateDynamicFontFromOSFont(preferred, 24); }
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
                Color line = new Color(0f, 0.04f, 0.06f, 0.25f);
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
            GameObject canvasGo = new GameObject("EndgameScreenCanvas");
            canvasGo.transform.SetParent(transform, true);
            canvasGo.transform.position = screenFrontWorldPos + screenForwardWorld.normalized * 0.005f;
            canvasGo.transform.rotation = Quaternion.LookRotation(-screenForwardWorld.normalized, Vector3.up);

            worldCanvas = canvasGo.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = Camera.main;
            worldCanvas.sortingOrder = 6;
            canvasGo.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            float scaleX = (screenWorldSize.x * 0.96f) / CanvasWidth;
            float scaleY = (screenWorldSize.y * 0.96f) / CanvasHeight;
            Vector3 pScale = transform.lossyScale;
            canvasGo.transform.localScale = new Vector3(
                scaleX / (Mathf.Approximately(pScale.x, 0f) ? 1f : pScale.x),
                scaleY / (Mathf.Approximately(pScale.y, 0f) ? 1f : pScale.y),
                1f / (Mathf.Approximately(pScale.z, 0f) ? 1f : pScale.z));

            GameObject bgGo = CreateUIObject("FrameBackground", canvasRect);
            StretchFull(bgGo.GetComponent<RectTransform>());
            frameBackground = bgGo.AddComponent<Image>();
            frameBackground.color = BgOffline;

            frameBorders = AddBorderOutline(bgGo.GetComponent<RectTransform>(), BorderCyan, 2.5f);

            GameObject contentGo = CreateUIObject("ContentRoot", bgGo.GetComponent<RectTransform>());
            contentRoot = contentGo.GetComponent<RectTransform>();
            StretchFull(contentRoot, 28f, 24f, 28f, 24f);
            glitchGroup = contentGo.AddComponent<CanvasGroup>();

            GameObject scanGo = CreateUIObject("CRTScanlines", bgGo.GetComponent<RectTransform>());
            StretchFull(scanGo.GetComponent<RectTransform>());
            scanlineOverlay = scanGo.AddComponent<RawImage>();
            scanlineOverlay.texture = sharedScanlineTex;
            scanlineOverlay.uvRect = new Rect(0f, 0f, 1f, 85f);
            scanlineOverlay.raycastTarget = false;
        }

        private void BuildPanels()
        {
            // 1. Access Available Panel (Etapa 2)
            accessPanel = CreatePanel("AccessAvailablePanel");
            RectTransform rtAcc = accessPanel.GetComponent<RectTransform>();
            CreateHeader(rtAcc, "COMPLEXO PRIMÁRIO // PAVIMENTO 01", "NEXUS");
            CreateDivider(rtAcc, -48f);
            CreateLabel(rtAcc, "TERMINAL DE SINCRONIZAÇÃO CENTRAL", 20, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -72f), new Vector2(880f, 30f));
            CreateLabel(rtAcc, "<b>SINCRONIZAÇÃO CENTRAL — ACESSO DISPONÍVEL.</b>", 28, CyanBright, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(0f, -190f), new Vector2(880f, 110f));
            CreateDivider(rtAcc, -490f);
            accessCursorText = CreateLabel(rtAcc, "> PRESSIONE [E] PARA INICIAR CONEXÃO CENTRAL_", 21, TextIce, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(0f, -520f), new Vector2(880f, 44f));

            // 2. Final Records Panel (Etapa 3)
            recordsPanel = CreatePanel("FinalRecordsPanel");
            RectTransform rtRec = recordsPanel.GetComponent<RectTransform>();
            CreateHeader(rtRec, "TERMINAL DE SINCRONIZAÇÃO CENTRAL", "NEXUS");
            CreateDivider(rtRec, -48f);
            recordsLogText = CreateLabel(rtRec, string.Empty, 23, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -80f), new Vector2(880f, 420f));
            recordsLogText.lineSpacing = 1.25f;
            CreateDivider(rtRec, -520f);
            recordsFooterText = CreateLabel(rtRec, "SISTEMA DE MONITORAMENTO NEURAL // NEXUS", 17, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -544f), new Vector2(880f, 30f));

            // 3. Single Choice Panel (Etapa 4)
            choicePanel = CreatePanel("ChoicePromptPanel");
            RectTransform rtCh = choicePanel.GetComponent<RectTransform>();
            CreateHeader(rtCh, "TERMINAL DE SINCRONIZAÇÃO CENTRAL", "NEXUS");
            CreateDivider(rtCh, -48f);
            CreateLabel(rtCh, "COMANDO DISPONÍVEL:", 20, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -135f), new Vector2(880f, 32f));

            GameObject btnGo = CreateUIObject("SyncChoiceButton", rtCh);
            RectTransform btnRt = btnGo.GetComponent<RectTransform>();
            SetTopLeft(btnRt, new Vector2(0f, -195f), new Vector2(880f, 92f));
            Image btnBg = btnGo.AddComponent<Image>();
            btnBg.color = SelectFill;
            btnBg.raycastTarget = true;
            AddBorderOutline(btnRt, CyanBright, 2.5f);
            btnGo.AddComponent<EndgameChoiceClickTarget>().Bind(() => onChoiceClicked?.Invoke());

            GameObject btnTxtGo = CreateUIObject("Label", btnRt);
            StretchFull(btnTxtGo.GetComponent<RectTransform>(), 24f, 0f, 24f, 0f);
            Text btnTxt = btnTxtGo.AddComponent<Text>();
            ConfigureText(btnTxt, ">  [ SINCRONIZAR PROCESSO DE MEMÓRIA ]", 26, CyanBright, FontStyle.Bold, TextAnchor.MiddleCenter);
            btnTxt.raycastTarget = false;

            choiceHintText = CreateLabel(rtCh, "PRESSIONE [ E ] PARA EXECUTAR", 20, CyanBright, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, -330f), new Vector2(880f, 38f));

            CreateDivider(rtCh, -520f);
            CreateLabel(rtCh, "NENHUMA OUTRA OPERAÇÃO AUTORIZADA // NEXUS", 17, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -544f), new Vector2(880f, 30f));

            // 4. Sync Progress & Error Panel (Etapa 4)
            syncPanel = CreatePanel("SyncProgressPanel");
            RectTransform rtSync = syncPanel.GetComponent<RectTransform>();
            syncTitleText = CreateLabel(rtSync, "<b>SINCRONIZAÇÃO INICIADA.</b>", 29, CyanBright, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -28f), new Vector2(880f, 44f));
            syncIntegrityText = CreateLabel(rtSync, "INTEGRIDADE DO PROCESSO — 0%.", 23, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -104f), new Vector2(880f, 38f));

            GameObject barBgGo = CreateUIObject("SyncBarTrack", rtSync);
            RectTransform barBgRt = barBgGo.GetComponent<RectTransform>();
            SetTopLeft(barBgRt, new Vector2(0f, -184f), new Vector2(880f, 82f));
            Image barBgImg = barBgGo.AddComponent<Image>();
            barBgImg.color = BarTrack;
            AddBorderOutline(barBgRt, BorderCyan, 2f);

            GameObject barFillGo = CreateUIObject("SyncBarFill", barBgRt);
            syncBarFillRect = barFillGo.GetComponent<RectTransform>();
            syncBarFillRect.anchorMin = Vector2.zero;
            syncBarFillRect.anchorMax = new Vector2(0f, 1f);
            syncBarFillRect.offsetMin = new Vector2(2f, 2f);
            syncBarFillRect.offsetMax = new Vector2(-2f, -2f);
            syncBarFillImg = barFillGo.AddComponent<Image>();
            syncBarFillImg.color = BarFillCyan;

            GameObject barLblGo = CreateUIObject("SyncBarLabel", barBgRt);
            StretchFull(barLblGo.GetComponent<RectTransform>());
            syncBarLabelText = barLblGo.AddComponent<Text>();
            ConfigureText(syncBarLabelText, "<b>SINCRONIZANDO...</b>", 24, CyanBright, FontStyle.Bold, TextAnchor.MiddleCenter);

            syncBottomLogText = CreateLabel(rtSync, string.Empty, 25, TextIce, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -310f), new Vector2(880f, 160f));
            syncBottomLogText.lineSpacing = 1.3f;

            // 5. Corrupted Final Message Panel (End of Etapa 4)
            corruptedPanel = CreatePanel("CorruptedFinalMessagePanel");
            RectTransform rtCor = corruptedPanel.GetComponent<RectTransform>();
            CreateHeader(rtCor, "ERRO CRÍTICO // INTERFERÊNCIA", "NEXUS");
            CreateDivider(rtCor, -48f);
            corruptedText = CreateLabel(rtCor, GenerateCorruptedFragment(), 24, TextIce, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(0f, -120f), new Vector2(880f, 360f));
            corruptedText.lineSpacing = 1.3f;
        }

        private GameObject CreatePanel(string name)
        {
            GameObject go = CreateUIObject(name, contentRoot);
            StretchFull(go.GetComponent<RectTransform>());
            return go;
        }

        private void CreateHeader(RectTransform parent, string left, string right)
        {
            CreateLabel(parent, $"<b>{left}</b>", 23, CyanBright, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -4f), new Vector2(700f, 34f));
            CreateLabel(parent, right, 22, AmberGold, FontStyle.Bold, TextAnchor.UpperRight,
                new Vector2(700f, -4f), new Vector2(180f, 34f));
        }

        private void CreateDivider(RectTransform parent, float yPos)
        {
            GameObject go = CreateUIObject("Divider", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetTopLeft(rt, new Vector2(0f, yPos), new Vector2(880f, 2f));
            Image img = go.AddComponent<Image>();
            img.color = DividerCol;
            img.raycastTarget = false;
        }

        private Text CreateLabel(
            RectTransform parent,
            string content,
            int fontSize,
            Color color,
            FontStyle style,
            TextAnchor alignment,
            Vector2 topLeftPos,
            Vector2 size)
        {
            GameObject go = CreateUIObject("Label", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetTopLeft(rt, topLeftPos, size);
            Text txt = go.AddComponent<Text>();
            ConfigureText(txt, content, fontSize, color, style, alignment);
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
