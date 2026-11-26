using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Eco.Interaction;
using Eco.Player;

namespace Eco.Tutorial
{
    /// <summary>
    /// Final M7 protocol terminal attached to 'monitor M7__exp'.
    /// Unlocks and opens 'saida M7__exp' when the operator chooses ENCERRAR PROTOCOLO,
    /// then smoothly returns to the MainMenu scene.
    /// </summary>
    [RequireComponent(typeof(TutorialM7TerminalUI))]
    public class TutorialM7TerminalInteractable : MonoBehaviour, IInteractable
    {
        [Header("Exit Door & Return")]
        [SerializeField] private Transform exitDoorM7;
        [SerializeField] private float returnToMenuDelay = 1.8f;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private Color doorUnlockedColor = new Color(0.10f, 0.95f, 0.45f, 1f);
        [SerializeField] private Color doorUnlockedEmission = new Color(0.36f, 3.58f, 1.61f, 1f);

        [Header("Camera Focus")]
        [SerializeField] private CinemachineCamera terminalCamera;
        [SerializeField] private int activeCameraPriority = 50;
        [SerializeField] private int inactiveCameraPriority = 0;
        [SerializeField] private float cameraDistanceFromScreen = 0.62f;
        [SerializeField] private float cameraFov = 50f;
        [SerializeField] private float exitControlRestoreDelay = 0.45f;

        [Header("Screen Geometry")]
        [SerializeField] private Vector3 screenFrontCenterWorld = new Vector3(-166.38f, 4.85f, 0.0f);
        [SerializeField] private Vector3 screenForwardWorld = Vector3.right;
        [SerializeField] private Vector2 screenWorldSize = new Vector2(0.78f, 0.55f);

        [Header("Spatial Indicator (Approach)")]
        [SerializeField] private float indicatorShowDistance = 5.5f;
        [SerializeField] private Vector3 indicatorOffsetFromScreen = new Vector3(0f, 0.38f, 0f);

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private TutorialM7TerminalUI terminalUI;
        private CanvasGroup spatialIndicatorGroup;
        private Transform spatialIndicatorTransform;
        private Transform cachedPlayerTransform;

        private PlayerController activePlayerController;
        private PlayerInteractor activePlayerInteractor;
        private Vector3 doorClosedLocalPos;
        private bool isTerminalOpen;
        private bool isFinishing;

        public bool IsTerminalOpen => isTerminalOpen;
        public bool IsFinishing => isFinishing;
        public TutorialM7TerminalUI TerminalUI => terminalUI;
        public string PromptText => "Acessar Terminal — Encerrar Protocolo";

        public void Configure(Transform exitDoor, Vector3 frontCenter, Vector3 forward, Vector2 size)
        {
            exitDoorM7 = exitDoor;
            screenFrontCenterWorld = frontCenter;
            screenForwardWorld = forward.normalized;
            screenWorldSize = size;
        }

        private void Awake()
        {
            terminalUI = GetComponent<TutorialM7TerminalUI>();
            if (exitDoorM7 != null)
            {
                doorClosedLocalPos = exitDoorM7.localPosition;
            }
            EnsureTerminalCamera();
            BuildSpatialIndicator();
        }

        private void Start()
        {
            terminalUI.Initialize(
                screenFrontCenterWorld,
                screenForwardWorld,
                screenWorldSize,
                HandleEndProtocolRequested,
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
            return !isTerminalOpen && !isFinishing && interactor != null;
        }

        public void Interact(GameObject interactor)
        {
            if (isTerminalOpen || isFinishing || interactor == null) return;

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

        private void HandleEndProtocolRequested()
        {
            if (isFinishing) return;
            isFinishing = true;
            terminalUI.MarkProtocolFinished();
            StartCoroutine(EndProtocolSequenceRoutine());
        }

        private IEnumerator EndProtocolSequenceRoutine()
        {
            yield return new WaitForSecondsRealtime(0.45f);

            // Release camera so the player sees saida M7 turn bright green
            isTerminalOpen = false;
            terminalUI.SetFocused(false);
            if (terminalCamera != null)
            {
                terminalCamera.Priority.Value = inactiveCameraPriority;
            }

            // Switch saida M7 indicator from red to bright green (no sliding animation)
            UnlockExitDoorColor();

            yield return new WaitForSecondsRealtime(returnToMenuDelay);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        private void UnlockExitDoorColor()
        {
            if (exitDoorM7 == null) return;
            Renderer r = exitDoorM7.GetComponent<Renderer>();
            if (r == null) return;

            Material[] mats = r.sharedMaterials;
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.Contains("Red_NEXUS"))
                {
                    r.GetPropertyBlock(propBlock, i);
                    propBlock.SetColor(BaseColorId, doorUnlockedColor);
                    propBlock.SetColor(EmissionColorId, doorUnlockedEmission);
                    r.SetPropertyBlock(propBlock, i);
                }
            }
        }

        private void HandleDisconnectRequested()
        {
            if (!isTerminalOpen || isFinishing) return;

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

        private void EnsureTerminalCamera()
        {
            if (terminalCamera == null)
            {
                terminalCamera = GetComponentInChildren<CinemachineCamera>(true);
            }

            if (terminalCamera == null)
            {
                GameObject camGo = new GameObject("TerminalCamera_M7");
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
            GameObject indGo = new GameObject("SpatialInteractIndicator_M7");
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
            if (!isTerminalOpen && !isFinishing && cachedPlayerTransform != null)
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
