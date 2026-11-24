using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Eco.Save
{
    /// <summary>
    /// Helper component attached to diegetic terminal menu items so mouse hover and click
    /// stay in sync with keyboard navigation.
    /// </summary>
    public class TerminalOptionButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            onHover?.Invoke(Index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                onClick?.Invoke(Index);
            }
        }
    }

    public enum TerminalScreenState
    {
        Standby,
        Booting,
        NexusMessage,
        MainMenu,
        Syncing,
        RecordsList,
        RecordReader,
        PoweredIdle
    }

    /// <summary>
    /// Builds and drives the retro-industrial World Space Canvas rendered directly onto
    /// each 3D save terminal's monitor screen, complete with CRT scanlines, subtle glitches,
    /// keyboard + mouse navigation, sync progress animation, NEXUS message (Save 3),
    /// and record viewer.
    /// </summary>
    public class SaveTerminalUI : MonoBehaviour
    {
        private const float CanvasWidth = 960f;
        private const float CanvasHeight = 680f;

        // Industrial NEXUS palette matching reference aesthetics
        private static readonly Color BgDark = new Color(0.043f, 0.082f, 0.114f, 0.98f);         // #0B151D
        private static readonly Color BgStandby = new Color(0.024f, 0.051f, 0.071f, 0.98f);      // #060D12
        private static readonly Color BorderDim = new Color(0.227f, 0.400f, 0.478f, 0.85f);      // #3A667A
        private static readonly Color DividerCol = new Color(0.165f, 0.310f, 0.376f, 0.85f);     // #2A4F60
        private static readonly Color CyanBright = new Color(0.298f, 0.788f, 0.910f, 1.00f);     // #4CC9E8
        private static readonly Color GreenSynced = new Color(0.361f, 0.882f, 0.722f, 1.00f);    // #5CE1B8
        private static readonly Color AmberGold = new Color(0.851f, 0.643f, 0.255f, 1.00f);      // #D9A441
        private static readonly Color TextIce = new Color(0.886f, 0.945f, 0.973f, 1.00f);        // #E2F1F8
        private static readonly Color TextBody = new Color(0.722f, 0.831f, 0.890f, 1.00f);       // #B8D4E3
        private static readonly Color TextMuted = new Color(0.420f, 0.560f, 0.635f, 1.00f);      // #6B8FA2
        private static readonly Color SelectFill = new Color(0.063f, 0.216f, 0.278f, 0.95f);     // #103747
        private static readonly Color UnselectFill = new Color(0.043f, 0.082f, 0.114f, 0.01f);   // transparent hit-target
        private static readonly Color BarTrack = new Color(0.063f, 0.216f, 0.278f, 0.95f);       // #103747
        private static readonly Color BarFill = new Color(0.114f, 0.392f, 0.506f, 0.95f);        // #1D6481

        private static Font sharedMonoFont;
        private static Texture2D sharedScanlineTex;

        private SaveTerminalProfile profile;
        private Canvas worldCanvas;
        private RectTransform contentRoot;
        private Image frameBackground;
        private RawImage scanlineOverlay;
        private CanvasGroup glitchGroup;

        // Screen panels
        private GameObject standbyPanel;
        private Text standbyStatusText;
        private Text standbyCursorText;

        private GameObject bootPanel;
        private Text bootLogText;

        private GameObject nexusPanel;
        private Text nexusLinesText;
        private Text nexusQuestionText;
        private Text nexusFooterText;
        private GameObject nexusContinueRow;

        private GameObject mainMenuPanel;
        private Text mainSyncStatusText;
        private readonly List<MenuRowVisual> mainMenuRows = new List<MenuRowVisual>();

        private GameObject syncPanel;
        private Text syncTitleText;
        private Text syncStep1Text;
        private Text syncIntegrityText;
        private RectTransform syncBarFillRect;
        private Text syncBarLabelText;
        private Text syncCompleteLine1;
        private Text syncCompleteLine2;
        private Text syncFooterText;

        private GameObject recordsListPanel;
        private RectTransform recordsListContainer;
        private readonly List<MenuRowVisual> recordRows = new List<MenuRowVisual>();

        private GameObject recordReaderPanel;
        private Text readerHeaderText;
        private Text readerMetaText;
        private Text readerBodyText;
        private Text readerFooterNoteText;
        private MenuRowVisual readerBackRow;

        private TerminalScreenState currentState = TerminalScreenState.Standby;
        private bool isFocused;
        private bool isSynced;
        private bool isInputLocked;
        private int selectedIndex;
        private float glitchTimer;
        private float cursorBlinkTimer;
        private Coroutine activeRoutine;

        private Action onSyncRequested;
        private Action onDisconnectRequested;

        public TerminalScreenState CurrentState => currentState;
        public bool IsFocused => isFocused;

        private class MenuRowVisual
        {
            public GameObject root;
            public Image background;
            public Image borderTop;
            public Image borderBottom;
            public Image borderLeft;
            public Image borderRight;
            public Text label;
            public string baseText;
            public Action onActivate;
        }

        public void Initialize(
            SaveTerminalProfile terminalProfile,
            Vector3 screenFrontWorldPos,
            Vector3 screenForwardWorld,
            Vector2 screenWorldSize,
            Action syncCallback,
            Action disconnectCallback)
        {
            profile = terminalProfile ?? SaveTerminalPresets.CreateForTerminalIndex(1);
            onSyncRequested = syncCallback;
            onDisconnectRequested = disconnectCallback;

            EnsureSharedResources();
            BuildWorldSpaceCanvas(screenFrontWorldPos, screenForwardWorld, screenWorldSize);
            BuildAllScreens();

            isSynced = EcoSaveManager.Instance != null && EcoSaveManager.Instance.IsTerminalSynced(profile.terminalId);
            SetState(isSynced ? TerminalScreenState.PoweredIdle : TerminalScreenState.Standby);
        }

        public void EnterTerminal(bool alreadySynced)
        {
            isSynced = alreadySynced;
            isFocused = true;
            RefreshMainMenuSyncLabel();

            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(BootSequenceRoutine());
        }

        public void ExitTerminalKeepPowered()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            isFocused = false;
            isInputLocked = false;
            SetState(TerminalScreenState.PoweredIdle);
        }

        public void TriggerSyncFromCode()
        {
            if (currentState == TerminalScreenState.MainMenu && !isInputLocked)
            {
                StartSyncSequence();
            }
        }

        public void OpenRecordsFromCode()
        {
            if (currentState == TerminalScreenState.MainMenu && !isInputLocked)
            {
                OpenRecordsList();
            }
        }

        public void OpenRecordByIndexFromCode(int index)
        {
            if (profile != null && profile.records != null && index >= 0 && index < profile.records.Count)
            {
                OpenRecordReader(profile.records[index]);
            }
        }

        private void Update()
        {
            if (worldCanvas != null && worldCanvas.worldCamera == null && Camera.main != null)
            {
                worldCanvas.worldCamera = Camera.main;
            }

            UpdateCrtAndGlitchEffects();
            UpdateStandbyBlink();

            if (!isFocused || isInputLocked) return;

            HandleKeyboardNavigation();
        }

        private void UpdateCrtAndGlitchEffects()
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
                bool intense = currentState == TerminalScreenState.Booting || currentState == TerminalScreenState.NexusMessage;
                float chance = intense ? 0.45f : 0.14f;
                if (UnityEngine.Random.value < chance)
                {
                    float jitterX = UnityEngine.Random.Range(-1f, 1f) * (intense ? 5.5f : 2.2f);
                    contentRoot.anchoredPosition = new Vector2(jitterX, 0f);
                    glitchGroup.alpha = UnityEngine.Random.Range(intense ? 0.84f : 0.93f, 1f);
                    glitchTimer = UnityEngine.Random.Range(0.03f, 0.08f);
                }
                else
                {
                    contentRoot.anchoredPosition = Vector2.zero;
                    glitchGroup.alpha = 1f;
                    glitchTimer = UnityEngine.Random.Range(intense ? 0.35f : 1.1f, intense ? 0.9f : 2.8f);
                }
            }
        }

        private void UpdateStandbyBlink()
        {
            if (standbyCursorText == null || !standbyPanel.activeSelf) return;

            cursorBlinkTimer += Time.unscaledDeltaTime;
            bool showCursor = ((int)(cursorBlinkTimer * 2f) % 2) == 0;
            standbyCursorText.text = showCursor
                ? "> AGUARDANDO CONEXÃO DO USUÁRIO_"
                : "> AGUARDANDO CONEXÃO DO USUÁRIO ";
        }

        private void HandleKeyboardNavigation()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            bool up = kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
            bool down = kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
            bool confirm = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            bool back = kb.backspaceKey.wasPressedThisFrame;

            switch (currentState)
            {
                case TerminalScreenState.NexusMessage:
                    if (confirm || kb.eKey.wasPressedThisFrame)
                    {
                        ProceedFromNexusMessage();
                    }
                    break;

                case TerminalScreenState.MainMenu:
                    if (up) MoveSelection(mainMenuRows, -1);
                    else if (down) MoveSelection(mainMenuRows, 1);
                    else if (confirm) ActivateSelection(mainMenuRows);
                    break;

                case TerminalScreenState.RecordsList:
                    if (up) MoveSelection(recordRows, -1);
                    else if (down) MoveSelection(recordRows, 1);
                    else if (confirm) ActivateSelection(recordRows);
                    else if (back) OpenMainMenu();
                    break;

                case TerminalScreenState.RecordReader:
                    if (confirm || back)
                    {
                        OpenRecordsList();
                    }
                    break;
            }
        }

        private void MoveSelection(List<MenuRowVisual> rows, int delta)
        {
            if (rows == null || rows.Count == 0) return;
            selectedIndex = (selectedIndex + delta + rows.Count) % rows.Count;
            RefreshRowHighlights(rows, selectedIndex);
        }

        private void ActivateSelection(List<MenuRowVisual> rows)
        {
            if (rows == null || rows.Count == 0) return;
            int idx = Mathf.Clamp(selectedIndex, 0, rows.Count - 1);
            rows[idx].onActivate?.Invoke();
        }

        private IEnumerator BootSequenceRoutine()
        {
            isInputLocked = true;
            SetState(TerminalScreenState.Booting);

            if (bootLogText != null)
            {
                bootLogText.text = "NEXUS OS v4.7 // INICIALIZANDO TERMINAL...";
                yield return new WaitForSecondsRealtime(0.18f);
                bootLogText.text += "\nESTABELECENDO LINK DE MEMÓRIA LOCAL...";
                yield return new WaitForSecondsRealtime(0.18f);
                bootLogText.text += $"\nSETOR: {profile.sectorTitle} [OK]";
                yield return new WaitForSecondsRealtime(0.16f);
            }

            isInputLocked = false;
            activeRoutine = null;

            // Save 3 always displays the NEXUS observation message upon access
            if (profile.hasNexusObservationMessage)
            {
                activeRoutine = StartCoroutine(NexusMessageRoutine());
            }
            else
            {
                OpenMainMenu();
            }
        }

        private IEnumerator NexusMessageRoutine()
        {
            isInputLocked = true;
            SetState(TerminalScreenState.NexusMessage);

            nexusLinesText.text = string.Empty;
            nexusQuestionText.text = string.Empty;
            nexusFooterText.text = string.Empty;
            if (nexusContinueRow != null) nexusContinueRow.SetActive(false);

            string[] lines =
            {
                "<i>REGISTRO DE USUÁRIO IDENTIFICADO.</i>",
                "\n\n<i>IDENTIDADE: ECO.</i>",
                "\n\n<i>INTEGRIDADE DO REGISTRO: INDETERMINADA.</i>"
            };

            yield return new WaitForSecondsRealtime(0.25f);
            foreach (string line in lines)
            {
                nexusLinesText.text += line;
                yield return new WaitForSecondsRealtime(0.45f);
            }

            yield return new WaitForSecondsRealtime(0.35f);
            nexusQuestionText.text = $"<b>{profile.nexusQuestion}</b>";
            nexusFooterText.text = profile.nexusFooter;

            yield return new WaitForSecondsRealtime(0.30f);
            if (nexusContinueRow != null) nexusContinueRow.SetActive(true);
            isInputLocked = false;
            activeRoutine = null;
        }

        private void ProceedFromNexusMessage()
        {
            if (isInputLocked) return;
            OpenMainMenu();
        }

        private void OpenMainMenu()
        {
            SetState(TerminalScreenState.MainMenu);
            RefreshMainMenuSyncLabel();
            selectedIndex = 0;
            RefreshRowHighlights(mainMenuRows, selectedIndex);
        }

        private void StartSyncSequence()
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(SyncRoutine());
        }

        private IEnumerator SyncRoutine()
        {
            isInputLocked = true;
            SetState(TerminalScreenState.Syncing);

            syncTitleText.text = "<b>INICIANDO SINCRONIZAÇÃO...</b>";
            syncStep1Text.text = "LOCALIZANDO REGISTROS DE MEMÓRIA...";
            syncIntegrityText.text = "INTEGRIDADE DO PROCESSO: 0%";
            syncBarLabelText.text = "<b>SINCRONIZANDO...</b>";
            syncCompleteLine1.text = string.Empty;
            syncCompleteLine2.text = string.Empty;
            syncFooterText.text = "SISTEMA NEXUS // TRANSFERÊNCIA EM ANDAMENTO";
            SetProgressBar(0f);

            yield return new WaitForSecondsRealtime(0.35f);
            syncStep1Text.text = "REGISTROS DE MEMÓRIA IDENTIFICADOS.";

            float duration = 2.4f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smooth stepped industrial feel
                float eased = Mathf.SmoothStep(0f, 1f, t);
                int pct = Mathf.Clamp(Mathf.RoundToInt(eased * 100f), 0, 100);
                syncIntegrityText.text = $"INTEGRIDADE DO PROCESSO: {pct}%";
                SetProgressBar(eased);
                yield return null;
            }

            SetProgressBar(1f);
            syncIntegrityText.text = "INTEGRIDADE DO PROCESSO: 100%";
            syncBarLabelText.text = "<b>PROCESSO CONCLUÍDO</b>";

            // Perform real atomic save to disk
            onSyncRequested?.Invoke();
            isSynced = true;

            yield return new WaitForSecondsRealtime(0.20f);
            syncCompleteLine1.text = "<b>MEMÓRIA LOCAL ATUALIZADA.</b>";
            yield return new WaitForSecondsRealtime(0.20f);
            syncCompleteLine2.text = "<b>REGISTRO ARMAZENADO.</b>";
            syncFooterText.text = "SISTEMA NEXUS // SINCRONIZAÇÃO CONCLUÍDA";

            yield return new WaitForSecondsRealtime(1.35f);

            isInputLocked = false;
            activeRoutine = null;
            OpenMainMenu();
        }

        private void OpenRecordsList()
        {
            SetState(TerminalScreenState.RecordsList);
            selectedIndex = 0;
            RefreshRowHighlights(recordRows, selectedIndex);
        }

        private void OpenRecordReader(TerminalRecordEntry entry)
        {
            if (entry == null) return;

            readerHeaderText.text = $"<b>{entry.codeHeader}</b>";
            readerMetaText.text = entry.metadataLines;
            readerBodyText.text = entry.bodyText;
            readerFooterNoteText.text = $"<i>{entry.footerNote}</i>";

            SetState(TerminalScreenState.RecordReader);
            RefreshRowHighlights(new List<MenuRowVisual> { readerBackRow }, 0);
        }

        private void SetState(TerminalScreenState newState)
        {
            currentState = newState;

            if (standbyPanel != null) standbyPanel.SetActive(newState == TerminalScreenState.Standby || newState == TerminalScreenState.PoweredIdle);
            if (bootPanel != null) bootPanel.SetActive(newState == TerminalScreenState.Booting);
            if (nexusPanel != null) nexusPanel.SetActive(newState == TerminalScreenState.NexusMessage);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(newState == TerminalScreenState.MainMenu);
            if (syncPanel != null) syncPanel.SetActive(newState == TerminalScreenState.Syncing);
            if (recordsListPanel != null) recordsListPanel.SetActive(newState == TerminalScreenState.RecordsList);
            if (recordReaderPanel != null) recordReaderPanel.SetActive(newState == TerminalScreenState.RecordReader);

            if (frameBackground != null)
            {
                frameBackground.color = (newState == TerminalScreenState.Standby) ? BgStandby : BgDark;
            }

            if (standbyStatusText != null)
            {
                if (newState == TerminalScreenState.PoweredIdle)
                {
                    string syncLine = isSynced
                        ? "<color=#5CE1B8>REGISTRO LOCAL: SINCRONIZADO</color>"
                        : "REGISTRO LOCAL: NÃO SINCRONIZADO";
                    standbyStatusText.text =
                        $"<b>{profile.sectorTitle}</b>\n\n" +
                        $"ESTADO DO SISTEMA: {profile.systemStateLabel}\n" +
                        $"INTEGRIDADE DO TERMINAL: {profile.terminalIntegrityPercent}%\n" +
                        $"{syncLine}\n\n" +
                        "<color=#6B8FA2>CONEXÃO EM ESPERA — TERMINAL ATIVO</color>";
                }
                else if (newState == TerminalScreenState.Standby)
                {
                    standbyStatusText.text =
                        $"<b>{profile.sectorTitle}</b>\n\n" +
                        "ESTADO: MODO STANDBY\n" +
                        "REDE NEURAL NEXUS: CONECTADA";
                }
            }
        }

        private void RefreshMainMenuSyncLabel()
        {
            if (mainSyncStatusText == null) return;
            mainSyncStatusText.text = isSynced
                ? "REGISTRO LOCAL: <color=#5CE1B8><b>SINCRONIZADO</b></color>"
                : "REGISTRO LOCAL: NÃO SINCRONIZADO";
        }

        private void SetProgressBar(float normalized)
        {
            if (syncBarFillRect == null) return;
            syncBarFillRect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
        }

        // =========================================================================
        // Procedural World Space Canvas Construction
        // =========================================================================

        private static void EnsureSharedResources()
        {
            if (sharedMonoFont == null)
            {
                string[] preferredFonts = { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console" };
                try
                {
                    sharedMonoFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 24);
                }
                catch
                {
                    sharedMonoFont = null;
                }

                if (sharedMonoFont == null)
                {
                    sharedMonoFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
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
            GameObject canvasGo = new GameObject("DiegeticScreenCanvas");
            canvasGo.transform.SetParent(transform, true);

            // Place 5mm in front of the 3D monitor screen face so there is zero Z-fighting.
            // Orient canvas +Z into the monitor (-screenForwardWorld) so its readable front faces the player (+screenForwardWorld).
            canvasGo.transform.position = screenFrontWorldPos + screenForwardWorld.normalized * 0.005f;
            canvasGo.transform.rotation = Quaternion.LookRotation(-screenForwardWorld.normalized, Vector3.up);

            worldCanvas = canvasGo.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = Camera.main;
            worldCanvas.sortingOrder = 5;

            canvasGo.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            // Compute exact local scale to match the 3D screen world size (e.g. 0.78m x 0.55m)
            float scaleX = (screenWorldSize.x * 0.96f) / CanvasWidth;
            float scaleY = (screenWorldSize.y * 0.96f) / CanvasHeight;
            canvasGo.transform.localScale = Vector3.one;
            Vector3 parentScale = transform.lossyScale;
            canvasGo.transform.localScale = new Vector3(
                scaleX / (Mathf.Approximately(parentScale.x, 0f) ? 1f : parentScale.x),
                scaleY / (Mathf.Approximately(parentScale.y, 0f) ? 1f : parentScale.y),
                1f / (Mathf.Approximately(parentScale.z, 0f) ? 1f : parentScale.z));

            // Outer frame background
            GameObject bgGo = CreateUIObject("FrameBackground", canvasRect);
            StretchFull(bgGo.GetComponent<RectTransform>());
            frameBackground = bgGo.AddComponent<Image>();
            frameBackground.color = BgDark;

            // Outer industrial border
            AddBorderOutline(bgGo.GetComponent<RectTransform>(), BorderDim, 2.5f);

            // Glitchable content root
            GameObject contentGo = CreateUIObject("ContentRoot", bgGo.GetComponent<RectTransform>());
            contentRoot = contentGo.GetComponent<RectTransform>();
            StretchFull(contentRoot, 26f, 22f, 26f, 22f);
            glitchGroup = contentGo.AddComponent<CanvasGroup>();

            // Scanline overlay on top of everything (non-raycast)
            GameObject scanGo = CreateUIObject("CRTScanlines", bgGo.GetComponent<RectTransform>());
            StretchFull(scanGo.GetComponent<RectTransform>());
            scanlineOverlay = scanGo.AddComponent<RawImage>();
            scanlineOverlay.texture = sharedScanlineTex;
            scanlineOverlay.uvRect = new Rect(0f, 0f, 1f, 85f);
            scanlineOverlay.raycastTarget = false;
        }

        private void BuildAllScreens()
        {
            BuildStandbyPanel();
            BuildBootPanel();
            BuildNexusPanel();
            BuildMainMenuPanel();
            BuildSyncPanel();
            BuildRecordsListPanel();
            BuildRecordReaderPanel();
        }

        private void BuildStandbyPanel()
        {
            standbyPanel = CreatePanel("StandbyPanel");
            RectTransform rt = standbyPanel.GetComponent<RectTransform>();

            CreateTopHeaderBar(rt, profile.complexTitle, profile.systemTag);
            CreateDivider(rt, -48f);

            CreateLabel(rt, "TERMINAL DE SINCRONIZAÇÃO", 20, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -68f), new Vector2(860f, 30f));

            standbyStatusText = CreateLabel(rt, string.Empty, 22, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -120f), new Vector2(880f, 340f));

            CreateDivider(rt, -515f);

            standbyCursorText = CreateLabel(rt, "> AGUARDANDO CONEXÃO DO USUÁRIO_", 22, CyanBright, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(0f, -535f), new Vector2(880f, 40f));

            CreateLabel(rt, profile.footerText, 16, TextMuted, FontStyle.Normal, TextAnchor.LowerLeft,
                new Vector2(0f, -592f), new Vector2(880f, 28f));
        }

        private void BuildBootPanel()
        {
            bootPanel = CreatePanel("BootPanel");
            RectTransform rt = bootPanel.GetComponent<RectTransform>();

            CreateTopHeaderBar(rt, profile.complexTitle, profile.systemTag);
            CreateDivider(rt, -48f);

            bootLogText = CreateLabel(rt, "INICIALIZANDO...", 22, CyanBright, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -90f), new Vector2(880f, 440f));
        }

        private void BuildNexusPanel()
        {
            nexusPanel = CreatePanel("NexusMessagePanel");
            RectTransform rt = nexusPanel.GetComponent<RectTransform>();

            CreateLabel(rt, $"<b>{profile.nexusHeader}</b>", 28, CyanBright, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -6f), new Vector2(880f, 40f));
            CreateDivider(rt, -56f);

            nexusLinesText = CreateLabel(rt, string.Empty, 26, TextBody, FontStyle.Italic, TextAnchor.UpperLeft,
                new Vector2(0f, -96f), new Vector2(880f, 250f));
            nexusLinesText.lineSpacing = 1.25f;

            CreateDivider(rt, -365f);

            nexusQuestionText = CreateLabel(rt, string.Empty, 27, AmberGold, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -396f), new Vector2(880f, 64f));

            nexusFooterText = CreateLabel(rt, string.Empty, 18, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -470f), new Vector2(880f, 32f));

            MenuRowVisual contRow = CreateMenuRow(rt, "CONTINUAR ACESSO AO TERMINAL", new Vector2(0f, -540f), 880f, 54f, 0,
                _ => { },
                _ => ProceedFromNexusMessage());
            RefreshRowHighlights(new List<MenuRowVisual> { contRow }, 0);
            nexusContinueRow = contRow.root;
        }

        private void BuildMainMenuPanel()
        {
            mainMenuPanel = CreatePanel("MainMenuPanel");
            RectTransform rt = mainMenuPanel.GetComponent<RectTransform>();

            CreateTopHeaderBar(rt, profile.complexTitle, profile.systemTag);
            CreateDivider(rt, -46f);

            CreateLabel(rt, profile.terminalSubtitle, 19, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -64f), new Vector2(880f, 28f));

            CreateLabel(rt, $"<b>{profile.sectorTitle}</b>", 30, TextIce, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -98f), new Vector2(880f, 40f));

            CreateLabel(rt, $"<b>{profile.registrySubtitle}</b>", 22, CyanBright, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -142f), new Vector2(880f, 32f));

            CreateDivider(rt, -184f);

            CreateLabel(rt, $"ESTADO DO SISTEMA: {profile.systemStateLabel}", 20, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -200f), new Vector2(880f, 28f));
            CreateLabel(rt, $"INTEGRIDADE DO TERMINAL: {profile.terminalIntegrityPercent}%", 20, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -234f), new Vector2(880f, 28f));
            CreateLabel(rt, $"USUÁRIO: {profile.userLabel}", 20, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -268f), new Vector2(880f, 28f));
            mainSyncStatusText = CreateLabel(rt, "REGISTRO LOCAL: NÃO SINCRONIZADO", 20, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -302f), new Vector2(880f, 28f));

            CreateDivider(rt, -344f);

            mainMenuRows.Clear();
            string[] labels =
            {
                "SINCRONIZAR PROCESSO DE MEMÓRIA",
                "CONSULTAR REGISTROS",
                "ENCERRAR CONEXÃO"
            };
            Action[] actions =
            {
                StartSyncSequence,
                OpenRecordsList,
                () => onDisconnectRequested?.Invoke()
            };

            for (int i = 0; i < labels.Length; i++)
            {
                float y = -366f - (i * 62f);
                int capturedIdx = i;
                MenuRowVisual row = CreateMenuRow(
                    rt,
                    labels[i],
                    new Vector2(0f, y),
                    880f,
                    54f,
                    capturedIdx,
                    idx =>
                    {
                        if (isInputLocked) return;
                        selectedIndex = idx;
                        RefreshRowHighlights(mainMenuRows, selectedIndex);
                    },
                    idx =>
                    {
                        if (isInputLocked) return;
                        selectedIndex = idx;
                        RefreshRowHighlights(mainMenuRows, selectedIndex);
                        actions[idx]?.Invoke();
                    });
                row.onActivate = actions[i];
                mainMenuRows.Add(row);
            }

            CreateDivider(rt, -566f);
            CreateLabel(rt, profile.footerText, 16, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -584f), new Vector2(880f, 28f));
        }

        private void BuildSyncPanel()
        {
            syncPanel = CreatePanel("SyncPanel");
            RectTransform rt = syncPanel.GetComponent<RectTransform>();

            syncTitleText = CreateLabel(rt, "<b>INICIANDO SINCRONIZAÇÃO...</b>", 30, CyanBright, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -24f), new Vector2(880f, 44f));

            syncStep1Text = CreateLabel(rt, "REGISTROS DE MEMÓRIA IDENTIFICADOS.", 22, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -94f), new Vector2(880f, 34f));

            syncIntegrityText = CreateLabel(rt, "INTEGRIDADE DO PROCESSO: 0%", 22, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -142f), new Vector2(880f, 34f));

            // Progress bar container
            GameObject barBgGo = CreateUIObject("SyncBarTrack", rt);
            RectTransform barBgRt = barBgGo.GetComponent<RectTransform>();
            SetTopLeft(barBgRt, new Vector2(0f, -206f), new Vector2(880f, 78f));
            Image barBgImg = barBgGo.AddComponent<Image>();
            barBgImg.color = BarTrack;
            AddBorderOutline(barBgRt, BorderDim, 2f);

            GameObject barFillGo = CreateUIObject("SyncBarFill", barBgRt);
            syncBarFillRect = barFillGo.GetComponent<RectTransform>();
            syncBarFillRect.anchorMin = Vector2.zero;
            syncBarFillRect.anchorMax = new Vector2(0f, 1f);
            syncBarFillRect.offsetMin = new Vector2(2f, 2f);
            syncBarFillRect.offsetMax = new Vector2(-2f, -2f);
            Image barFillImg = barFillGo.AddComponent<Image>();
            barFillImg.color = BarFill;

            GameObject barLblGo = CreateUIObject("SyncBarLabel", barBgRt);
            StretchFull(barLblGo.GetComponent<RectTransform>());
            syncBarLabelText = barLblGo.AddComponent<Text>();
            ConfigureText(syncBarLabelText, "<b>SINCRONIZANDO...</b>", 25, CyanBright, FontStyle.Bold, TextAnchor.MiddleCenter);

            syncCompleteLine1 = CreateLabel(rt, string.Empty, 25, TextIce, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -326f), new Vector2(880f, 38f));

            syncCompleteLine2 = CreateLabel(rt, string.Empty, 25, TextIce, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -374f), new Vector2(880f, 38f));

            CreateDivider(rt, -512f);

            syncFooterText = CreateLabel(rt, "SISTEMA NEXUS // TRANSFERÊNCIA EM ANDAMENTO", 18, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -536f), new Vector2(880f, 30f));
        }

        private void BuildRecordsListPanel()
        {
            recordsListPanel = CreatePanel("RecordsListPanel");
            RectTransform rt = recordsListPanel.GetComponent<RectTransform>();

            CreateTopHeaderBar(rt, $"REGISTROS LOCAIS // {profile.floorLabel}", profile.systemTag);
            CreateDivider(rt, -46f);

            CreateLabel(rt, "SELECIONE UM DOCUMENTO PARA LEITURA:", 19, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -66f), new Vector2(880f, 30f));

            GameObject listHolder = CreateUIObject("RecordsContainer", rt);
            recordsListContainer = listHolder.GetComponent<RectTransform>();
            SetTopLeft(recordsListContainer, new Vector2(0f, -112f), new Vector2(880f, 440f));

            recordRows.Clear();
            int count = profile.records != null ? profile.records.Count : 0;
            for (int i = 0; i < count; i++)
            {
                TerminalRecordEntry entry = profile.records[i];
                string tag = SaveTerminalPresets.GetCategoryTag(entry.category);
                string rowLabel = $"{tag} {entry.listTitle}";
                int capturedIdx = i;

                MenuRowVisual row = CreateMenuRow(
                    recordsListContainer,
                    rowLabel,
                    new Vector2(0f, -(i * 74f)),
                    880f,
                    62f,
                    capturedIdx,
                    idx =>
                    {
                        selectedIndex = idx;
                        RefreshRowHighlights(recordRows, selectedIndex);
                    },
                    idx =>
                    {
                        selectedIndex = idx;
                        RefreshRowHighlights(recordRows, selectedIndex);
                        OpenRecordReader(profile.records[idx]);
                    });
                row.onActivate = () => OpenRecordReader(entry);
                recordRows.Add(row);
            }

            // Back button at bottom of list
            int backIdx = recordRows.Count;
            MenuRowVisual backRow = CreateMenuRow(
                recordsListContainer,
                "VOLTAR AO MENU PRINCIPAL",
                new Vector2(0f, -350f),
                880f,
                54f,
                backIdx,
                idx =>
                {
                    selectedIndex = idx;
                    RefreshRowHighlights(recordRows, selectedIndex);
                },
                idx => OpenMainMenu());
            backRow.onActivate = OpenMainMenu;
            recordRows.Add(backRow);

            CreateDivider(rt, -566f);
            CreateLabel(rt, "USE SETAS/W-S + ENTER OU CLIQUE COM O MOUSE // BACKSPACE PARA VOLTAR", 15, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -584f), new Vector2(880f, 28f));
        }

        private void BuildRecordReaderPanel()
        {
            recordReaderPanel = CreatePanel("RecordReaderPanel");
            RectTransform rt = recordReaderPanel.GetComponent<RectTransform>();

            readerHeaderText = CreateLabel(rt, "<b>LOG DE FALHA</b>", 25, CyanBright, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -4f), new Vector2(880f, 36f));
            CreateDivider(rt, -46f);

            readerMetaText = CreateLabel(rt, string.Empty, 18, TextBody, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -62f), new Vector2(880f, 52f));
            readerMetaText.lineSpacing = 1.15f;

            readerBodyText = CreateLabel(rt, string.Empty, 19, TextBody, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -124f), new Vector2(880f, 332f));
            readerBodyText.lineSpacing = 1.22f;

            CreateDivider(rt, -468f);

            readerFooterNoteText = CreateLabel(rt, string.Empty, 19, AmberGold, FontStyle.Italic, TextAnchor.UpperLeft,
                new Vector2(0f, -482f), new Vector2(880f, 56f));
            readerFooterNoteText.lineSpacing = 1.15f;

            readerBackRow = CreateMenuRow(
                rt,
                "VOLTAR AOS REGISTROS",
                new Vector2(0f, -556f),
                880f,
                50f,
                0,
                _ => { },
                _ => OpenRecordsList());
            readerBackRow.onActivate = OpenRecordsList;
        }

        // =========================================================================
        // UI Primitives
        // =========================================================================

        private GameObject CreatePanel(string name)
        {
            GameObject go = CreateUIObject(name, contentRoot);
            StretchFull(go.GetComponent<RectTransform>());
            return go;
        }

        private void CreateTopHeaderBar(RectTransform parent, string leftText, string rightText)
        {
            CreateLabel(parent, $"<b>{leftText}</b>", 24, CyanBright, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(0f, -4f), new Vector2(680f, 34f));
            CreateLabel(parent, rightText, 22, AmberGold, FontStyle.Bold, TextAnchor.UpperRight,
                new Vector2(680f, -4f), new Vector2(200f, 34f));
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

        private MenuRowVisual CreateMenuRow(
            RectTransform parent,
            string text,
            Vector2 topLeftPos,
            float width,
            float height,
            int index,
            Action<int> onHover,
            Action<int> onClick)
        {
            GameObject rowGo = CreateUIObject($"Option_{index}", parent);
            RectTransform rt = rowGo.GetComponent<RectTransform>();
            SetTopLeft(rt, topLeftPos, new Vector2(width, height));

            Image bg = rowGo.AddComponent<Image>();
            bg.color = UnselectFill;
            bg.raycastTarget = true;

            TerminalOptionButton btn = rowGo.AddComponent<TerminalOptionButton>();
            btn.Bind(index, onHover, onClick);

            Image[] borders = AddBorderOutline(rt, CyanBright, 2f);

            GameObject lblGo = CreateUIObject("Text", rt);
            RectTransform lblRt = lblGo.GetComponent<RectTransform>();
            StretchFull(lblRt, 20f, 0f, 16f, 0f);
            Text lbl = lblGo.AddComponent<Text>();
            ConfigureText(lbl, text, 21, TextBody, FontStyle.Normal, TextAnchor.MiddleLeft);
            lbl.raycastTarget = false;

            return new MenuRowVisual
            {
                root = rowGo,
                background = bg,
                borderTop = borders[0],
                borderBottom = borders[1],
                borderLeft = borders[2],
                borderRight = borders[3],
                label = lbl,
                baseText = text
            };
        }

        private void RefreshRowHighlights(List<MenuRowVisual> rows, int activeIdx)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                MenuRowVisual r = rows[i];
                bool selected = i == activeIdx;
                r.background.color = selected ? SelectFill : UnselectFill;
                r.borderTop.enabled = selected;
                r.borderBottom.enabled = selected;
                r.borderLeft.enabled = selected;
                r.borderRight.enabled = selected;
                r.label.color = selected ? CyanBright : TextBody;
                r.label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                r.label.text = selected ? $"> {r.baseText}" : $"  {r.baseText}";
            }
        }

        private static Image[] AddBorderOutline(RectTransform parent, Color color, float thickness)
        {
            Image top = CreateBorderEdge("BorderTop", parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
            Image bottom = CreateBorderEdge("BorderBottom", parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
            Image left = CreateBorderEdge("BorderLeft", parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
            Image right = CreateBorderEdge("BorderRight", parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero);
            return new[] { top, bottom, left, right };
        }

        private static Image CreateBorderEdge(
            string name,
            RectTransform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
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
