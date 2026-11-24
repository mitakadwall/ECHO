using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Eco.Interaction;
using Eco.Player;
using Eco.Save;

namespace Eco.MemoryFragments
{
    /// <summary>
    /// Single-use interactive Memory Fragment attached to frag P4, frag P3, frag 2 P3, frag P2, and frag 2 P2.
    /// Locks player control cleanly, triggers fragment-specific visual effects, plays ECO/NEXUS subtitles
    /// (with Space to advance a line), marks the fragment as corrupted/used (persisted in EcoSaveManager),
    /// and restores full control to the player.
    /// </summary>
    public class MemoryFragmentInteractable : MonoBehaviour, IInteractable
    {
        [Header("Fragment Identity")]
        [SerializeField] private MemoryFragmentKind fragmentKind = MemoryFragmentKind.Frag01_PrimeiroRegistro;
        [SerializeField] private MemoryFragmentDefinition definition;

        [Header("Scene Effects (Optional)")]
        [SerializeField] private Light localEffectLight;
        [SerializeField] private Light[] oscillatingFloorLights;

        [Header("Glow Colors")]
        [SerializeField] private Color idleCyanColor = new Color(0.10f, 0.90f, 1.0f);
        [SerializeField] private Color activeBlueColor = new Color(0.08f, 0.68f, 1.0f);
        [SerializeField] private Color corruptedAmberColor = new Color(0.65f, 0.28f, 0.08f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Renderer meshRenderer;
        private MaterialPropertyBlock propBlock;
        private int glowMaterialIndex = -1;
        private bool isActivated;
        private bool isPlaying;

        public MemoryFragmentKind FragmentKind => fragmentKind;
        public MemoryFragmentDefinition Definition => definition;
        public bool IsActivated => isActivated;
        public bool IsPlaying => isPlaying;

        public string PromptText => "Acessar Fragmento de Memória";

        public void Configure(MemoryFragmentKind kind, Light localLight, Light[] floorLights)
        {
            fragmentKind = kind;
            definition = MemoryFragmentPresets.Create(kind);
            localEffectLight = localLight;
            oscillatingFloorLights = floorLights;
        }

        private void Awake()
        {
            meshRenderer = GetComponent<Renderer>();
            propBlock = new MaterialPropertyBlock();

            if (definition == null || string.IsNullOrEmpty(definition.fragmentId) || definition.dialogue == null || definition.dialogue.Count == 0)
            {
                definition = MemoryFragmentPresets.Create(fragmentKind);
            }

            DetectGlowMaterialSlot();
        }

        private void Start()
        {
            if (localEffectLight != null) localEffectLight.enabled = false;

            if (EcoSaveManager.Instance != null && EcoSaveManager.Instance.IsFragmentCollected(definition.fragmentId))
            {
                isActivated = true;
                ApplyPostEventVisualState();
            }
        }

        private void Update()
        {
            if (glowMaterialIndex < 0) return;

            if (!isActivated && !isPlaying)
            {
                // Subtle inviting cyan pulse while undiscovered
                float pulse = 1.4f + 0.65f * Mathf.Sin(Time.time * 2.8f);
                ApplyGlow(idleCyanColor, pulse);
            }
            else if (isActivated && !isPlaying && fragmentKind == MemoryFragmentKind.Frag02_RegistroDeAusencia)
            {
                // Fragmento 02 returns to dim flickering corrupted state
                float flicker = Random.value < 0.08f ? 0.15f : 0.45f;
                ApplyGlow(corruptedAmberColor, flicker);
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return !isActivated && !isPlaying && interactor != null;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            StartCoroutine(PlayFragmentRoutine(interactor));
        }

        private IEnumerator PlayFragmentRoutine(GameObject interactor)
        {
            isPlaying = true;
            isActivated = true;

            if (EcoSaveManager.Instance != null)
            {
                EcoSaveManager.Instance.MarkFragmentCollected(definition.fragmentId);
            }

            MemoryFragmentOverlayUI overlay = EnsureOverlay();
            PlayerController pc = interactor.GetComponent<PlayerController>();
            PlayerInteractor pi = interactor.GetComponent<PlayerInteractor>();
            CinemachineCamera playerVcam = FindPlayerVcam(interactor);
            float origFov = playerVcam != null ? playerVcam.Lens.FieldOfView : 70f;

            // Lock movement cleanly while keeping cursor hidden (no external menu)
            if (pc != null) pc.SetControlLocked(true);
            if (pi != null) pi.IsInteractionBlocked = true;
            Cursor.visible = false;

            // Capture Pavimento 2 light intensities if Fragmento 05 oscillates them
            float[] origFloorLightIntensities = null;
            Coroutine flickerRoutine = null;
            if (fragmentKind == MemoryFragmentKind.Frag05_AOrigem && oscillatingFloorLights != null && oscillatingFloorLights.Length > 0)
            {
                origFloorLightIntensities = new float[oscillatingFloorLights.Length];
                for (int i = 0; i < oscillatingFloorLights.Length; i++)
                {
                    origFloorLightIntensities[i] = oscillatingFloorLights[i] != null ? oscillatingFloorLights[i].intensity : 3.8f;
                }
                flickerRoutine = StartCoroutine(OscillateFloorLightsRoutine(origFloorLightIntensities));
            }

            // =========================================================================
            // Fragment-Specific Opening Visuals
            // =========================================================================
            ApplyGlow(activeBlueColor, 3.6f);
            if (localEffectLight != null)
            {
                localEffectLight.enabled = true;
                localEffectLight.color = (fragmentKind == MemoryFragmentKind.Frag04_ONome) ? activeBlueColor : idleCyanColor;
                localEffectLight.intensity = (fragmentKind == MemoryFragmentKind.Frag04_ONome) ? 5.2f : 2.6f;
            }

            switch (fragmentKind)
            {
                case MemoryFragmentKind.Frag01_PrimeiroRegistro:
                    // [HOOK DE ÁUDIO FUTURO: som de esteiras operando + interferência]
                    overlay.SetDistortion(0.45f);
                    overlay.ShowDataPanel(definition.headerTag, definition.corruptedDisplayText, false);
                    yield return StartCoroutine(overlay.PlayAbstractMemoryFlashesRoutine());
                    break;

                case MemoryFragmentKind.Frag02_RegistroDeAusencia:
                    // [HOOK DE ÁUDIO FUTURO: ruído de abertura da cápsula vazia]
                    overlay.SetDistortion(0.32f);
                    overlay.ShowDataPanel(definition.headerTag, definition.corruptedDisplayText, false);
                    yield return WaitSkippableBySpace(2.2f);
                    // Sequence vanishes before being fully read
                    overlay.HideDataPanel();
                    break;

                case MemoryFragmentKind.Frag03_AVoz:
                    // [HOOK DE ÁUDIO FUTURO: ruído de gravação corrompida / respiração distante]
                    overlay.SetDistortion(0.28f);
                    overlay.ShowDataPanel(definition.headerTag, definition.corruptedDisplayText, false);
                    yield return WaitSkippableBySpace(1.8f);
                    // NEXUS abruptly cuts the transmission
                    overlay.ShowDataPanel(
                        "SISTEMA NEXUS // INTERVENÇÃO",
                        "TRANSMISSÃO INTERROMPIDA.\nSINAL DE OBSERVAÇÃO ENCERRADO.",
                        true);
                    yield return WaitSkippableBySpace(1.2f);
                    overlay.HideDataPanel();
                    break;

                case MemoryFragmentKind.Frag04_ONome:
                    // [HOOK DE ÁUDIO FUTURO: ventilação abafada + pulso criogênico]
                    overlay.SetDistortion(0.40f);
                    overlay.ShowDataPanel(definition.headerTag, definition.corruptedDisplayText, false);
                    if (playerVcam != null) yield return LerpCameraFov(playerVcam, origFov, 54f, 0.85f);
                    break;

                case MemoryFragmentKind.Frag05_AOrigem:
                    // [HOOK DE ÁUDIO FUTURO: servidores com ruídos irregulares]
                    overlay.SetDistortion(0.48f);
                    overlay.ShowDataPanel(definition.headerTag, definition.corruptedDisplayText, false);
                    yield return WaitSkippableBySpace(1.4f);
                    break;
            }

            // =========================================================================
            // Play ECO / NEXUS Dialogue Lines (Space advances current line)
            // =========================================================================
            if (definition.dialogue != null)
            {
                foreach (MemoryDialogueLine line in definition.dialogue)
                {
                    overlay.ShowSubtitle(line.speaker, line.text, line.IsNexus);
                    yield return WaitSkippableBySpace(line.durationSeconds);
                    overlay.HideSubtitle();
                    if (line.pauseAfterSeconds > 0f)
                    {
                        yield return WaitSkippableBySpace(line.pauseAfterSeconds);
                    }
                }
            }

            // =========================================================================
            // Post-Fragment Cleanup & Return Control
            // =========================================================================
            overlay.HideDataPanel();
            overlay.HideSubtitle();

            if (flickerRoutine != null)
            {
                StopCoroutine(flickerRoutine);
                yield return RestoreFloorLightsRoutine(origFloorLightIntensities, 0.85f);
            }

            if (playerVcam != null && !Mathf.Approximately(playerVcam.Lens.FieldOfView, origFov))
            {
                yield return LerpCameraFov(playerVcam, playerVcam.Lens.FieldOfView, origFov, 0.65f);
            }

            overlay.SetDistortion(0f);
            overlay.HideAllImmediate();

            if (localEffectLight != null) localEffectLight.enabled = false;
            ApplyPostEventVisualState();

            if (pi != null) pi.IsInteractionBlocked = false;
            if (pc != null) pc.SetControlLocked(false);

            isPlaying = false;
        }

        private void ApplyPostEventVisualState()
        {
            if (fragmentKind == MemoryFragmentKind.Frag02_RegistroDeAusencia)
            {
                ApplyGlow(corruptedAmberColor, 0.45f);
            }
            else
            {
                // Fragmento 01, 03, 04, 05 power off / enter inert corrupted state
                ApplyGlow(Color.black, 0f);
            }
        }

        private IEnumerator WaitSkippableBySpace(float duration)
        {
            float elapsed = 0f;
            // Small guard so the E press or previous Space press doesn't double-skip instantaneously
            float minGuard = 0.22f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= minGuard && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    yield break;
                }
                yield return null;
            }
        }

        private IEnumerator OscillateFloorLightsRoutine(float[] baseIntensities)
        {
            while (true)
            {
                for (int i = 0; i < oscillatingFloorLights.Length; i++)
                {
                    Light l = oscillatingFloorLights[i];
                    if (l == null) continue;
                    float factor = Random.value < 0.25f ? Random.Range(0.08f, 0.35f) : Random.Range(0.55f, 1.15f);
                    l.intensity = baseIntensities[i] * factor;
                }
                yield return new WaitForSecondsRealtime(Random.Range(0.08f, 0.22f));
            }
        }

        private IEnumerator RestoreFloorLightsRoutine(float[] targetIntensities, float duration)
        {
            if (oscillatingFloorLights == null || targetIntensities == null) yield break;
            float[] start = new float[oscillatingFloorLights.Length];
            for (int i = 0; i < oscillatingFloorLights.Length; i++)
            {
                start[i] = oscillatingFloorLights[i] != null ? oscillatingFloorLights[i].intensity : 0f;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < oscillatingFloorLights.Length; i++)
                {
                    if (oscillatingFloorLights[i] != null)
                    {
                        oscillatingFloorLights[i].intensity = Mathf.Lerp(start[i], targetIntensities[i], t);
                    }
                }
                yield return null;
            }

            for (int i = 0; i < oscillatingFloorLights.Length; i++)
            {
                if (oscillatingFloorLights[i] != null) oscillatingFloorLights[i].intensity = targetIntensities[i];
            }
        }

        private static IEnumerator LerpCameraFov(CinemachineCamera vcam, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                vcam.Lens.FieldOfView = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            vcam.Lens.FieldOfView = to;
        }

        private void DetectGlowMaterialSlot()
        {
            if (meshRenderer == null) return;
            Material[] mats = meshRenderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && (mats[i].name.ToLowerInvariant().Contains("luz") || mats[i].name.ToLowerInvariant().Contains("emit")))
                {
                    glowMaterialIndex = i;
                    return;
                }
            }
        }

        private void ApplyGlow(Color color, float intensity)
        {
            if (meshRenderer == null || glowMaterialIndex < 0) return;
            meshRenderer.GetPropertyBlock(propBlock, glowMaterialIndex);
            propBlock.SetColor(BaseColorId, color);
            propBlock.SetColor(EmissionColorId, intensity > 0f ? color * Mathf.LinearToGammaSpace(intensity) : Color.black);
            meshRenderer.SetPropertyBlock(propBlock, glowMaterialIndex);
        }

        private static MemoryFragmentOverlayUI EnsureOverlay()
        {
            if (MemoryFragmentOverlayUI.Instance != null) return MemoryFragmentOverlayUI.Instance;
            MemoryFragmentOverlayUI found = FindAnyObjectByType<MemoryFragmentOverlayUI>();
            if (found != null) return found;
            GameObject go = new GameObject("MemoryFragmentOverlayUI");
            return go.AddComponent<MemoryFragmentOverlayUI>();
        }

        private static CinemachineCamera FindPlayerVcam(GameObject player)
        {
            return player != null ? player.GetComponentInChildren<CinemachineCamera>(true) : null;
        }
    }
}
