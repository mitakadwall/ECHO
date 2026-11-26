using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using Eco.Interaction;
using Eco.Player;

namespace Eco.Tutorial
{
    /// <summary>
    /// Diegetic M1 protocol terminal attached to 'monitor M1__exp'.
    /// Matches SaveTerminalInteractable: proximity badge, Cinemachine zoom to the monitor,
    /// player control lock while using the terminal, and triggers TutorialM1SequenceController
    /// when the operator starts the test sequence.
    /// </summary>
    [RequireComponent(typeof(TutorialTerminalUI))]
    public class TutorialM1TerminalInteractable : MonoBehaviour, IInteractable
    {
        [Header("Sequence Reference")]
        [SerializeField] private TutorialM1SequenceController sequenceController;

        [Header("Camera Focus")]
        [SerializeField] private CinemachineCamera terminalCamera;
        [SerializeField] private int activeCameraPriority = 50;
        [SerializeField] private int inactiveCameraPriority = 0;
        [SerializeField] private float cameraDistanceFromScreen = 0.62f;
        [SerializeField] private float cameraFov = 50f;
        [SerializeField] private float exitControlRestoreDelay = 0.45f;

        [Header("Screen Geometry")]
        [SerializeField] private bool useManualScreenGeometry = false;
        [SerializeField] private Vector3 manualScreenFrontCenterWorld = new Vector3(-6.503f, 1.65f, 2.303f);
        [SerializeField] private Vector3 manualScreenForwardWorld = new Vector3(0.7071068f, 0f, -0.7071068f);
        [SerializeField] private Vector2 manualScreenWorldSize = new Vector2(0.78f, 0.55f);

        [Header("Spatial Indicator (Approach)")]
        [SerializeField] private float indicatorShowDistance = 5.5f;
        [SerializeField] private Vector3 indicatorOffsetFromScreen = new Vector3(0f, 0.38f, 0f);

        private TutorialTerminalUI terminalUI;
        private Vector3 screenFrontCenterWorld;
        private Vector3 screenForwardWorld = Vector3.right;
        private Vector2 screenWorldSize = new Vector2(0.90f, 0.72f);

        private CanvasGroup spatialIndicatorGroup;
        private Transform spatialIndicatorTransform;
        private Transform cachedPlayerTransform;

        private PlayerController activePlayerController;
        private PlayerInteractor activePlayerInteractor;
        private bool isTerminalOpen;

        public bool IsTerminalOpen => isTerminalOpen;
        public TutorialTerminalUI TerminalUI => terminalUI;
        public string PromptText => "Acessar Terminal — Protocolo de Operação";

        public void AssignSequenceController(TutorialM1SequenceController controller)
        {
            sequenceController = controller;
        }

        public void ConfigureScreenGeometry(Vector3 frontCenterWorld, Vector3 forwardWorld, Vector2 worldSize, float camDist = 0.62f)
        {
            useManualScreenGeometry = true;
            manualScreenFrontCenterWorld = frontCenterWorld;
            manualScreenForwardWorld = forwardWorld.normalized;
            manualScreenWorldSize = worldSize;
            cameraDistanceFromScreen = camDist;
        }

        private void Awake()
        {
            terminalUI = GetComponent<TutorialTerminalUI>();
            DetectScreenGeometry();
            EnsureTerminalCamera();
            BuildSpatialIndicator();
        }

        private void Start()
        {
            if (sequenceController == null)
            {
                sequenceController = FindAnyObjectByType<TutorialM1SequenceController>();
            }

            terminalUI.Initialize(
                screenFrontCenterWorld,
                screenForwardWorld,
                screenWorldSize,
                HandleStartSequenceRequested,
                HandleDisconnectRequested);

            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) cachedPlayerTransform = pc.transform;
        }

        private void Update()
        {
            UpdateSpatialIndicator();
        }

        public bool CanInteract(GameObject interactor)
        {
            return !isTerminalOpen && interactor != null;
        }

        public void Interact(GameObject interactor)
        {
            if (isTerminalOpen || interactor == null) return;

            activePlayerController = interactor.GetComponent<PlayerController>();
            activePlayerInteractor = interactor.GetComponent<PlayerInteractor>();
            isTerminalOpen = true;

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

            terminalUI.SetFocused(true);
        }

        private void HandleStartSequenceRequested()
        {
            terminalUI.MarkSequenceStarted();

            if (sequenceController != null)
            {
                sequenceController.StartSequence();
            }

            StartCoroutine(AutoExitAfterSequenceStart());
        }

        private IEnumerator AutoExitAfterSequenceStart()
        {
            yield return new WaitForSecondsRealtime(0.75f);
            HandleDisconnectRequested();
        }

        private void HandleDisconnectRequested()
        {
            if (!isTerminalOpen) return;

            isTerminalOpen = false;
            terminalUI.SetFocused(false);

            if (terminalCamera != null)
            {
                terminalCamera.Priority.Value = inactiveCameraPriority;
            }

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

            activePlayerController = null;
            activePlayerInteractor = null;
        }

        private void DetectScreenGeometry()
        {
            if (useManualScreenGeometry)
            {
                screenFrontCenterWorld = manualScreenFrontCenterWorld;
                screenForwardWorld = manualScreenForwardWorld.sqrMagnitude > 0.001f
                    ? manualScreenForwardWorld.normalized
                    : Vector3.forward;
                screenWorldSize = manualScreenWorldSize;
                return;
            }

            MeshFilter mf = GetComponent<MeshFilter>();
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null)
            {
                screenFrontCenterWorld = transform.position + Vector3.right * 0.22f;
                screenForwardWorld = Vector3.right;
                screenWorldSize = new Vector2(0.90f, 0.72f);
                return;
            }

            // Locate the cyan screen submesh if readable, otherwise fall back to renderer bounds
            Material[] mats = mr.sharedMaterials;
            int screenMatIdx = -1;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.Contains("Cyan_Diegetic"))
                {
                    screenMatIdx = i;
                    break;
                }
            }

            Vector3 minW;
            Vector3 maxW;
            if (screenMatIdx >= 0 && screenMatIdx < mf.sharedMesh.subMeshCount && mf.sharedMesh.isReadable)
            {
                int[] tris = mf.sharedMesh.GetTriangles(screenMatIdx);
                Vector3[] verts = mf.sharedMesh.vertices;
                minW = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                maxW = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int i = 0; i < tris.Length; i++)
                {
                    Vector3 w = transform.TransformPoint(verts[tris[i]]);
                    minW = Vector3.Min(minW, w);
                    maxW = Vector3.Max(maxW, w);
                }
            }
            else
            {
                Bounds b = mr.bounds;
                minW = b.min;
                maxW = b.max;
            }

            Vector3 center = (minW + maxW) * 0.5f;
            Vector3 size = maxW - minW;

            // Determine whether the screen faces along X or Z (thinnest horizontal axis)
            if (size.x <= size.z)
            {
                // Monitor mounted on X wall (monitor M1__exp is at x=-7.55 facing +X toward the room center x=-4)
                screenForwardWorld = (center.x <= -4.0f) ? Vector3.right : Vector3.left;
                float frontX = (screenForwardWorld.x > 0f) ? maxW.x : minW.x;

                // When falling back to full renderer bounds (mesh not readable), the cyan display occupies
                // the upper screen housing (width ~size.z * 0.92, 16:11.3 aspect ratio matching 960x680 canvas).
                float screenW = Mathf.Max(0.25f, size.z * 0.92f);
                float screenH = (mf.sharedMesh.isReadable && screenMatIdx >= 0)
                    ? Mathf.Max(0.25f, size.y)
                    : Mathf.Min(size.y * 0.55f, screenW * (680f / 960f));
                float screenCenterY = (mf.sharedMesh.isReadable && screenMatIdx >= 0)
                    ? center.y
                    : maxW.y - (screenH * 0.5f) - 0.06f;

                screenFrontCenterWorld = new Vector3(frontX, screenCenterY, center.z);
                screenWorldSize = new Vector2(screenW, screenH);
            }
            else
            {
                screenForwardWorld = (center.z >= transform.position.z) ? Vector3.forward : Vector3.back;
                float frontZ = (screenForwardWorld.z > 0f) ? maxW.z : minW.z;
                screenFrontCenterWorld = new Vector3(center.x, center.y, frontZ);
                screenWorldSize = new Vector2(Mathf.Max(0.25f, size.x), Mathf.Max(0.25f, size.y));
            }
        }

        private void EnsureTerminalCamera()
        {
            if (terminalCamera == null)
            {
                terminalCamera = GetComponentInChildren<CinemachineCamera>(true);
            }

            if (terminalCamera == null)
            {
                GameObject camGo = new GameObject("TerminalCamera_M1");
                camGo.transform.SetParent(transform, true);
                camGo.transform.position = screenFrontCenterWorld + screenForwardWorld.normalized * cameraDistanceFromScreen;
                camGo.transform.rotation = Quaternion.LookRotation(-screenForwardWorld.normalized, Vector3.up);
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
            rt.sizeDelta = new Vector2(200f, 52f);

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
    }
}
