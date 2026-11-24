using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Eco.Combat;
using Eco.Interaction;
using Eco.Player;

namespace Eco.Endgame
{
    public enum EndgameStage
    {
        LockedBeforeBoss,
        UnlockedAwaitingApproach,
        AccessAvailable,
        Stage3RecordsAndDialogue,
        Stage4WaitingChoice,
        Stage4SyncAndError,
        Stage5BlackoutAndArm,
        Credits
    }

    /// <summary>
    /// Orchestrates the playable Endgame sequence ("A última sincronização") on the 'final'
    /// terminal in Pavimento 01 (Câmara de Prensagem):
    /// - Locked until Carcass-0 is defeated (bossHealth.OnDied or UnlockAfterBossDefeated()).
    /// - Auto-wakes on approach with "SINCRONIZAÇÃO CENTRAL — ACESSO DISPONÍVEL."
    /// - Plays corrupted records + NEXUS/ECO dialogue, single choice [E], 12->48->76% error,
    ///   full chamber + emissive material blackout leaving only ECO's forearm light,
    ///   arm pulse readout, hard cut to black, "ECOS DA MEMÓRIA", and credits.
    /// </summary>
    [RequireComponent(typeof(EndgameTerminalUI))]
    public class EndgameTerminalInteractable : MonoBehaviour, IInteractable
    {
        public static EndgameTerminalInteractable Instance { get; private set; }

        [Header("Boss Gating (Carcass-0)")]
        [Tooltip("Arraste o componente Health do boss Carcass-0 quando ele for adicionado à cena.")]
        [SerializeField] private Health bossHealth;
        [SerializeField] private float postBossSilenceDelay = 2.5f;
        [Tooltip("Ative apenas para testar o final sem precisar derrotar o Carcass-0.")]
        [SerializeField] private bool unlockImmediatelyForTesting = false;

        [Header("Scene References")]
        [SerializeField] private CinemachineCamera terminalCamera;
        [SerializeField] private Light emergencyPathLight;
        [SerializeField] private Light[] chamberLights;
        [SerializeField] private EndgameOverlayUI overlayUI;

        [Header("Camera Choreography")]
        [SerializeField] private int activeCameraPriority = 60;
        [SerializeField] private float baseCameraDistance = 0.76f;
        [SerializeField] private float dialogueVerticalOffset = -0.055f;
        [SerializeField] private float dollyInDistance = 0.44f;
        [SerializeField] private float dollyOutDistance = 1.18f;
        [SerializeField] private float cameraFov = 50f;

        [Header("Approach Settings")]
        [SerializeField] private float autoWakeDistance = 6.0f;
        [SerializeField] private Vector3 indicatorOffsetFromScreen = new Vector3(0f, 0.36f, 0.08f);

        [Header("Emission Colors")]
        [SerializeField] private Color activeCyan = new Color(0.12f, 0.85f, 1.0f);
        [SerializeField] private Color warningAmber = new Color(0.95f, 0.62f, 0.14f);
        [SerializeField] private Color errorRed = new Color(0.95f, 0.14f, 0.12f);

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private EndgameTerminalUI terminalUI;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock screenPropBlock;
        private int screenMaterialIndex = -1;

        private Vector3 screenFrontCenterWorld;
        private Vector3 screenForwardWorld = Vector3.forward;
        private Vector2 screenWorldSize = new Vector2(0.78f, 0.55f);

        private CanvasGroup spatialIndicatorGroup;
        private Transform spatialIndicatorTransform;
        private Transform cachedPlayerTransform;

        private EndgameStage currentStage = EndgameStage.LockedBeforeBoss;
        private bool isBossDefeated;
        private bool choiceConfirmed;
        private Color initialAmbientLight;

        public EndgameStage CurrentStage => currentStage;
        public bool IsBossDefeated => isBossDefeated;
        public EndgameTerminalUI TerminalUI => terminalUI;
        public EndgameOverlayUI OverlayUI => overlayUI;

        public string PromptText => "Acessar Terminal de Sincronização Central";

        public void AssignReferences(CinemachineCamera vcam, Light emergencyLight, Light[] p1Lights, EndgameOverlayUI overlay)
        {
            terminalCamera = vcam;
            emergencyPathLight = emergencyLight;
            chamberLights = p1Lights;
            overlayUI = overlay;
        }

        private void Awake()
        {
            Instance = this;
            terminalUI = GetComponent<EndgameTerminalUI>();
            meshRenderer = GetComponent<MeshRenderer>();
            screenPropBlock = new MaterialPropertyBlock();
            initialAmbientLight = RenderSettings.ambientLight;

            DetectScreenGeometry();
            EnsureTerminalCamera();
            BuildSpatialIndicator();
        }

        private void OnEnable()
        {
            if (bossHealth != null)
            {
                bossHealth.OnDied += HandleBossDied;
            }
        }

        private void OnDisable()
        {
            if (bossHealth != null)
            {
                bossHealth.OnDied -= HandleBossDied;
            }
        }

        private void Start()
        {
            if (overlayUI == null)
            {
                overlayUI = FindAnyObjectByType<EndgameOverlayUI>();
                if (overlayUI == null)
                {
                    GameObject ovGo = new GameObject("EndgameOverlayUI");
                    overlayUI = ovGo.AddComponent<EndgameOverlayUI>();
                }
            }

            terminalUI.Initialize(
                screenFrontCenterWorld,
                screenForwardWorld,
                screenWorldSize,
                ConfirmChoice);

            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) cachedPlayerTransform = pc.transform;

            if (unlockImmediatelyForTesting)
            {
                UnlockImmediately();
            }
            else
            {
                SetLockedVisualState();
            }
        }

        private void Update()
        {
            if (currentStage == EndgameStage.UnlockedAwaitingApproach)
            {
                UpdateApproachAutoWake();
            }

            UpdateSpatialIndicator();

            if (currentStage == EndgameStage.Stage4WaitingChoice && Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame
                    || Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                {
                    ConfirmChoice();
                }
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return isBossDefeated
                && (currentStage == EndgameStage.AccessAvailable || currentStage == EndgameStage.UnlockedAwaitingApproach)
                && interactor != null;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            StartCoroutine(RunEndgameSequenceRoutine(interactor));
        }

        /// <summary>
        /// Called automatically when bossHealth.OnDied fires, or directly by Carcass-0's boss controller.
        /// Keeps the arena silent briefly before unlocking the Central Synchronization Terminal.
        /// </summary>
        public void UnlockAfterBossDefeated()
        {
            if (isBossDefeated) return;
            StartCoroutine(UnlockAfterSilenceRoutine());
        }

        /// <summary>
        /// Immediately unlocks the terminal (useful for Editor/Play Mode testing).
        /// </summary>
        [ContextMenu("Testar: Desbloquear Terminal Final Agora")]
        public void UnlockImmediately()
        {
            isBossDefeated = true;
            currentStage = EndgameStage.UnlockedAwaitingApproach;
            if (emergencyPathLight != null) emergencyPathLight.enabled = true;
            Debug.Log("[Endgame] Terminal de Sincronização Central desbloqueado. Aproxime-se do objeto 'final' no Pavimento 01.");
        }

        public void ConfirmChoiceFromCode()
        {
            if (currentStage == EndgameStage.Stage4WaitingChoice)
            {
                ConfirmChoice();
            }
        }

        private void HandleBossDied()
        {
            UnlockAfterBossDefeated();
        }

        private IEnumerator UnlockAfterSilenceRoutine()
        {
            yield return new WaitForSecondsRealtime(postBossSilenceDelay);
            UnlockImmediately();
        }

        private void SetLockedVisualState()
        {
            isBossDefeated = false;
            currentStage = EndgameStage.LockedBeforeBoss;
            terminalUI.SetMode(EndgameScreenMode.Offline);
            ApplyScreenEmission(Color.black, 0f);
            if (emergencyPathLight != null) emergencyPathLight.enabled = false;
        }

        private void UpdateApproachAutoWake()
        {
            if (cachedPlayerTransform == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) cachedPlayerTransform = pc.transform;
                if (cachedPlayerTransform == null) return;
            }

            float dist = Vector3.Distance(cachedPlayerTransform.position, screenFrontCenterWorld);
            if (dist <= autoWakeDistance)
            {
                currentStage = EndgameStage.AccessAvailable;
                terminalUI.SetMode(EndgameScreenMode.AccessAvailable);
                terminalUI.SetGlitchIntensity(0.12f);
                ApplyScreenEmission(activeCyan, 1.8f);
            }
        }

        private void ConfirmChoice()
        {
            choiceConfirmed = true;
        }

        // =========================================================================
        // Full Playable Endgame Sequence (Etapas 3, 4 e 5)
        // =========================================================================

        private IEnumerator RunEndgameSequenceRoutine(GameObject interactor)
        {
            currentStage = EndgameStage.Stage3RecordsAndDialogue;

            PlayerController playerController = interactor.GetComponent<PlayerController>();
            PlayerInteractor playerInteractor = interactor.GetComponent<PlayerInteractor>();
            ArmHealthLight armLight = interactor.GetComponent<ArmHealthLight>();

            if (playerController != null) playerController.SetControlLocked(true);
            if (playerInteractor != null) playerInteractor.IsInteractionBlocked = true;

            // Focus camera on the Central Synchronization Terminal (slightly offset so bottom dialogue never overlaps the monitor)
            if (terminalCamera != null)
            {
                terminalCamera.transform.position = GetCameraWorldPosition(baseCameraDistance) + Vector3.up * dialogueVerticalOffset;
                terminalCamera.transform.rotation = GetRecommendedCameraWorldRotation();
                terminalCamera.Priority.Value = activeCameraPriority;
            }

            yield return new WaitForSecondsRealtime(0.85f);

            // --- ETAPA 3: O registro final ---
            terminalUI.SetMode(EndgameScreenMode.FinalRecords);
            terminalUI.ClearRecordsText();
            terminalUI.SetGlitchIntensity(0.15f);
            ApplyScreenEmission(activeCyan, 2.1f);

            terminalUI.AppendRecordLine("IDENTIFICAÇÃO DO PROCESSO — ECO.");
            yield return new WaitForSecondsRealtime(1.1f);
            terminalUI.AppendRecordLine("ORIGEM — INDETERMINADA.", true);
            yield return new WaitForSecondsRealtime(1.1f);
            terminalUI.AppendRecordLine("REGISTROS BIOLÓGICOS — INCONSISTENTES.", true);
            yield return new WaitForSecondsRealtime(1.1f);
            terminalUI.AppendRecordLine("REGISTROS SINTÉTICOS — INCONSISTENTES.", true);
            yield return new WaitForSecondsRealtime(1.1f);
            terminalUI.AppendRecordLine("SINCRONIZAÇÃO — PENDENTE.");
            yield return new WaitForSecondsRealtime(1.0f);

            // Display begins glitching as NEXUS speaks
            terminalUI.SetGlitchIntensity(0.45f);
            terminalUI.SetRecordsFooter("INTERFERÊNCIA DETECTADA // NEXUS ONLINE");
            ApplyScreenEmission(warningAmber, 2.3f);

            yield return ShowDialogueLine("NEXUS", "Você chegou.", true, 2.2f);
            yield return new WaitForSecondsRealtime(1.1f);
            yield return ShowDialogueLine("NEXUS", "Eu sabia que chegaria.", true, 2.4f);
            yield return ShowDialogueLine("ECO", "O que eu sou?", false, 2.1f);
            yield return ShowDialogueLine("NEXUS", "Você ainda está perguntando?", true, 2.5f);
            yield return ShowDialogueLine("ECO", "Eu quero saber.", false, 2.0f);
            yield return ShowDialogueLine("NEXUS", "Então sincronize.", true, 2.2f);
            yield return ShowDialogueLine("NEXUS", "Talvez você encontre a resposta.", true, 2.8f);
            overlayUI.HideSubtitle();

            // --- ETAPA 4: A escolha que não é uma escolha ---
            currentStage = EndgameStage.Stage4WaitingChoice;
            choiceConfirmed = false;
            terminalUI.SetMode(EndgameScreenMode.ChoicePrompt);
            terminalUI.SetGlitchIntensity(0.18f);
            ApplyScreenEmission(activeCyan, 2.2f);

            yield return new WaitUntil(() => choiceConfirmed);

            currentStage = EndgameStage.Stage4SyncAndError;
            Cursor.visible = false;
            terminalUI.SetMode(EndgameScreenMode.SyncProgress);
            terminalUI.SetGlitchIntensity(0.22f);
            terminalUI.UpdateSyncProgress(0, "SINCRONIZAÇÃO INICIADA.", "SINCRONIZANDO...", "ESTABELECENDO FREQUÊNCIA NEURAL...", false);

            // Slow dolly-in + pulsing ECO forearm light synced with terminal
            Vector3 startCamPos = GetCameraWorldPosition(baseCameraDistance);
            Vector3 dollyInPos = GetCameraWorldPosition(dollyInDistance);
            float syncDuration = 5.4f;
            float elapsed = 0f;

            while (elapsed < syncDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / syncDuration);

                if (terminalCamera != null)
                {
                    terminalCamera.transform.position = Vector3.Lerp(startCamPos, dollyInPos, Mathf.SmoothStep(0f, 1f, u));
                }

                // Pulse forearm light & terminal emission together
                float pulse = 0.55f + 0.45f * Mathf.Sin(elapsed * 7.5f);
                ApplyScreenEmission(activeCyan, 1.8f + pulse * 1.6f);
                if (armLight != null)
                {
                    armLight.SetEndgameOverride(true, activeCyan, 1.8f + pulse * 2.4f);
                }

                int pct;
                string bottomMsg;
                if (u < 0.28f)
                {
                    pct = Mathf.RoundToInt(Mathf.Lerp(0f, 12f, u / 0.28f));
                    bottomMsg = "SINCRONIZAÇÃO INICIADA.";
                }
                else if (u < 0.62f)
                {
                    pct = Mathf.RoundToInt(Mathf.Lerp(12f, 48f, (u - 0.28f) / 0.34f));
                    bottomMsg = "TRANSFERINDO REGISTROS DE MEMÓRIA...";
                }
                else
                {
                    pct = Mathf.RoundToInt(Mathf.Lerp(48f, 76f, (u - 0.62f) / 0.38f));
                    bottomMsg = "DIVERGÊNCIA DETECTADA NO NÚCLEO...";
                    terminalUI.SetGlitchIntensity(Mathf.Lerp(0.25f, 0.72f, (u - 0.62f) / 0.38f));
                }

                terminalUI.UpdateSyncProgress(pct, "SINCRONIZAÇÃO INICIADA.", $"{pct}%", bottomMsg, false);
                yield return null;
            }

            // Hard error at 76% — NEXUS goes silent
            terminalUI.SetMode(EndgameScreenMode.SyncError);
            terminalUI.SetGlitchIntensity(0.92f);
            terminalUI.UpdateSyncProgress(
                76,
                "ERRO DE IDENTIFICAÇÃO.",
                "PROCESSO INTERROMPIDO",
                "ERRO DE IDENTIFICAÇÃO.\nPROCESSO INTERROMPIDO.",
                true);
            ApplyScreenEmission(errorRed, 3.0f);
            if (armLight != null) armLight.SetEndgameOverride(true, warningAmber, 2.2f);

            yield return new WaitForSecondsRealtime(2.4f);

            // Final distorted illegible message + slow camera dolly-out
            terminalUI.SetMode(EndgameScreenMode.CorruptedFinalMessage);
            terminalUI.SetGlitchIntensity(0.95f);

            Vector3 dollyOutPos = GetCameraWorldPosition(dollyOutDistance);
            float outDur = 3.2f;
            float outElapsed = 0f;
            while (outElapsed < outDur)
            {
                outElapsed += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(outElapsed / outDur));
                if (terminalCamera != null)
                {
                    terminalCamera.transform.position = Vector3.Lerp(dollyInPos, dollyOutPos, u);
                }
                yield return null;
            }

            // --- ETAPA 5: A última imagem (Blackout total: luzes + TODOS os emissivos, exceto o antebraço de ECO) ---
            currentStage = EndgameStage.Stage5BlackoutAndArm;
            terminalUI.SetMode(EndgameScreenMode.Blackout);
            terminalUI.SetGlitchIntensity(0f);

            yield return StartCoroutine(ExtinguishChamberAndEmissivesRoutine(armLight, 1.4f));

            // Return to FIRST-PERSON camera (PlayerCamera inside CameraRoot) so ECO looks down at their own FPS forearm
            // without ever showing the headless/legs-only third-person rig.
            PlayerCombat combat = interactor.GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = false;

            if (terminalCamera != null)
            {
                terminalCamera.Priority.Value = 0;
            }

            Transform camRoot = playerController != null ? playerController.CameraRoot : null;
            Transform rightArmFps = camRoot != null ? camRoot.Find("RightArmFPS") : null;

            if (camRoot != null)
            {
                Quaternion rootStartRot = camRoot.localRotation;
                Quaternion rootEndRot = Quaternion.Euler(24f, 0f, 0f);

                Vector3 armStartPos = rightArmFps != null ? rightArmFps.localPosition : Vector3.zero;
                Quaternion armStartRot = rightArmFps != null ? rightArmFps.localRotation : Quaternion.identity;
                Vector3 armTargetPos = new Vector3(0.10f, -0.15f, 0.36f);
                Quaternion armTargetRot = Quaternion.Euler(14f, 326f, 16f);

                float tiltDur = 2.4f;
                float tiltElapsed = 0f;
                while (tiltElapsed < tiltDur)
                {
                    tiltElapsed += Time.unscaledDeltaTime;
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tiltElapsed / tiltDur));
                    camRoot.localRotation = Quaternion.Slerp(rootStartRot, rootEndRot, u);
                    if (rightArmFps != null)
                    {
                        rightArmFps.localPosition = Vector3.Lerp(armStartPos, armTargetPos, u);
                        rightArmFps.localRotation = Quaternion.Slerp(armStartRot, armTargetRot, u);
                    }
                    yield return null;
                }
            }

            // Forearm light pulses once... then a second time + uninterpretable numeric sequence
            yield return PulseArmWithCrypticNumbers(armLight, "07-419-00 // 8842.19.??", 1.15f);
            yield return new WaitForSecondsRealtime(0.55f);
            yield return PulseArmWithCrypticNumbers(armLight, "0110-9F-004 // ??-76-000 // ERR", 1.35f);

            // Forearm light extinguishes completely
            if (armLight != null)
            {
                armLight.SetEndgameOverride(true, Color.black, 0f);
            }
            overlayUI.ShowForearmCrypticSequence(string.Empty, 0f);
            yield return new WaitForSecondsRealtime(0.35f);

            // Hard cut to black -> Silence -> "ECOS DA MEMÓRIA / O fim da transmissão." -> Credits
            currentStage = EndgameStage.Credits;
            yield return StartCoroutine(overlayUI.PlayTitleAndCreditsRoutine());
        }

        private IEnumerator ShowDialogueLine(string speaker, string line, bool isNexus, float duration)
        {
            overlayUI.ShowSubtitle(speaker, line, isNexus);
            yield return new WaitForSecondsRealtime(duration);
            overlayUI.HideSubtitle();
            yield return new WaitForSecondsRealtime(0.35f);
        }

        private IEnumerator PulseArmWithCrypticNumbers(ArmHealthLight armLight, string code, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                if (armLight != null)
                {
                    armLight.SetEndgameOverride(true, activeCyan, 0.35f + wave * 3.8f);
                }
                overlayUI.ShowForearmCrypticSequence(code, wave * 0.92f);
                yield return null;
            }
            if (armLight != null) armLight.SetEndgameOverride(true, activeCyan, 0.2f);
            overlayUI.ShowForearmCrypticSequence(string.Empty, 0f);
        }

        /// <summary>
        /// Extinguishes all chamber lights, ambient light, AND every emissive Renderer in the scene
        /// via MaterialPropertyBlock (leaving the original .mat assets untouched) EXCEPT ECO's forearm light.
        /// </summary>
        private IEnumerator ExtinguishChamberAndEmissivesRoutine(ArmHealthLight armLight, float fadeDuration)
        {
            // Collect protected forearm renderers so ONLY the player's arm stays illuminated
            HashSet<Renderer> protectedRenderers = new HashSet<Renderer>();
            if (armLight != null && armLight.LightRenderers != null)
            {
                foreach (Renderer r in armLight.LightRenderers)
                {
                    if (r != null) protectedRenderers.Add(r);
                }
            }

            // Collect lights to extinguish
            List<Light> lightsToDim = new List<Light>();
            List<float> startIntensities = new List<float>();

            if (chamberLights != null)
            {
                foreach (Light l in chamberLights)
                {
                    if (l != null && !lightsToDim.Contains(l))
                    {
                        lightsToDim.Add(l);
                        startIntensities.Add(l.intensity);
                    }
                }
            }

            if (emergencyPathLight != null && !lightsToDim.Contains(emergencyPathLight))
            {
                lightsToDim.Add(emergencyPathLight);
                startIntensities.Add(emergencyPathLight.intensity);
            }

            // Also grab any other active lights in Pavimento 1 (y < 15)
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l != null && l.transform.position.y < 15f && !lightsToDim.Contains(l))
                {
                    lightsToDim.Add(l);
                    startIntensities.Add(l.intensity);
                }
            }

            // Dim all scene emissive materials via MaterialPropertyBlock (never mutates shared .mat assets)
            Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / fadeDuration);

                for (int i = 0; i < lightsToDim.Count; i++)
                {
                    if (lightsToDim[i] != null) lightsToDim[i].intensity = startIntensities[i] * t;
                }

                RenderSettings.ambientLight = Color.Lerp(Color.black, initialAmbientLight, t);

                if (armLight != null)
                {
                    armLight.SetEndgameOverride(true, activeCyan, 2.6f);
                }

                yield return null;
            }

            foreach (Light l in lightsToDim)
            {
                if (l != null)
                {
                    l.intensity = 0f;
                    l.enabled = false;
                }
            }

            RenderSettings.ambientLight = Color.black;

            // Zero out emission on all non-arm renderers so no emissive material lights up the dark room
            foreach (Renderer r in allRenderers)
            {
                if (r == null || protectedRenderers.Contains(r)) continue;
                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].HasProperty(EmissionColorId))
                    {
                        r.GetPropertyBlock(mpb, i);
                        mpb.SetColor(EmissionColorId, Color.black);
                        r.SetPropertyBlock(mpb, i);
                    }
                }
            }

            // Keep ONLY ECO's forearm glowing softly in the pitch dark
            if (armLight != null)
            {
                armLight.SetEndgameOverride(true, activeCyan, 2.4f);
            }
        }

        // =========================================================================
        // Geometry, Camera & Spatial Indicator Helpers
        // =========================================================================

        private void ApplyScreenEmission(Color color, float intensity)
        {
            if (meshRenderer == null || screenMaterialIndex < 0) return;
            meshRenderer.GetPropertyBlock(screenPropBlock, screenMaterialIndex);
            screenPropBlock.SetColor(EmissionColorId, intensity > 0f ? color * Mathf.LinearToGammaSpace(intensity) : Color.black);
            meshRenderer.SetPropertyBlock(screenPropBlock, screenMaterialIndex);
        }

        public void DetectScreenGeometry()
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null)
            {
                screenFrontCenterWorld = transform.position + Vector3.forward * 0.42f;
                screenForwardWorld = Vector3.forward;
                screenWorldSize = new Vector2(0.78f, 0.55f);
                return;
            }

            Material[] mats = mr.sharedMaterials;
            screenMaterialIndex = -1;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.Contains("Cyan_Diegetic"))
                {
                    screenMaterialIndex = i;
                    break;
                }
            }

            if (screenMaterialIndex < 0 || screenMaterialIndex >= mf.sharedMesh.subMeshCount)
            {
                Bounds b = mr.bounds;
                screenFrontCenterWorld = new Vector3(b.center.x, b.center.y, b.max.z);
                screenForwardWorld = Vector3.forward;
                screenWorldSize = new Vector2(0.78f, 0.55f);
                return;
            }

            int[] tris = mf.sharedMesh.GetTriangles(screenMaterialIndex);
            Vector3[] verts = mf.sharedMesh.vertices;

            Vector3 minW = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maxW = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < tris.Length; i++)
            {
                Vector3 w = transform.TransformPoint(verts[tris[i]]);
                minW = Vector3.Min(minW, w);
                maxW = Vector3.Max(maxW, w);
            }

            Vector3 center = (minW + maxW) * 0.5f;
            Vector3 size = maxW - minW;
            screenForwardWorld = (center.z >= transform.position.z) ? Vector3.forward : Vector3.back;
            float frontZ = (screenForwardWorld.z > 0f) ? maxW.z : minW.z;
            screenFrontCenterWorld = new Vector3(center.x, center.y, frontZ);
            screenWorldSize = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
        }

        public Vector3 GetCameraWorldPosition(float distance)
        {
            return screenFrontCenterWorld + screenForwardWorld.normalized * distance;
        }

        public Quaternion GetRecommendedCameraWorldRotation()
        {
            return Quaternion.LookRotation(-screenForwardWorld.normalized, Vector3.up);
        }

        private void EnsureTerminalCamera()
        {
            if (terminalCamera == null)
            {
                terminalCamera = GetComponentInChildren<CinemachineCamera>(true);
            }

            if (terminalCamera == null)
            {
                GameObject camGo = new GameObject("TerminalCamera_Final");
                camGo.transform.SetParent(transform, true);
                camGo.transform.position = GetCameraWorldPosition(baseCameraDistance);
                camGo.transform.rotation = GetRecommendedCameraWorldRotation();
                terminalCamera = camGo.AddComponent<CinemachineCamera>();
            }

            terminalCamera.Lens.FieldOfView = cameraFov;
            terminalCamera.Priority.Value = 0;
        }

        private void BuildSpatialIndicator()
        {
            GameObject indGo = new GameObject("SpatialFinalIndicator");
            indGo.transform.SetParent(transform, true);
            indGo.transform.position = screenFrontCenterWorld + indicatorOffsetFromScreen;
            spatialIndicatorTransform = indGo.transform;

            Canvas c = indGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = 10;

            RectTransform rt = indGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240f, 52f);

            Vector3 pScale = transform.lossyScale;
            float s = 0.0014f;
            indGo.transform.localScale = new Vector3(
                s / (Mathf.Approximately(pScale.x, 0f) ? 1f : pScale.x),
                s / (Mathf.Approximately(pScale.y, 0f) ? 1f : pScale.y),
                s / (Mathf.Approximately(pScale.z, 0f) ? 1f : pScale.z));

            spatialIndicatorGroup = indGo.AddComponent<CanvasGroup>();
            spatialIndicatorGroup.alpha = 0f;
            spatialIndicatorGroup.interactable = false;
            spatialIndicatorGroup.blocksRaycasts = false;

            Image bg = indGo.AddComponent<Image>();
            bg.color = new Color(0.043f, 0.082f, 0.114f, 0.90f);
            bg.raycastTarget = false;

            GameObject lblGo = new GameObject("BadgeText", typeof(RectTransform));
            lblGo.transform.SetParent(rt, false);
            RectTransform lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;

            Text txt = lblGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = "[ E ]  SINCRONIZAÇÃO";
            txt.fontSize = 19;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.298f, 0.788f, 0.910f, 1f);
            txt.raycastTarget = false;
        }

        private void UpdateSpatialIndicator()
        {
            if (spatialIndicatorGroup == null || spatialIndicatorTransform == null) return;

            float targetAlpha = 0f;
            if (currentStage == EndgameStage.AccessAvailable && cachedPlayerTransform != null)
            {
                float dist = Vector3.Distance(cachedPlayerTransform.position, screenFrontCenterWorld);
                if (dist <= autoWakeDistance)
                {
                    targetAlpha = Mathf.InverseLerp(autoWakeDistance, autoWakeDistance * 0.55f, dist);
                }
            }

            spatialIndicatorGroup.alpha = Mathf.MoveTowards(spatialIndicatorGroup.alpha, targetAlpha, Time.deltaTime * 4f);
            if (spatialIndicatorGroup.alpha > 0.01f && Camera.main != null)
            {
                float bob = Mathf.Sin(Time.time * 2.6f) * 0.018f;
                spatialIndicatorTransform.position = screenFrontCenterWorld + indicatorOffsetFromScreen + Vector3.up * bob;
                Vector3 toCam = Camera.main.transform.position - spatialIndicatorTransform.position;
                if (toCam.sqrMagnitude > 0.001f)
                {
                    spatialIndicatorTransform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                }
            }
        }
    }
}
