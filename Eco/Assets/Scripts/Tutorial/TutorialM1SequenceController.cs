using System.Collections;
using UnityEngine;

namespace Eco.Tutorial
{
    /// <summary>
    /// Controls the M1 opening sequence when the operator starts the test protocol:
    /// 1) Unlocks the front door ('saida M1__exp'), switching its red lock bar to blue/cyan and sliding it open.
    /// 2) Sends a bright blue/cyan light pulse sweeping along the floor guide ('linha M1' -> 'linha entrada M2')
    ///    to guide the player into the first module.
    /// </summary>
    public class TutorialM1SequenceController : MonoBehaviour
    {
        [Header("Scene Objects")]
        [SerializeField] private Transform frontDoor;
        [SerializeField] private Renderer linhaM1Renderer;
        [SerializeField] private Renderer linhaEntradaM2Renderer;

        [Header("Door Unlock & Open")]
        [SerializeField] private Vector3 doorOpenOffset = new Vector3(0f, 3.85f, 0f);
        [SerializeField] private float doorOpenDuration = 1.45f;
        [SerializeField] private Color doorUnlockedEmission = new Color(0.15f, 0.85f, 1.0f, 1f);
        [SerializeField] private float doorUnlockedEmissionIntensity = 3.2f;

        [Header("Floor Blue Light Sweep")]
        [SerializeField] private float sweepDuration = 2.1f;
        [SerializeField] private Color standbyLineEmission = new Color(0.02f, 0.10f, 0.16f, 1f);
        [SerializeField] private Color activeLineEmission = new Color(0.12f, 0.72f, 1.0f, 1f);
        [SerializeField] private float activeLineIntensity = 3.4f;
        [SerializeField] private Color runnerLightColor = new Color(0.15f, 0.78f, 1.0f, 1f);
        [SerializeField] private float runnerLightIntensity = 12f;
        [SerializeField] private float runnerLightRange = 8.5f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Vector3 doorClosedPos;
        private bool sequenceStarted;
        private bool doorOpened;

        private Transform runnerRoot;
        private Light runnerLight;
        private Renderer runnerMeshRenderer;
        private MaterialPropertyBlock sharedPropBlock;

        private float pathStartX = -1.3f;
        private float pathMidX = -8.0f;
        private float pathEndX = -22.5f;
        private float pathY = 0.045f;
        private float pathZ = 0.0f;

        public bool SequenceStarted => sequenceStarted;
        public bool DoorOpened => doorOpened;

        public void ConfigureReferences(Transform door, Renderer m1Line, Renderer m2EntryLine)
        {
            frontDoor = door;
            linhaM1Renderer = m1Line;
            linhaEntradaM2Renderer = m2EntryLine;
        }

        private void Awake()
        {
            sharedPropBlock = new MaterialPropertyBlock();

            if (frontDoor != null)
            {
                doorClosedPos = frontDoor.localPosition;
            }

            ComputeFloorPathBounds();
            CreateFloorLightRunner();
        }

        private void Start()
        {
            // Keep floor guide strips dim until the operator initiates the protocol
            SetRendererEmission(linhaM1Renderer, standbyLineEmission, 0.4f);
            SetRendererEmission(linhaEntradaM2Renderer, standbyLineEmission, 0.35f);
        }

        public void StartSequence()
        {
            if (sequenceStarted) return;
            sequenceStarted = true;
            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            // 1. Switch door lock indicator from Red NEXUS to bright cyan/blue
            UnlockDoorIndicatorColor();

            // 2. Launch blue light sweep along the floor + open front door in parallel
            Coroutine doorRoutine = StartCoroutine(OpenDoorRoutine());
            yield return StartCoroutine(SweepFloorLightRoutine(isInitialSweep: true));
            yield return doorRoutine;

            // 3. Keep a continuous directional blue pulse travelling toward M2 so the path stays unmistakable
            StartCoroutine(ContinuousGuidePulseRoutine());
        }

        private void UnlockDoorIndicatorColor()
        {
            if (frontDoor == null) return;
            Renderer doorRenderer = frontDoor.GetComponent<Renderer>();
            if (doorRenderer == null) return;

            Material[] mats = doorRenderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.Contains("Red_NEXUS"))
                {
                    doorRenderer.GetPropertyBlock(sharedPropBlock, i);
                    sharedPropBlock.SetColor(BaseColorId, doorUnlockedEmission);
                    sharedPropBlock.SetColor(EmissionColorId, doorUnlockedEmission * doorUnlockedEmissionIntensity);
                    doorRenderer.SetPropertyBlock(sharedPropBlock, i);
                }
            }
        }

        private IEnumerator OpenDoorRoutine()
        {
            if (frontDoor == null) yield break;

            // Slight mechanical unlock beat before sliding
            yield return new WaitForSeconds(0.25f);

            Vector3 startPos = doorClosedPos;
            Vector3 targetPos = doorClosedPos + doorOpenOffset;
            float elapsed = 0f;

            while (elapsed < doorOpenDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / doorOpenDuration);
                float eased = t * t * (3f - 2f * t);
                frontDoor.localPosition = Vector3.Lerp(startPos, targetPos, eased);
                yield return null;
            }

            frontDoor.localPosition = targetPos;
            doorOpened = true;
        }

        private IEnumerator SweepFloorLightRoutine(bool isInitialSweep)
        {
            if (runnerRoot != null) runnerRoot.gameObject.SetActive(true);

            float elapsed = 0f;
            float duration = isInitialSweep ? sweepDuration : sweepDuration * 1.15f;
            bool m1Lit = !isInitialSweep;
            bool m2Lit = !isInitialSweep;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float x = Mathf.Lerp(pathStartX, pathEndX, t);

                if (runnerRoot != null)
                {
                    runnerRoot.position = new Vector3(x, pathY, pathZ);
                }

                if (runnerLight != null)
                {
                    float edgeFade = Mathf.Sin(t * Mathf.PI);
                    runnerLight.intensity = (isInitialSweep ? runnerLightIntensity : runnerLightIntensity * 0.65f) * Mathf.Clamp01(edgeFade * 2.2f);
                }

                if (isInitialSweep)
                {
                    if (!m1Lit && x <= pathStartX - 0.5f)
                    {
                        m1Lit = true;
                        SetRendererEmission(linhaM1Renderer, activeLineEmission, activeLineIntensity);
                    }
                    if (!m2Lit && x <= pathMidX)
                    {
                        m2Lit = true;
                        SetRendererEmission(linhaEntradaM2Renderer, activeLineEmission, activeLineIntensity);
                    }
                }

                yield return null;
            }

            if (isInitialSweep)
            {
                SetRendererEmission(linhaM1Renderer, activeLineEmission, activeLineIntensity);
                SetRendererEmission(linhaEntradaM2Renderer, activeLineEmission, activeLineIntensity);
            }
        }

        private IEnumerator ContinuousGuidePulseRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.45f);
                yield return StartCoroutine(SweepFloorLightRoutine(isInitialSweep: false));
            }
        }

        private void ComputeFloorPathBounds()
        {
            if (linhaM1Renderer != null)
            {
                Bounds b1 = linhaM1Renderer.bounds;
                pathStartX = b1.max.x;
                pathMidX = b1.min.x;
                pathY = b1.max.y + 0.03f;
                pathZ = b1.center.z;
            }

            if (linhaEntradaM2Renderer != null)
            {
                Bounds b2 = linhaEntradaM2Renderer.bounds;
                pathEndX = b2.min.x;
            }
        }

        private void CreateFloorLightRunner()
        {
            GameObject go = new GameObject("M1_FloorBlueLightRunner");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(pathStartX, pathY, pathZ);
            runnerRoot = go.transform;

            // Dynamic blue point light slightly above floor so walls & doorway catch the travelling glow
            GameObject lightGo = new GameObject("RunnerPointLight");
            lightGo.transform.SetParent(runnerRoot, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            runnerLight = lightGo.AddComponent<Light>();
            runnerLight.type = LightType.Point;
            runnerLight.color = runnerLightColor;
            runnerLight.intensity = runnerLightIntensity;
            runnerLight.range = runnerLightRange;
            runnerLight.shadows = LightShadows.None;

            // Glowing energy bar sliding directly over the floor guide strip
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "RunnerGlowBar";
            bar.transform.SetParent(runnerRoot, false);
            bar.transform.localPosition = Vector3.zero;
            bar.transform.localScale = new Vector3(1.6f, 0.035f, 0.22f);

            Collider col = bar.GetComponent<Collider>();
            if (col != null) Destroy(col);

            runnerMeshRenderer = bar.GetComponent<Renderer>();
            if (runnerMeshRenderer != null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(urpLit);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", runnerLightColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", runnerLightColor);
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", runnerLightColor * 5.0f);
                runnerMeshRenderer.sharedMaterial = mat;
                runnerMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            runnerRoot.gameObject.SetActive(false);
        }

        private void SetRendererEmission(Renderer target, Color color, float intensity)
        {
            if (target == null) return;
            target.GetPropertyBlock(sharedPropBlock);
            sharedPropBlock.SetColor(BaseColorId, color);
            sharedPropBlock.SetColor(EmissionColorId, color * intensity);
            target.SetPropertyBlock(sharedPropBlock);
        }
    }
}
