using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Eco.MemoryFragments
{
    /// <summary>
    /// Shared screen-space overlay & subtle URP post-processing controller used exclusively
    /// while a Memory Fragment event is active. Remains completely disabled during normal gameplay.
    /// </summary>
    public class MemoryFragmentOverlayUI : MonoBehaviour
    {
        public static MemoryFragmentOverlayUI Instance { get; private set; }

        private static readonly Color CyanBright = new Color(0.298f, 0.788f, 0.910f, 1f);
        private static readonly Color AmberGold = new Color(0.851f, 0.643f, 0.255f, 1f);
        private static readonly Color ErrorRed = new Color(0.950f, 0.260f, 0.240f, 1f);
        private static readonly Color TextIce = new Color(0.886f, 0.945f, 0.973f, 1f);
        private static readonly Color TextMuted = new Color(0.450f, 0.600f, 0.680f, 1f);

        private static Font sharedMonoFont;

        private Canvas overlayCanvas;
        private Image flashImage;
        private Image silhouetteGlowImage;

        private CanvasGroup dataPanelGroup;
        private Text dataHeaderText;
        private Text dataBodyText;

        private CanvasGroup subtitleGroup;
        private Text subtitleText;
        private Text advanceHintText;

        private Volume distortionVolume;
        private ChromaticAberration chromaticAberration;
        private LensDistortion lensDistortion;
        private Vignette vignette;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureFont();
            BuildOverlayCanvas();
            BuildDistortionVolume();
            HideAllImmediate();
        }

        private void OnDestroy()
        {
            if (distortionVolume != null && distortionVolume.profile != null)
            {
                Destroy(distortionVolume.profile);
            }
        }

        public void HideAllImmediate()
        {
            if (overlayCanvas != null) overlayCanvas.enabled = false;
            if (flashImage != null) flashImage.color = new Color(1f, 1f, 1f, 0f);
            if (silhouetteGlowImage != null) silhouetteGlowImage.color = new Color(0f, 0f, 0f, 0f);
            if (dataPanelGroup != null) dataPanelGroup.alpha = 0f;
            if (subtitleGroup != null) subtitleGroup.alpha = 0f;
            SetDistortion(0f);
        }

        public void SetDistortion(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (distortionVolume != null)
            {
                distortionVolume.enabled = intensity > 0.005f;
                distortionVolume.weight = intensity;
            }
            if (chromaticAberration != null) chromaticAberration.intensity.value = Mathf.Lerp(0f, 0.65f, intensity);
            if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(0f, -0.26f, intensity);
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, 0.42f, intensity);
        }

        public void ShowDataPanel(string header, string body, bool alertRed = false)
        {
            if (overlayCanvas != null) overlayCanvas.enabled = true;
            if (dataHeaderText != null)
            {
                dataHeaderText.color = alertRed ? ErrorRed : CyanBright;
                dataHeaderText.text = $"<b>{header}</b>";
            }
            if (dataBodyText != null)
            {
                dataBodyText.color = alertRed ? AmberGold : TextIce;
                dataBodyText.text = body;
            }
            if (dataPanelGroup != null) dataPanelGroup.alpha = 1f;
        }

        public void HideDataPanel()
        {
            if (dataPanelGroup != null) dataPanelGroup.alpha = 0f;
        }

        public void ShowSubtitle(string speaker, string text, bool isNexus)
        {
            if (overlayCanvas != null) overlayCanvas.enabled = true;
            string hex = isNexus ? "#D9A441" : "#4CC9E8";
            if (subtitleText != null)
            {
                subtitleText.text = $"<color={hex}><b>{speaker}:</b></color> \"{text}\"";
            }
            if (subtitleGroup != null) subtitleGroup.alpha = 1f;
        }

        public void HideSubtitle()
        {
            if (subtitleGroup != null) subtitleGroup.alpha = 0f;
        }

        /// <summary>
        /// Fragmento 01: Brief abstract flashes (white room, corridor light, intense flare, vague silhouette)
        /// without requiring any new character model.
        /// </summary>
        public IEnumerator PlayAbstractMemoryFlashesRoutine()
        {
            if (overlayCanvas != null) overlayCanvas.enabled = true;

            // Flash 1: Brief blinding white room flash
            yield return FlashScreen(new Color(0.92f, 0.97f, 1.0f, 0.68f), 0.14f, 0.22f);
            yield return new WaitForSecondsRealtime(0.28f);

            // Flash 2: Cold cyan corridor glare
            yield return FlashScreen(new Color(0.28f, 0.85f, 0.98f, 0.45f), 0.12f, 0.20f);
            yield return new WaitForSecondsRealtime(0.24f);

            // Flash 3: Intense white light + vague dark silhouette shadow in center
            if (silhouetteGlowImage != null) silhouetteGlowImage.color = new Color(0.02f, 0.04f, 0.06f, 0.55f);
            yield return FlashScreen(new Color(1f, 1f, 1f, 0.78f), 0.10f, 0.30f);
            if (silhouetteGlowImage != null) silhouetteGlowImage.color = new Color(0f, 0f, 0f, 0f);
        }

        private IEnumerator FlashScreen(Color peakColor, float holdTime, float fadeTime)
        {
            if (flashImage == null) yield break;
            flashImage.color = peakColor;
            yield return new WaitForSecondsRealtime(holdTime);
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / fadeTime);
                flashImage.color = new Color(peakColor.r, peakColor.g, peakColor.b, peakColor.a * t);
                yield return null;
            }
            flashImage.color = new Color(peakColor.r, peakColor.g, peakColor.b, 0f);
        }

        private void BuildDistortionVolume()
        {
            GameObject volGo = new GameObject("MemoryFragmentPostProcessVolume");
            volGo.transform.SetParent(transform, false);

            distortionVolume = volGo.AddComponent<Volume>();
            distortionVolume.isGlobal = true;
            distortionVolume.priority = 85f;
            distortionVolume.weight = 0f;
            distortionVolume.enabled = false;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            chromaticAberration = profile.Add<ChromaticAberration>(true);
            chromaticAberration.intensity.overrideState = true;

            lensDistortion = profile.Add<LensDistortion>(true);
            lensDistortion.intensity.overrideState = true;

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.55f;

            distortionVolume.profile = profile;
        }

        private void BuildOverlayCanvas()
        {
            GameObject canvasGo = new GameObject("MemoryFragmentOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);

            overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 90;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRt = canvasGo.GetComponent<RectTransform>();

            // Fullscreen abstract flash layer
            GameObject flashGo = CreateUI("AbstractFlashLayer", rootRt);
            StretchFull(flashGo.GetComponent<RectTransform>());
            flashImage = flashGo.AddComponent<Image>();
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.raycastTarget = false;

            // Vague undefined vertical silhouette blur for Fragmento 01 flash
            GameObject silGo = CreateUI("AbstractSilhouette", rootRt);
            RectTransform silRt = silGo.GetComponent<RectTransform>();
            silRt.anchorMin = new Vector2(0.42f, 0.18f);
            silRt.anchorMax = new Vector2(0.58f, 0.82f);
            silRt.offsetMin = Vector2.zero;
            silRt.offsetMax = Vector2.zero;
            silhouetteGlowImage = silGo.AddComponent<Image>();
            silhouetteGlowImage.color = new Color(0f, 0f, 0f, 0f);
            silhouetteGlowImage.raycastTarget = false;

            // Corrupted data / audio readout panel (upper-center, sleek industrial)
            GameObject dataGo = CreateUI("CorruptedDataPanel", rootRt);
            RectTransform dataRt = dataGo.GetComponent<RectTransform>();
            dataRt.anchorMin = new Vector2(0.20f, 0.72f);
            dataRt.anchorMax = new Vector2(0.80f, 0.90f);
            dataRt.offsetMin = Vector2.zero;
            dataRt.offsetMax = Vector2.zero;
            dataPanelGroup = dataGo.AddComponent<CanvasGroup>();
            dataPanelGroup.alpha = 0f;
            Image dataBg = dataGo.AddComponent<Image>();
            dataBg.color = new Color(0.032f, 0.062f, 0.088f, 0.86f);
            dataBg.raycastTarget = false;

            GameObject dhGo = CreateUI("DataHeader", dataRt);
            RectTransform dhRt = dhGo.GetComponent<RectTransform>();
            dhRt.anchorMin = new Vector2(0.03f, 0.68f);
            dhRt.anchorMax = new Vector2(0.97f, 0.94f);
            dhRt.offsetMin = Vector2.zero;
            dhRt.offsetMax = Vector2.zero;
            dataHeaderText = dhGo.AddComponent<Text>();
            ConfigureText(dataHeaderText, string.Empty, 22, CyanBright, FontStyle.Bold, TextAnchor.MiddleLeft);

            GameObject dbGo = CreateUI("DataBody", dataRt);
            RectTransform dbRt = dbGo.GetComponent<RectTransform>();
            dbRt.anchorMin = new Vector2(0.03f, 0.08f);
            dbRt.anchorMax = new Vector2(0.97f, 0.66f);
            dbRt.offsetMin = Vector2.zero;
            dbRt.offsetMax = Vector2.zero;
            dataBodyText = dbGo.AddComponent<Text>();
            ConfigureText(dataBodyText, string.Empty, 21, TextIce, FontStyle.Normal, TextAnchor.UpperLeft);
            dataBodyText.lineSpacing = 1.25f;

            // Bottom ECO / NEXUS subtitle bar
            GameObject subGo = CreateUI("MemorySubtitlePanel", rootRt);
            RectTransform subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.10f, 0.04f);
            subRt.anchorMax = new Vector2(0.90f, 0.135f);
            subRt.offsetMin = Vector2.zero;
            subRt.offsetMax = Vector2.zero;
            subtitleGroup = subGo.AddComponent<CanvasGroup>();
            subtitleGroup.alpha = 0f;
            Image subBg = subGo.AddComponent<Image>();
            subBg.color = new Color(0.015f, 0.028f, 0.040f, 0.88f);
            subBg.raycastTarget = false;

            GameObject subTxtGo = CreateUI("SubtitleText", subRt);
            RectTransform stRt = subTxtGo.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0.03f, 0.18f);
            stRt.anchorMax = new Vector2(0.97f, 0.92f);
            stRt.offsetMin = Vector2.zero;
            stRt.offsetMax = Vector2.zero;
            subtitleText = subTxtGo.AddComponent<Text>();
            ConfigureText(subtitleText, string.Empty, 28, TextIce, FontStyle.Normal, TextAnchor.MiddleCenter);

            GameObject hintGo = CreateUI("AdvanceHint", subRt);
            RectTransform hRt = hintGo.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0.70f, 0.02f);
            hRt.anchorMax = new Vector2(0.98f, 0.24f);
            hRt.offsetMin = Vector2.zero;
            hRt.offsetMax = Vector2.zero;
            advanceHintText = hintGo.AddComponent<Text>();
            ConfigureText(advanceHintText, "[ESPAÇO] AVANÇAR", 15, TextMuted, FontStyle.Normal, TextAnchor.LowerRight);
        }

        private static void EnsureFont()
        {
            if (sharedMonoFont != null) return;
            string[] preferred = { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console" };
            try { sharedMonoFont = Font.CreateDynamicFontFromOSFont(preferred, 28); }
            catch { sharedMonoFont = null; }
            if (sharedMonoFont == null) sharedMonoFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void ConfigureText(Text txt, string content, int fontSize, Color color, FontStyle style, TextAnchor align)
        {
            txt.font = sharedMonoFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.fontStyle = style;
            txt.alignment = align;
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
