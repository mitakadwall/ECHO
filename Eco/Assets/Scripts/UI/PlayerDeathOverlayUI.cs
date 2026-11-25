using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Eco.Combat;
using Eco.Player;
using Eco.Save;

namespace Eco.UI
{
    /// <summary>
    /// Diegetic NEXUS death screen ("FALHA DE INTEGRIDADE") displayed when the Player's Health reaches 0.
    /// Allows restoring the last saved record or quitting the application via mouse or keyboard.
    /// </summary>
    public class PlayerDeathOverlayUI : MonoBehaviour
    {
        [Header("Behavior")]
        [SerializeField] private float fadeInDuration = 0.35f;
        [SerializeField] private string gameplaySceneName = "Gameplay";

        private static readonly Color BackdropColor = new Color(0.01f, 0.015f, 0.02f, 0.90f);
        private static readonly Color PanelFillColor = new Color(0.027f, 0.043f, 0.055f, 0.98f);
        private static readonly Color BorderColor = new Color(0.24f, 0.35f, 0.42f, 0.85f);
        private static readonly Color DividerColor = new Color(0.22f, 0.32f, 0.38f, 0.75f);
        private static readonly Color AlertRed = new Color(0.85f, 0.27f, 0.27f, 1f);
        private static readonly Color TextWhite = new Color(0.95f, 0.97f, 0.99f, 1f);
        private static readonly Color TextLight = new Color(0.76f, 0.82f, 0.86f, 1f);
        private static readonly Color CyanSelected = new Color(0.29f, 0.80f, 0.93f, 1f);

        private static Font sharedFont;
        private static Sprite roundedBorderSprite;

        private Canvas overlayCanvas;
        private CanvasGroup rootGroup;
        private Text restoreLabel;
        private Text quitLabel;

        private Health playerHealth;
        private PlayerController playerController;
        private PlayerInteractor playerInteractor;

        private bool isShowing;
        private int selectedIndex; // 0 = Restore Last Record, 1 = Quit System

        private void Awake()
        {
            EnsureFont();
            EnsureRoundedSprite();
            BuildUI();
            HideImmediate();
        }

        private void Start()
        {
            BindToPlayer();
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDied -= HandlePlayerDied;
            }
            if (isShowing)
            {
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (!isShowing) return;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
                {
                    SetSelectedIndex(0);
                }
                else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
                {
                    SetSelectedIndex(1);
                }
                else if (Keyboard.current.enterKey.wasPressedThisFrame ||
                         Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                         Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    ExecuteSelectedOption();
                }
            }
        }

        private void BindToPlayer()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerObj = pc.gameObject;
            }

            if (playerObj == null) return;

            playerController = playerObj.GetComponent<PlayerController>();
            playerInteractor = playerObj.GetComponent<PlayerInteractor>();
            playerHealth = playerObj.GetComponent<Health>();

            if (playerHealth != null)
            {
                playerHealth.OnDied -= HandlePlayerDied;
                playerHealth.OnDied += HandlePlayerDied;
            }
        }

        private void HandlePlayerDied()
        {
            if (isShowing) return;
            ShowDeathScreen();
        }

        public void ShowDeathScreen()
        {
            isShowing = true;
            selectedIndex = 0;
            RefreshOptionVisuals();

            if (playerController != null)
            {
                playerController.BeginMovementOverride();
            }
            if (playerInteractor != null)
            {
                playerInteractor.IsInteractionBlocked = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;

            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = true;
            }

            StopAllCoroutines();
            StartCoroutine(FadeInRoutine());
        }

        private void HideImmediate()
        {
            isShowing = false;
            if (overlayCanvas != null) overlayCanvas.enabled = false;
            if (rootGroup != null) rootGroup.alpha = 0f;
        }

        private IEnumerator FadeInRoutine()
        {
            if (rootGroup == null) yield break;

            float elapsed = 0f;
            float dur = Mathf.Max(0.05f, fadeInDuration);
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                rootGroup.alpha = Mathf.Clamp01(elapsed / dur);
                yield return null;
            }
            rootGroup.alpha = 1f;
        }

        private void SetSelectedIndex(int index)
        {
            selectedIndex = Mathf.Clamp(index, 0, 1);
            RefreshOptionVisuals();
        }

        private void RefreshOptionVisuals()
        {
            if (restoreLabel != null)
            {
                bool active = selectedIndex == 0;
                restoreLabel.text = active ? "> RESTAURAR ÚLTIMO REGISTRO" : "RESTAURAR ÚLTIMO REGISTRO";
                restoreLabel.color = active ? CyanSelected : TextLight;
                restoreLabel.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            }

            if (quitLabel != null)
            {
                bool active = selectedIndex == 1;
                quitLabel.text = active ? "> ENCERRAR SISTEMA" : "ENCERRAR SISTEMA";
                quitLabel.color = active ? CyanSelected : TextLight;
                quitLabel.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void ExecuteSelectedOption()
        {
            if (selectedIndex == 0)
            {
                RestoreLastRecord();
            }
            else
            {
                QuitSystem();
            }
        }

        private void RestoreLastRecord()
        {
            Time.timeScale = 1f;
            isShowing = false;

            bool hasSave = EcoSaveManager.HasSavedGameOnDisk();
            EcoSaveManager.PendingLoadOnNextGameplay = hasSave;

            string targetScene = !string.IsNullOrEmpty(gameplaySceneName)
                ? gameplaySceneName
                : SceneManager.GetActiveScene().name;

            SceneManager.LoadScene(targetScene);
        }

        private void QuitSystem()
        {
            Time.timeScale = 1f;
            isShowing = false;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject("PlayerDeathOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);

            overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 500;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            rootGroup = canvasGo.AddComponent<CanvasGroup>();
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;

            // Full-screen dark backdrop
            GameObject backdropGo = CreateUIObject("Backdrop", canvasGo.transform);
            StretchFull(backdropGo.GetComponent<RectTransform>());
            Image backdropImg = backdropGo.AddComponent<Image>();
            backdropImg.color = BackdropColor;

            // Main rounded border frame
            GameObject frameGo = CreateUIObject("DeathTerminalFrame", canvasGo.transform);
            RectTransform frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.5f, 0.5f);
            frameRt.anchorMax = new Vector2(0.5f, 0.5f);
            frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.sizeDelta = new Vector2(1140f, 540f);

            Image frameBorder = frameGo.AddComponent<Image>();
            frameBorder.sprite = roundedBorderSprite;
            frameBorder.type = Image.Type.Sliced;
            frameBorder.color = BorderColor;

            // Inner dark fill (2px inset for crisp thin border)
            GameObject innerGo = CreateUIObject("InnerFill", frameGo.transform);
            RectTransform innerRt = innerGo.GetComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(2f, 2f);
            innerRt.offsetMax = new Vector2(-2f, -2f);

            Image innerImg = innerGo.AddComponent<Image>();
            innerImg.sprite = roundedBorderSprite;
            innerImg.type = Image.Type.Sliced;
            innerImg.color = PanelFillColor;

            // 1. Header: FALHA DE INTEGRIDADE
            CreateText(
                "HeaderText",
                innerGo.transform,
                "FALHA DE INTEGRIDADE",
                26,
                FontStyle.Normal,
                AlertRed,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 212f),
                new Vector2(1040f, 44f));

            CreateDivider("DividerTop", innerGo.transform, 166f, 1050f);

            // 2. Center status block
            CreateText(
                "UnitTitleText",
                innerGo.transform,
                "UNIDADE ECO",
                25,
                FontStyle.Bold,
                TextWhite,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 110f),
                new Vector2(1040f, 40f));

            CreateText(
                "StatusLine1",
                innerGo.transform,
                "SINAL VITAL / INDISPONÍVEL",
                24,
                FontStyle.Normal,
                TextLight,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 52f),
                new Vector2(1040f, 38f));

            CreateText(
                "StatusLine2",
                innerGo.transform,
                "PROCESSO DE CONSCIÊNCIA / INTERRUPÇÃO",
                24,
                FontStyle.Normal,
                TextLight,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -6f),
                new Vector2(1040f, 38f));

            CreateDivider("DividerMiddle", innerGo.transform, -56f, 1050f);

            // 3. Action options (left-aligned, mouse + keyboard interactive)
            restoreLabel = CreateMenuOption(
                "OptionRestore",
                innerGo.transform,
                "> RESTAURAR ÚLTIMO REGISTRO",
                new Vector2(0f, -108f),
                0,
                RestoreLastRecord);

            quitLabel = CreateMenuOption(
                "OptionQuit",
                innerGo.transform,
                "ENCERRAR SISTEMA",
                new Vector2(0f, -162f),
                1,
                QuitSystem);

            CreateDivider("DividerBottom", innerGo.transform, -210f, 1050f);

            // 4. Footer
            CreateText(
                "FooterText",
                innerGo.transform,
                "NEXUS // PROCESSO DE RECUPERAÇÃO",
                20,
                FontStyle.Normal,
                TextLight,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -242f),
                new Vector2(1040f, 34f));

            RefreshOptionVisuals();
        }

        private Text CreateMenuOption(
            string objName,
            Transform parent,
            string initialText,
            Vector2 anchoredPos,
            int optionIndex,
            UnityEngine.Events.UnityAction onClickAction)
        {
            GameObject btnGo = CreateUIObject(objName, parent);
            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(1040f, 46f);

            Image hitBg = btnGo.AddComponent<Image>();
            hitBg.color = new Color(0f, 0f, 0f, 0f); // Transparent raycast target

            Button btn = btnGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClickAction);

            EventTrigger trigger = btnGo.AddComponent<EventTrigger>();
            EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ => SetSelectedIndex(optionIndex));
            trigger.triggers.Add(enterEntry);

            Text label = CreateText(
                "Label",
                btnGo.transform,
                initialText,
                24,
                FontStyle.Normal,
                TextLight,
                TextAnchor.MiddleLeft,
                Vector2.zero,
                new Vector2(1040f, 46f));

            return label;
        }

        private static void CreateDivider(string objName, Transform parent, float posY, float width)
        {
            GameObject divGo = CreateUIObject(objName, parent);
            RectTransform rt = divGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, posY);
            rt.sizeDelta = new Vector2(width, 1.5f);

            Image img = divGo.AddComponent<Image>();
            img.color = DividerColor;
            img.raycastTarget = false;
        }

        private static Text CreateText(
            string objName,
            Transform parent,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment,
            Vector2 anchoredPos,
            Vector2 size)
        {
            GameObject go = CreateUIObject(objName, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Text txt = go.AddComponent<Text>();
            txt.font = sharedFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
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

        private static void EnsureFont()
        {
            if (sharedFont != null) return;
            sharedFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Segoe UI", "Consolas", "Arial" },
                24);
            if (sharedFont == null)
            {
                sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        private static void EnsureRoundedSprite()
        {
            if (roundedBorderSprite != null) return;

            const int size = 64;
            const int radius = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Max(0, Mathf.Max(radius - x, x - (size - 1 - radius)));
                    int dy = Mathf.Max(0, Mathf.Max(radius - y, y - (size - 1 - radius)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            roundedBorderSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }
    }
}
