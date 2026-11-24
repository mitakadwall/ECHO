using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Eco.Player;
using Eco.Save;

namespace Eco.Cutscene
{
    public enum IntroCutsceneStage
    {
        Idle,
        Scene1Darkness,
        Scene2BootVision,
        Scene3LookAroundAndArm,
        Scene4NexusDialogue,
        Scene5DoorUnlockAndExit,
        Completed
    }

    /// <summary>
    /// First-person opening wake-up cutscene in Pavimento 05 — Sala 5A (Câmara de Quarentena, ~25s):
    /// - Cena 1: Pitch black screen with subtle neural boot interference.
    /// - Cena 2: Gradual vision wake-up on the metal 'maca' looking up at the ceiling,
    ///           brief URP distortion & gradual focus, blue forearm display powers on.
    /// - Cena 3: Controlled first-person head movement observing Sala 5A, looking at the glowing forearm,
    ///           then turning toward the quarantine exit door.
    /// - Cena 4: NEXUS first transmission subtitles with dramatic pauses.
    /// - Cena 5: Emergency exit light turns on, electronic door lock releases (red -> green),
    ///           camera blends smoothly to PlayerCamera and hands full control to ECO.
    /// Automatically skipped when loading a saved game via MainMenu's "CONTINUAR REGISTRO".
    /// </summary>
    public class IntroWakeCutsceneController : MonoBehaviour
    {
        [Header("Playback Settings")]
        [SerializeField] private bool playOnFreshStart = true;
        [SerializeField] private bool allowHoldSpaceToSkip = true;
        [SerializeField] private float holdSkipDuration = 1.0f;

        [Header("Scene References")]
        [SerializeField] private CinemachineCamera cutsceneCamera;
        [SerializeField] private Light emergencyDoorLight;
        [SerializeField] private Renderer doorLockIndicatorRenderer;
        [SerializeField] private Transform doorLookTarget;

        [Header("Camera Pose on Maca (Sala 5A)")]
        [SerializeField] private Vector3 macaHeadWorldPos = new Vector3(14.00f, 43.34f, -8.25f);
        [SerializeField] private float sitUpHeightOffset = 0.42f;
        [SerializeField] private int activeCameraPriority = 70;
        [SerializeField] private float cutsceneFov = 64f;

        [Header("Visual Colors")]
        [SerializeField] private Color armBlueColor = new Color(0.08f, 0.90f, 1.0f);
        [SerializeField] private Color lockRedColor = new Color(0.95f, 0.12f, 0.10f);
        [SerializeField] private Color lockGreenColor = new Color(0.10f, 0.95f, 0.45f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color AmberGold = new Color(0.851f, 0.643f, 0.255f, 1f);
        private static readonly Color TextIce = new Color(0.886f, 0.945f, 0.973f, 1f);
        private static readonly Color TextMuted = new Color(0.420f, 0.560f, 0.635f, 1f);

        private static Font sharedMonoFont;

        private IntroCutsceneStage currentStage = IntroCutsceneStage.Idle;
        private bool skipRequested;
        private float holdSkipTimer;

        // Player & arm restore state
        private PlayerController playerController;
        private PlayerInteractor playerInteractor;
        private PlayerCombat playerCombat;
        private ArmHealthLight armLight;
        private Transform rightArmFps;
        private Transform origArmParent;
        private Vector3 origArmLocalPos;
        private Quaternion origArmLocalRot;
        private bool armReparented;

        // Runtime URP Volume for distortion + gradual focus
        private Volume postProcessVolume;
        private Vignette vignette;
        private ChromaticAberration chromaticAberration;
        private LensDistortion lensDistortion;
        private DepthOfField depthOfField;

        // Runtime Overlay UI (black fade, interference, NEXUS subtitles, optional skip hint)
        private Canvas overlayCanvas;
        private Image blackFadeImage;
        private Image glitchStripImage;
        private CanvasGroup subtitleGroup;
        private Text subtitleText;
        private Text skipHintText;

        private MaterialPropertyBlock lockPropBlock;
        private Coroutine cutsceneRoutine;

        public IntroCutsceneStage CurrentStage => currentStage;
        public bool IsPlaying => currentStage != IntroCutsceneStage.Idle && currentStage != IntroCutsceneStage.Completed;

        public void AssignReferences(CinemachineCamera cam, Light emergLight, Renderer lockRend, Transform doorTarget)
        {
            cutsceneCamera = cam;
            emergencyDoorLight = emergLight;
            doorLockIndicatorRenderer = lockRend;
            doorLookTarget = doorTarget;
        }

        private void Awake()
        {
            lockPropBlock = new MaterialPropertyBlock();
            EnsureFont();
        }

        private void Start()
        {
            playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerInteractor = playerController.GetComponent<PlayerInteractor>();
                playerCombat = playerController.GetComponent<PlayerCombat>();
                armLight = playerController.GetComponent<ArmHealthLight>();
                if (playerController.CameraRoot != null)
                {
                    rightArmFps = playerController.CameraRoot.Find("RightArmFPS");
                }
            }

            // Skip automatically if the player clicked "CONTINUAR REGISTRO" in MainMenu
            bool isContinuingSave = EcoSaveManager.PendingLoadOnNextGameplay;
            if (!playOnFreshStart || isContinuingSave)
            {
                SetDoorUnlockedVisuals(true);
                if (cutsceneCamera != null) cutsceneCamera.Priority.Value = 0;
                currentStage = IntroCutsceneStage.Completed;
                return;
            }

            BuildOverlayCanvas();
            BuildRuntimePostProcessVolume();
            cutsceneRoutine = StartCoroutine(PlayIntroCutsceneRoutine());
        }

        private void Update()
        {
            if (!IsPlaying || !allowHoldSpaceToSkip || Keyboard.current == null) return;

            if (Keyboard.current.spaceKey.isPressed)
            {
                holdSkipTimer += Time.unscaledDeltaTime;
                if (skipHintText != null)
                {
                    int pct = Mathf.Clamp(Mathf.RoundToInt((holdSkipTimer / holdSkipDuration) * 100f), 0, 100);
                    skipHintText.text = $"PULANDO... {pct}%";
                }
                if (holdSkipTimer >= holdSkipDuration)
                {
                    skipRequested = true;
                }
            }
            else if (holdSkipTimer > 0f)
            {
                holdSkipTimer = 0f;
                if (skipHintText != null) skipHintText.text = "[SEGURE ESPAÇO PARA PULAR]";
            }
        }

        private void OnDestroy()
        {
            RestoreArmToPlayer();
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                Destroy(postProcessVolume.profile);
            }
        }

        private IEnumerator PlayIntroCutsceneRoutine()
        {
            // Lock player controls cleanly and hide cursor
            if (playerController != null) playerController.SetControlLocked(true);
            if (playerInteractor != null) playerInteractor.IsInteractionBlocked = true;
            if (playerCombat != null) playerCombat.enabled = false;
            Cursor.visible = false;

            // Initial locked chamber door visuals
            SetDoorUnlockedVisuals(false);

            // Start forearm light OFF during Cena 1 darkness
            if (armLight != null) armLight.SetEndgameOverride(true, Color.black, 0f);

            // Position cutscene camera lying on the maca looking straight up at the ceiling (ducha quimica)
            if (cutsceneCamera == null)
            {
                GameObject camGo = new GameObject("IntroCutsceneCamera");
                camGo.transform.SetParent(transform, true);
                cutsceneCamera = camGo.AddComponent<CinemachineCamera>();
            }

            Quaternion ceilingRot = Quaternion.Euler(-78f, -15f, 4f);
            cutsceneCamera.transform.position = macaHeadWorldPos;
            cutsceneCamera.transform.rotation = ceilingRot;
            cutsceneCamera.Lens.FieldOfView = cutsceneFov;
            cutsceneCamera.Priority.Value = activeCameraPriority;

            // Temporarily attach FPS forearm to cutscene camera so Cena 3 arm inspection is 100% first-person
            ReparentArmToCutsceneCamera();
            if (rightArmFps != null)
            {
                rightArmFps.localPosition = new Vector3(0.32f, -0.62f, 0.30f);
                rightArmFps.localRotation = Quaternion.Euler(28f, 340f, 0f);
            }

            // =========================================================================
            // CENA 1 — Escuridão (0s – 3.0s)
            // [HOOK DE ÁUDIO FUTURO: maquinário distante, ventilação e interferência eletrônica]
            // =========================================================================
            currentStage = IntroCutsceneStage.Scene1Darkness;
            SetBlackAlpha(1f);
            SetPostProcessIntensity(1f);

            float c1Dur = 3.0f;
            float elapsed = 0f;
            while (elapsed < c1Dur && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                // Subtle neural boot interference flickers in the dark
                if (glitchStripImage != null)
                {
                    bool flash = Random.value < 0.14f && elapsed > 0.7f;
                    glitchStripImage.enabled = flash;
                    if (flash)
                    {
                        RectTransform rt = glitchStripImage.rectTransform;
                        float y = Random.Range(0.15f, 0.85f);
                        rt.anchorMin = new Vector2(0f, y);
                        rt.anchorMax = new Vector2(1f, y + Random.Range(0.004f, 0.018f));
                        glitchStripImage.color = new Color(0.25f, 0.78f, 0.92f, Random.Range(0.06f, 0.22f));
                    }
                }
                yield return null;
            }
            if (glitchStripImage != null) glitchStripImage.enabled = false;

            // =========================================================================
            // CENA 2 — Inicialização (3.0s – 8.0s)
            // Visão surge gradualmente sobre a maca olhando para o teto, distorção breve + foco gradual,
            // display luminoso no antebraço acende em luz azul.
            // =========================================================================
            if (!skipRequested)
            {
                currentStage = IntroCutsceneStage.Scene2BootVision;
                float c2Dur = 5.0f;
                elapsed = 0f;
                bool armPoweredOn = false;

                while (elapsed < c2Dur && !skipRequested)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(elapsed / c2Dur);

                    // Gradual eyelid-like fade in with subtle consciousness wobble
                    float wobble = Mathf.Sin(elapsed * 4.5f) * 0.06f * (1f - u);
                    float blackA = Mathf.Clamp01(Mathf.Pow(1f - u, 1.55f) + wobble);
                    SetBlackAlpha(blackA);

                    // Brief distortion & gradual focus via URP post-processing
                    SetPostProcessIntensity(1f - Mathf.SmoothStep(0f, 1f, u));

                    // Subtle breathing camera sway while looking up at the ceiling
                    float sway = Mathf.Sin(elapsed * 1.4f) * 1.8f;
                    cutsceneCamera.transform.rotation = Quaternion.Euler(-78f + sway, -15f + sway * 0.6f, 4f * (1f - u));

                    // Forearm display lights up in blue halfway through Cena 2
                    if (u >= 0.45f)
                    {
                        float armT = Mathf.Clamp01((u - 0.45f) / 0.35f);
                        if (armLight != null)
                        {
                            armLight.SetEndgameOverride(true, armBlueColor, Mathf.Lerp(0f, 3.2f, armT));
                        }
                        if (!armPoweredOn)
                        {
                            armPoweredOn = true;
                            // [HOOK DE ÁUDIO FUTURO: som eletrônico do display do braço ligando]
                        }
                    }

                    yield return null;
                }

                SetBlackAlpha(0f);
                SetPostProcessIntensity(0f);
            }

            // =========================================================================
            // CENA 3 — Movimento (8.0s – 14.0s)
            // Cabeça ergue lentamente, olha esquerda -> direita na sala, olha para o próprio braço
            // com display azul aceso e depois vira para a porta da câmara.
            // =========================================================================
            if (!skipRequested)
            {
                currentStage = IntroCutsceneStage.Scene3LookAroundAndArm;

                Vector3 sittingPos = macaHeadWorldPos + Vector3.up * sitUpHeightOffset;
                Quaternion lookCenter = Quaternion.Euler(-10f, -20f, 0f);
                Quaternion lookLeft = Quaternion.Euler(-6f, -68f, 0f);
                Quaternion lookRight = Quaternion.Euler(-4f, 28f, 0f);
                Quaternion lookDownArm = Quaternion.Euler(22f, -15f, 0f);
                Quaternion lookAtDoor = GetDoorLookRotation(sittingPos);

                // 3a: Raise head slowly off the maca
                yield return BlendCameraPose(macaHeadWorldPos, cutsceneCamera.transform.rotation, sittingPos, lookCenter, 1.8f);
                // 3b: Look left across Sala 5A
                if (!skipRequested) yield return BlendCameraPose(sittingPos, lookCenter, sittingPos, lookLeft, 1.3f);
                // 3c: Look right across Sala 5A
                if (!skipRequested) yield return BlendCameraPose(sittingPos, lookLeft, sittingPos, lookRight, 1.4f);

                // 3d: Look down at own forearm with glowing blue display
                if (!skipRequested)
                {
                    Vector3 armHidden = rightArmFps != null ? rightArmFps.localPosition : Vector3.zero;
                    Quaternion armHiddenRot = rightArmFps != null ? rightArmFps.localRotation : Quaternion.identity;
                    Vector3 armInspectPos = new Vector3(0.14f, -0.17f, 0.36f);
                    Quaternion armInspectRot = Quaternion.Euler(14f, 326f, 16f);

                    float armDur = 1.5f;
                    elapsed = 0f;
                    while (elapsed < armDur && !skipRequested)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / armDur));
                        cutsceneCamera.transform.rotation = Quaternion.Slerp(lookRight, lookDownArm, u);
                        if (rightArmFps != null)
                        {
                            rightArmFps.localPosition = Vector3.Lerp(armHidden, armInspectPos, u);
                            rightArmFps.localRotation = Quaternion.Slerp(armHiddenRot, armInspectRot, u);
                        }
                        yield return null;
                    }

                    // Brief beat observing the blue display
                    yield return WaitSecondsSkippable(0.9f);

                    // 3e: Lower arm and turn toward the chamber door
                    float toDoorDur = 1.3f;
                    elapsed = 0f;
                    while (elapsed < toDoorDur && !skipRequested)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / toDoorDur));
                        cutsceneCamera.transform.rotation = Quaternion.Slerp(lookDownArm, lookAtDoor, u);
                        if (rightArmFps != null)
                        {
                            rightArmFps.localPosition = Vector3.Lerp(armInspectPos, origArmLocalPos, u);
                            rightArmFps.localRotation = Quaternion.Slerp(armInspectRot, origArmLocalRot, u);
                        }
                        yield return null;
                    }
                }
            }

            // =========================================================================
            // CENA 4 — NEXUS (14.0s – 23.0s)
            // [HOOK DE ÁUDIO FUTURO: ruído de ativação do alto-falante no teto + voz da NEXUS]
            // =========================================================================
            if (!skipRequested)
            {
                currentStage = IntroCutsceneStage.Scene4NexusDialogue;

                yield return ShowNexusSubtitle("Unidade despertada. Integridade do sistema... aceitável.", 2.7f);
                yield return WaitSecondsSkippable(0.85f);

                yield return ShowNexusSubtitle("Ah. Você também está acordado.", 2.1f);
                yield return WaitSecondsSkippable(1.25f); // pausa ligeiramente mais longa

                yield return ShowNexusSubtitle("Bem-vindo de volta, ECO.", 2.0f);
                yield return WaitSecondsSkippable(0.45f);

                yield return ShowNexusSubtitle("Não se preocupe. A saída está logo ali.", 2.5f);
                HideSubtitle();
            }

            // =========================================================================
            // CENA 5 — Encerramento (23.0s – 26.0s)
            // Câmera vira lentamente para a porta, luz de emergência acende,
            // trava eletrônica é liberada e o jogador recebe controle total.
            // =========================================================================
            if (!skipRequested)
            {
                currentStage = IntroCutsceneStage.Scene5DoorUnlockAndExit;
                Vector3 currentPos = cutsceneCamera.transform.position;
                Quaternion doorRot = GetDoorLookRotation(currentPos);
                yield return BlendCameraPose(currentPos, cutsceneCamera.transform.rotation, currentPos, doorRot, 0.9f);

                // Emergency light turns on & electronic door lock releases (red -> green)
                // [HOOK DE ÁUDIO FUTURO: clique metálico da trava eletrônica sendo liberada]
                SetDoorUnlockedVisuals(true);
                yield return WaitSecondsSkippable(1.1f);
            }

            FinishCutscene();
        }

        private void FinishCutscene()
        {
            HideSubtitle();
            SetBlackAlpha(0f);
            SetPostProcessIntensity(0f);
            if (glitchStripImage != null) glitchStripImage.enabled = false;
            if (overlayCanvas != null) overlayCanvas.enabled = false;
            if (postProcessVolume != null) postProcessVolume.enabled = false;

            SetDoorUnlockedVisuals(true);
            RestoreArmToPlayer();

            if (armLight != null)
            {
                armLight.SetEndgameOverride(false, armBlueColor, 3.0f);
            }

            // Blend smoothly back to PlayerCamera (first-person gameplay)
            if (cutsceneCamera != null)
            {
                cutsceneCamera.Priority.Value = 0;
            }

            if (playerCombat != null) playerCombat.enabled = true;
            if (playerInteractor != null) playerInteractor.IsInteractionBlocked = false;
            if (playerController != null) playerController.SetControlLocked(false);

            currentStage = IntroCutsceneStage.Completed;
        }

        private void ReparentArmToCutsceneCamera()
        {
            if (rightArmFps == null || cutsceneCamera == null || armReparented) return;
            origArmParent = rightArmFps.parent;
            origArmLocalPos = rightArmFps.localPosition;
            origArmLocalRot = rightArmFps.localRotation;

            rightArmFps.SetParent(cutsceneCamera.transform, false);
            armReparented = true;
        }

        private void RestoreArmToPlayer()
        {
            if (!armReparented || rightArmFps == null || origArmParent == null) return;
            rightArmFps.SetParent(origArmParent, false);
            rightArmFps.localPosition = origArmLocalPos;
            rightArmFps.localRotation = origArmLocalRot;
            armReparented = false;
        }

        private Quaternion GetDoorLookRotation(Vector3 fromPos)
        {
            Vector3 target = doorLookTarget != null
                ? doorLookTarget.position
                : new Vector3(9.20f, 44.10f, -2.00f);
            Vector3 dir = target - fromPos;
            return dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
        }

        private void SetDoorUnlockedVisuals(bool unlocked)
        {
            if (emergencyDoorLight != null)
            {
                emergencyDoorLight.enabled = unlocked;
            }

            if (doorLockIndicatorRenderer != null)
            {
                Color c = unlocked ? lockGreenColor : lockRedColor;
                float intensity = unlocked ? 3.2f : 2.2f;
                doorLockIndicatorRenderer.GetPropertyBlock(lockPropBlock);
                lockPropBlock.SetColor(BaseColorId, c);
                lockPropBlock.SetColor(EmissionColorId, c * Mathf.LinearToGammaSpace(intensity));
                doorLockIndicatorRenderer.SetPropertyBlock(lockPropBlock);
            }
        }

        private IEnumerator BlendCameraPose(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                cutsceneCamera.transform.position = Vector3.Lerp(fromPos, toPos, u);
                cutsceneCamera.transform.rotation = Quaternion.Slerp(fromRot, toRot, u);
                yield return null;
            }
            if (!skipRequested)
            {
                cutsceneCamera.transform.position = toPos;
                cutsceneCamera.transform.rotation = toRot;
            }
        }

        private IEnumerator ShowNexusSubtitle(string line, float duration)
        {
            if (subtitleText != null && subtitleGroup != null)
            {
                subtitleText.text = $"<color=#D9A441><b>NEXUS:</b></color> \"{line}\"";
                subtitleGroup.alpha = 1f;
            }
            yield return WaitSecondsSkippable(duration);
            HideSubtitle();
        }

        private void HideSubtitle()
        {
            if (subtitleGroup != null) subtitleGroup.alpha = 0f;
        }

        private IEnumerator WaitSecondsSkippable(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SetBlackAlpha(float alpha)
        {
            if (blackFadeImage != null)
            {
                blackFadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            }
        }

        private void SetPostProcessIntensity(float t)
        {
            t = Mathf.Clamp01(t);
            if (postProcessVolume != null) postProcessVolume.weight = t;
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, 0.58f, t);
            if (chromaticAberration != null) chromaticAberration.intensity.value = Mathf.Lerp(0f, 0.72f, t);
            if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(0f, -0.34f, t);
            if (depthOfField != null)
            {
                depthOfField.active = t > 0.02f;
                depthOfField.gaussianStart.value = Mathf.Lerp(6f, 0.2f, t);
                depthOfField.gaussianEnd.value = Mathf.Lerp(14f, 1.4f, t);
            }
        }

        private void BuildRuntimePostProcessVolume()
        {
            GameObject volGo = new GameObject("IntroCutscenePostProcessVolume");
            volGo.transform.SetParent(transform, false);

            postProcessVolume = volGo.AddComponent<Volume>();
            postProcessVolume.isGlobal = true;
            postProcessVolume.priority = 100f;
            postProcessVolume.weight = 1f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.65f;

            chromaticAberration = profile.Add<ChromaticAberration>(true);
            chromaticAberration.intensity.overrideState = true;

            lensDistortion = profile.Add<LensDistortion>(true);
            lensDistortion.intensity.overrideState = true;

            depthOfField = profile.Add<DepthOfField>(true);
            depthOfField.mode.overrideState = true;
            depthOfField.mode.value = DepthOfFieldMode.Gaussian;
            depthOfField.gaussianStart.overrideState = true;
            depthOfField.gaussianEnd.overrideState = true;
            depthOfField.gaussianMaxRadius.overrideState = true;
            depthOfField.gaussianMaxRadius.value = 1.2f;

            postProcessVolume.profile = profile;
        }

        private void BuildOverlayCanvas()
        {
            GameObject canvasGo = new GameObject("IntroCutsceneOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);

            overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 95;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRt = canvasGo.GetComponent<RectTransform>();

            // Fullscreen black fade
            GameObject blackGo = new GameObject("BlackFade", typeof(RectTransform));
            blackGo.transform.SetParent(rootRt, false);
            StretchFull(blackGo.GetComponent<RectTransform>());
            blackFadeImage = blackGo.AddComponent<Image>();
            blackFadeImage.color = Color.black;
            blackFadeImage.raycastTarget = false;

            // Subtle neural interference strip
            GameObject glitchGo = new GameObject("BootGlitchStrip", typeof(RectTransform));
            glitchGo.transform.SetParent(rootRt, false);
            glitchStripImage = glitchGo.AddComponent<Image>();
            glitchStripImage.color = new Color(0.25f, 0.78f, 0.92f, 0.14f);
            glitchStripImage.raycastTarget = false;
            glitchStripImage.enabled = false;

            // Bottom NEXUS subtitle bar
            GameObject subGo = new GameObject("NexusSubtitleBar", typeof(RectTransform));
            subGo.transform.SetParent(rootRt, false);
            RectTransform subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.10f, 0.03f);
            subRt.anchorMax = new Vector2(0.90f, 0.115f);
            subRt.offsetMin = Vector2.zero;
            subRt.offsetMax = Vector2.zero;
            subtitleGroup = subGo.AddComponent<CanvasGroup>();
            subtitleGroup.alpha = 0f;
            Image subBg = subGo.AddComponent<Image>();
            subBg.color = new Color(0.015f, 0.028f, 0.040f, 0.85f);
            subBg.raycastTarget = false;

            GameObject subTxtGo = new GameObject("SubtitleText", typeof(RectTransform));
            subTxtGo.transform.SetParent(subRt, false);
            StretchFull(subTxtGo.GetComponent<RectTransform>());
            subtitleText = subTxtGo.AddComponent<Text>();
            ConfigureText(subtitleText, string.Empty, 28, TextIce, FontStyle.Normal, TextAnchor.MiddleCenter);

            // Subtle optional skip hint (top-right)
            if (allowHoldSpaceToSkip)
            {
                GameObject skipGo = new GameObject("SkipHint", typeof(RectTransform));
                skipGo.transform.SetParent(rootRt, false);
                RectTransform skipRt = skipGo.GetComponent<RectTransform>();
                skipRt.anchorMin = new Vector2(0.72f, 0.92f);
                skipRt.anchorMax = new Vector2(0.97f, 0.97f);
                skipRt.offsetMin = Vector2.zero;
                skipRt.offsetMax = Vector2.zero;
                skipHintText = skipGo.AddComponent<Text>();
                ConfigureText(skipHintText, "[SEGURE ESPAÇO PARA PULAR]", 18, TextMuted, FontStyle.Normal, TextAnchor.MiddleRight);
            }
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

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
