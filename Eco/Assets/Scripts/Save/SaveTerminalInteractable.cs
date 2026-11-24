using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using Eco.Interaction;
using Eco.Player;

namespace Eco.Save
{
    /// <summary>
    /// Diegetic save terminal attached to save 1, save 2, save 3, and save 4.
    /// Handles proximity spatial indicator, Cinemachine camera zoom to the 3D monitor,
    /// player control locking, screen emission glow, and real save persistence via EcoSaveManager.
    /// </summary>
    [RequireComponent(typeof(SaveTerminalUI))]
    public class SaveTerminalInteractable : MonoBehaviour, IInteractable
    {
        [Header("Terminal Identity")]
        [SerializeField, Range(1, 4)] private int terminalIndex = 1;
        [SerializeField] private SaveTerminalProfile profile;

        [Header("Camera Focus")]
        [SerializeField] private CinemachineCamera terminalCamera;
        [SerializeField] private int activeCameraPriority = 50;
        [SerializeField] private int inactiveCameraPriority = 0;
        [SerializeField] private float cameraDistanceFromScreen = 0.62f;
        [SerializeField] private float cameraFov = 50f;
        [SerializeField] private float exitControlRestoreDelay = 0.55f;

        [Header("Spatial Indicator (Approach)")]
        [SerializeField] private float indicatorShowDistance = 5.5f;
        [SerializeField] private Vector3 indicatorOffsetFromScreen = new Vector3(0f, 0.36f, 0.08f);

        [Header("Screen Emission")]
        [SerializeField] private Color standbyEmissionColor = new Color(0.06f, 0.30f, 0.38f);
        [SerializeField] private float standbyEmissionIntensity = 0.45f;
        [SerializeField] private Color activeEmissionColor = new Color(0.12f, 0.85f, 1.0f);
        [SerializeField] private float activeEmissionIntensity = 2.0f;
        [SerializeField] private Color nexusEmissionColor = new Color(0.95f, 0.62f, 0.14f);
        [SerializeField] private float nexusEmissionIntensity = 2.4f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private SaveTerminalUI terminalUI;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock screenPropBlock;
        private int screenMaterialIndex = -1;

        private Vector3 screenFrontCenterWorld;
        private Vector3 screenForwardWorld = Vector3.forward;
        private Vector2 screenWorldSize = new Vector2(0.78f, 0.55f);

        private CanvasGroup spatialIndicatorGroup;
        private Transform spatialIndicatorTransform;

        private GameObject activeInteractor;
        private PlayerController activePlayerController;
        private PlayerInteractor activePlayerInteractor;
        private Transform cachedPlayerTransform;
        private bool isTerminalOpen;
        private bool hasBeenActivated;

        public int TerminalIndex => terminalIndex;
        public SaveTerminalProfile Profile => profile;
        public bool IsTerminalOpen => isTerminalOpen;
        public SaveTerminalUI TerminalUI => terminalUI;

        public string PromptText => "Acessar Terminal de Sincronização";

        public void ConfigureTerminalIndex(int index)
        {
            terminalIndex = Mathf.Clamp(index, 1, 4);
            profile = SaveTerminalPresets.CreateForTerminalIndex(terminalIndex);
        }

        public void AssignTerminalCamera(CinemachineCamera cam)
        {
            terminalCamera = cam;
        }

        private void Awake()
        {
            terminalUI = GetComponent<SaveTerminalUI>();
            meshRenderer = GetComponent<MeshRenderer>();
            screenPropBlock = new MaterialPropertyBlock();

            if (profile == null || string.IsNullOrEmpty(profile.terminalId) || profile.records == null || profile.records.Count == 0)
            {
                profile = SaveTerminalPresets.CreateForTerminalIndex(terminalIndex);
            }

            DetectScreenGeometry();
            EnsureTerminalCamera();
            BuildSpatialIndicator();
        }

        private void Start()
        {
            terminalUI.Initialize(
                profile,
                screenFrontCenterWorld,
                screenForwardWorld,
                screenWorldSize,
                HandleSyncRequested,
                HandleDisconnectRequested);

            bool synced = EcoSaveManager.Instance != null && EcoSaveManager.Instance.IsTerminalSynced(profile.terminalId);
            hasBeenActivated = synced;
            ApplyScreenEmission(synced ? activeEmissionColor : standbyEmissionColor, synced ? activeEmissionIntensity * 0.65f : standbyEmissionIntensity);

            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) cachedPlayerTransform = pc.transform;
        }

        private void Update()
        {
            UpdateSpatialIndicator();
            UpdateDynamicEmission();
        }

        public bool CanInteract(GameObject interactor)
        {
            return !isTerminalOpen && interactor != null;
        }

        public void Interact(GameObject interactor)
        {
            if (isTerminalOpen || interactor == null) return;

            activeInteractor = interactor;
            activePlayerController = interactor.GetComponent<PlayerController>();
            activePlayerInteractor = interactor.GetComponent<PlayerInteractor>();

            isTerminalOpen = true;
            hasBeenActivated = true;

            if (activePlayerController != null)
            {
                activePlayerController.SetControlLocked(true);
            }

            if (activePlayerInteractor != null)
            {
                activePlayerInteractor.IsInteractionBlocked = true;
            }

            if (terminalCamera != null)
            {
                terminalCamera.Priority.Value = activeCameraPriority;
            }

            bool synced = EcoSaveManager.Instance != null && EcoSaveManager.Instance.IsTerminalSynced(profile.terminalId);
            terminalUI.EnterTerminal(synced);
        }

        private void HandleSyncRequested()
        {
            if (EcoSaveManager.Instance == null)
            {
                GameObject mgrGo = new GameObject("EcoSaveManager");
                mgrGo.AddComponent<EcoSaveManager>();
            }

            EcoSaveManager.Instance.SaveFromTerminal(profile.terminalId, profile.floorLabel, activeInteractor);
        }

        private void HandleDisconnectRequested()
        {
            if (!isTerminalOpen) return;

            isTerminalOpen = false;
            terminalUI.ExitTerminalKeepPowered();

            if (terminalCamera != null)
            {
                terminalCamera.Priority.Value = inactiveCameraPriority;
            }

            ApplyScreenEmission(activeEmissionColor, activeEmissionIntensity * 0.7f);
            StartCoroutine(RestorePlayerControlAfterBlend());
        }

        private IEnumerator RestorePlayerControlAfterBlend()
        {
            yield return new WaitForSecondsRealtime(exitControlRestoreDelay);

            if (activePlayerController != null)
            {
                activePlayerController.SetControlLocked(false);
            }

            if (activePlayerInteractor != null)
            {
                activePlayerInteractor.IsInteractionBlocked = false;
            }

            activeInteractor = null;
            activePlayerController = null;
            activePlayerInteractor = null;
        }

        private void UpdateSpatialIndicator()
        {
            if (spatialIndicatorGroup == null || spatialIndicatorTransform == null) return;

            if (cachedPlayerTransform == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) cachedPlayerTransform = pc.transform;
            }

            float targetAlpha = 0f;
            if (!isTerminalOpen && cachedPlayerTransform != null)
            {
                float dist = Vector3.Distance(cachedPlayerTransform.position, screenFrontCenterWorld);
                if (dist <= indicatorShowDistance)
                {
                    targetAlpha = Mathf.InverseLerp(indicatorShowDistance, indicatorShowDistance * 0.55f, dist);
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

        private void UpdateDynamicEmission()
        {
            if (screenMaterialIndex < 0) return;

            if (isTerminalOpen)
            {
                if (terminalUI != null && terminalUI.CurrentState == TerminalScreenState.NexusMessage)
                {
                    float pulse = nexusEmissionIntensity + Mathf.Sin(Time.unscaledTime * 7f) * 0.35f;
                    ApplyScreenEmission(nexusEmissionColor, pulse);
                }
                else
                {
                    float pulse = activeEmissionIntensity + Mathf.Sin(Time.unscaledTime * 3.5f) * 0.15f;
                    ApplyScreenEmission(activeEmissionColor, pulse);
                }
            }
            else if (hasBeenActivated)
            {
                ApplyScreenEmission(activeEmissionColor, activeEmissionIntensity * 0.65f);
            }
        }

        private void ApplyScreenEmission(Color color, float intensity)
        {
            if (meshRenderer == null || screenMaterialIndex < 0) return;

            meshRenderer.GetPropertyBlock(screenPropBlock, screenMaterialIndex);
            screenPropBlock.SetColor(EmissionColorId, color * Mathf.LinearToGammaSpace(intensity));
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
                screenFrontCenterWorld = new Vector3(b.center.x, b.center.y + 0.25f, b.max.z);
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

            // Screen is thin on Z (0.06m) and faces +Z relative to object center
            screenForwardWorld = (center.z >= transform.position.z) ? Vector3.forward : Vector3.back;
            float frontZ = (screenForwardWorld.z > 0f) ? maxW.z : minW.z;
            screenFrontCenterWorld = new Vector3(center.x, center.y, frontZ);
            screenWorldSize = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
        }

        public Vector3 GetRecommendedCameraWorldPosition()
        {
            return screenFrontCenterWorld + screenForwardWorld.normalized * cameraDistanceFromScreen;
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
                GameObject camGo = new GameObject($"TerminalCamera_Save{terminalIndex}");
                camGo.transform.SetParent(transform, true);
                camGo.transform.position = GetRecommendedCameraWorldPosition();
                camGo.transform.rotation = GetRecommendedCameraWorldRotation();

                terminalCamera = camGo.AddComponent<CinemachineCamera>();
            }

            terminalCamera.Lens.FieldOfView = cameraFov;
            terminalCamera.Priority.Value = inactiveCameraPriority;
        }

        private void BuildSpatialIndicator()
        {
            GameObject indGo = new GameObject("SpatialInteractIndicator");
            indGo.transform.SetParent(transform, true);
            indGo.transform.position = screenFrontCenterWorld + indicatorOffsetFromScreen;
            spatialIndicatorTransform = indGo.transform;

            Canvas c = indGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = 10;

            RectTransform rt = indGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180f, 52f);

            // World scale ~ 0.24m wide
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
            txt.text = "[ E ]  TERMINAL";
            txt.fontSize = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.298f, 0.788f, 0.910f, 1f);
            txt.raycastTarget = false;
        }
    }
}
