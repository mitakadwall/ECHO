using UnityEngine;
using UnityEngine.UI;
using Eco.Combat;
using Eco.Enemies.Boss;

namespace Eco.UI
{
    /// <summary>
    /// Subtle diegetic top-center boss health bar for Overseer "Carcass-0".
    /// Appears only while the boss fight is active and hides automatically upon defeat.
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.045f, 0.06f, 0.86f);
        private static readonly Color BorderColor = new Color(0.24f, 0.35f, 0.42f, 0.85f);
        private static readonly Color Phase1Fill = new Color(0.86f, 0.52f, 0.14f, 1f);
        private static readonly Color Phase2Fill = new Color(0.92f, 0.20f, 0.12f, 1f);
        private static readonly Color TextLight = new Color(0.88f, 0.93f, 0.96f, 1f);

        private static Font sharedFont;

        private Canvas canvas;
        private CanvasGroup group;
        private Image fillImage;
        private Text phaseText;
        private Text hpValueText;

        private BossCarcassController boss;
        private EnemyHealth bossHealth;
        private float targetAlpha;

        private void Awake()
        {
            EnsureFont();
            BuildBar();
            if (group != null) group.alpha = 0f;
            if (canvas != null) canvas.enabled = false;
        }

        private void Start()
        {
            BindBoss();
        }

        private void OnDestroy()
        {
            UnbindBoss();
        }

        public void BindBoss(BossCarcassController targetBoss = null)
        {
            UnbindBoss();

            boss = targetBoss != null ? targetBoss : FindAnyObjectByType<BossCarcassController>();
            if (boss == null) return;

            bossHealth = boss.GetComponent<EnemyHealth>();
            if (bossHealth != null)
            {
                bossHealth.OnHealthChanged += HandleHealthChanged;
                bossHealth.OnDied += HandleBossDied;
                HandleHealthChanged(bossHealth.CurrentHealth, bossHealth.MaxHealth);
            }
        }

        private void UnbindBoss()
        {
            if (bossHealth != null)
            {
                bossHealth.OnHealthChanged -= HandleHealthChanged;
                bossHealth.OnDied -= HandleBossDied;
            }
        }

        private void Update()
        {
            if (boss == null)
            {
                targetAlpha = 0f;
            }
            else
            {
                bool shouldShow = boss.IsFightActive && bossHealth != null && !bossHealth.IsDead;
                targetAlpha = shouldShow ? 1f : 0f;

                if (shouldShow)
                {
                    if (fillImage != null)
                    {
                        fillImage.color = boss.IsPhase2 ? Phase2Fill : Phase1Fill;
                    }
                    if (phaseText != null)
                    {
                        phaseText.text = boss.IsPhase2
                            ? "FASE 02 // SUPERAQUECIMENTO"
                            : "FASE 01 // PRESSÃO HIDRÁULICA";
                        phaseText.color = boss.IsPhase2 ? Phase2Fill : new Color(0.75f, 0.83f, 0.88f, 1f);
                    }
                }
            }

            if (group != null)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 3.5f);
                if (canvas != null)
                {
                    canvas.enabled = group.alpha > 0.01f;
                }
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (fillImage != null)
            {
                fillImage.fillAmount = ratio;
            }
            if (hpValueText != null)
            {
                hpValueText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        private void HandleBossDied()
        {
            HandleHealthChanged(0f, bossHealth != null ? bossHealth.MaxHealth : 220f);
            targetAlpha = 0f;
        }

        private void BuildBar()
        {
            GameObject canvasGo = new GameObject("BossHealthBarCanvas");
            canvasGo.transform.SetParent(transform, false);

            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            group = canvasGo.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // Top-center container
            GameObject root = new GameObject("BossBarRoot", typeof(RectTransform));
            root.transform.SetParent(canvasGo.transform, false);
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.anchoredPosition = new Vector2(0f, -26f);
            rootRt.sizeDelta = new Vector2(760f, 68f);

            // Title (left)
            CreateLabel("BossName", root.transform, "OVERSEER // CARCASS-0", 19, FontStyle.Bold, TextLight,
                TextAnchor.MiddleLeft, new Vector2(0f, 20f), new Vector2(460f, 26f), new Vector2(0f, 0.5f));

            // Phase label (right)
            phaseText = CreateLabel("PhaseLabel", root.transform, "FASE 01 // PRESSÃO HIDRÁULICA", 15, FontStyle.Normal, TextLight,
                TextAnchor.MiddleRight, new Vector2(0f, 20f), new Vector2(360f, 24f), new Vector2(1f, 0.5f));

            // Bar border
            GameObject borderGo = new GameObject("BarBorder", typeof(RectTransform));
            borderGo.transform.SetParent(root.transform, false);
            RectTransform borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(0f, 0.5f);
            borderRt.anchorMax = new Vector2(1f, 0.5f);
            borderRt.pivot = new Vector2(0.5f, 0.5f);
            borderRt.anchoredPosition = new Vector2(0f, -8f);
            borderRt.sizeDelta = new Vector2(0f, 18f);
            Image borderImg = borderGo.AddComponent<Image>();
            borderImg.color = BorderColor;

            // Bar background
            GameObject bgGo = new GameObject("BarBg", typeof(RectTransform));
            bgGo.transform.SetParent(borderGo.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(2f, 2f);
            bgRt.offsetMax = new Vector2(-2f, -2f);
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.color = PanelBg;

            // Bar fill
            GameObject fillGo = new GameObject("BarFill", typeof(RectTransform));
            fillGo.transform.SetParent(bgGo.transform, false);
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            fillImage = fillGo.AddComponent<Image>();
            fillImage.color = Phase1Fill;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;

            // Small numeric readout below right
            hpValueText = CreateLabel("HpValue", root.transform, "220 / 220", 13, FontStyle.Normal,
                new Color(0.68f, 0.78f, 0.84f, 0.9f), TextAnchor.MiddleRight,
                new Vector2(0f, -26f), new Vector2(200f, 20f), new Vector2(1f, 0.5f));
        }

        private static Text CreateLabel(
            string name,
            Transform parent,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor align,
            Vector2 pos,
            Vector2 size,
            Vector2 anchorPivotX)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorPivotX;
            rt.anchorMax = anchorPivotX;
            rt.pivot = anchorPivotX;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text t = go.AddComponent<Text>();
            t.font = sharedFont;
            t.text = content;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void EnsureFont()
        {
            if (sharedFont != null) return;
            sharedFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Consolas", "Arial" }, 18);
            if (sharedFont == null)
            {
                sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }
    }
}
