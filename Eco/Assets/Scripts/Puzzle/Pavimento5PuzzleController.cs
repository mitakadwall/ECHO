using System.Collections;
using UnityEngine;

namespace Eco.Puzzle
{
    public enum PuzzleColor
    {
        Purple,
        Yellow,
        Cyan
    }

    /// <summary>
    /// Controls the Pavimento 5 3-lever color-sequence puzzle:
    /// - Monitor flashes the 3 colors (Purple, Yellow, Cyan) in the required order when interacted with (E).
    /// - Pulling the 3 levers in that exact order unlocks the blocked door, retracts the lock bar, and turns the cable green.
    /// - Pulling a wrong lever flashes the monitor red and resets all levers.
    /// </summary>
    public class Pavimento5PuzzleController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Renderer monitorRenderer;
        [SerializeField] private int monitorScreenMaterialIndex = 1;
        [SerializeField] private PuzzleLeverInteractable[] levers;
        [SerializeField] private Transform lockedDoor;
        [SerializeField] private Transform lockBar;
        [SerializeField] private Renderer energyCableRenderer;

        [Header("Sequence Settings")]
        [SerializeField] private bool randomizeOrderOnStart = true;
        [SerializeField] private PuzzleColor[] sequence = new[] { PuzzleColor.Purple, PuzzleColor.Yellow, PuzzleColor.Cyan };

        [Header("Timings & Door")]
        [SerializeField] private float colorDisplayDuration = 0.85f;
        [SerializeField] private float gapBetweenColors = 0.35f;
        [SerializeField] private Vector3 doorOpenOffset = new Vector3(0f, 3.35f, 0f);
        [SerializeField] private float doorOpenSpeed = 2.4f;

        [Header("HDR Colors")]
        [SerializeField] private Color purpleColor = new Color(0.75f, 0.22f, 1.0f);
        [SerializeField] private Color yellowColor = new Color(1.0f, 0.78f, 0.08f);
        [SerializeField] private Color cyanColor = new Color(0.08f, 0.95f, 1.0f);
        [SerializeField] private Color standbyColor = new Color(0.05f, 0.16f, 0.20f);
        [SerializeField] private Color errorColor = new Color(1.0f, 0.08f, 0.05f);
        [SerializeField] private Color solvedColor = new Color(0.10f, 1.0f, 0.42f);
        [SerializeField] private float flashEmissionIntensity = 3.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock screenBlock;
        private MaterialPropertyBlock cableBlock;
        private Coroutine monitorRoutine;
        private Vector3 doorClosedPos;
        private Vector3 doorTargetPos;
        private int currentStep;
        private bool isSolved;
        private bool isDisplayingSequence;
        private bool isResetting;

        public bool IsSolved => isSolved;
        public bool IsBusy => isDisplayingSequence || isResetting;

        private void Awake()
        {
            screenBlock = new MaterialPropertyBlock();
            cableBlock = new MaterialPropertyBlock();

            if (lockedDoor != null)
            {
                doorClosedPos = lockedDoor.localPosition;
                doorTargetPos = doorClosedPos;
            }
        }

        private void Start()
        {
            if (randomizeOrderOnStart)
            {
                ShuffleSequence();
            }

            SetMonitorScreenColor(standbyColor, 0.6f);
            Debug.Log($"[Pavimento5Puzzle] Sequência ativa: {sequence[0]} -> {sequence[1]} -> {sequence[2]}");
        }

        private void Update()
        {
            if (isSolved && lockedDoor != null)
            {
                lockedDoor.localPosition = Vector3.MoveTowards(
                    lockedDoor.localPosition,
                    doorTargetPos,
                    doorOpenSpeed * Time.deltaTime);
            }
        }

        public void PlaySequenceOnMonitor()
        {
            if (isSolved || isResetting) return;

            if (monitorRoutine != null)
            {
                StopCoroutine(monitorRoutine);
            }
            monitorRoutine = StartCoroutine(FlashSequenceRoutine());
        }

        public void RestoreSolvedState()
        {
            if (!isSolved)
            {
                SolvePuzzle();
            }

            if (lockedDoor != null)
            {
                lockedDoor.localPosition = doorClosedPos + doorOpenOffset;
            }
        }

        public void NotifyLeverPulled(PuzzleLeverInteractable lever)
        {
            if (isSolved || isResetting) return;

            if (lever.LeverColor == sequence[currentStep])
            {
                currentStep++;
                Debug.Log($"[Pavimento5Puzzle] Alavanca correta ({lever.LeverColor}) [{currentStep}/{sequence.Length}]");

                if (currentStep >= sequence.Length)
                {
                    SolvePuzzle();
                }
                else if (!isDisplayingSequence)
                {
                    if (monitorRoutine != null) StopCoroutine(monitorRoutine);
                    monitorRoutine = StartCoroutine(BriefConfirmFlash(GetColor(lever.LeverColor)));
                }
            }
            else
            {
                Debug.Log($"[Pavimento5Puzzle] Ordem incorreta! Esperado {sequence[currentStep]}, acionado {lever.LeverColor}. Resetando.");
                if (monitorRoutine != null) StopCoroutine(monitorRoutine);
                monitorRoutine = StartCoroutine(ErrorAndResetRoutine());
            }
        }

        private void SolvePuzzle()
        {
            isSolved = true;
            isDisplayingSequence = false;
            if (monitorRoutine != null) StopCoroutine(monitorRoutine);

            SetMonitorScreenColor(solvedColor, flashEmissionIntensity);
            SetRendererColor(energyCableRenderer, 0, solvedColor, 2.8f, cableBlock);

            if (lockBar != null)
            {
                lockBar.gameObject.SetActive(false);
            }

            if (lockedDoor != null)
            {
                doorTargetPos = doorClosedPos + doorOpenOffset;
            }

            Debug.Log("[Pavimento5Puzzle] Puzzle resolvido! Porta desbloqueada.");
        }

        private IEnumerator FlashSequenceRoutine()
        {
            isDisplayingSequence = true;
            SetMonitorScreenColor(Color.black, 0f);
            yield return new WaitForSeconds(0.2f);

            for (int i = 0; i < sequence.Length; i++)
            {
                SetMonitorScreenColor(GetColor(sequence[i]), flashEmissionIntensity);
                yield return new WaitForSeconds(colorDisplayDuration);

                SetMonitorScreenColor(Color.black, 0f);
                yield return new WaitForSeconds(gapBetweenColors);
            }

            SetMonitorScreenColor(standbyColor, 0.6f);
            isDisplayingSequence = false;
            monitorRoutine = null;
        }

        private IEnumerator BriefConfirmFlash(Color c)
        {
            SetMonitorScreenColor(c, flashEmissionIntensity * 0.85f);
            yield return new WaitForSeconds(0.35f);
            if (!isSolved) SetMonitorScreenColor(standbyColor, 0.6f);
            monitorRoutine = null;
        }

        private IEnumerator ErrorAndResetRoutine()
        {
            isResetting = true;
            isDisplayingSequence = false;
            currentStep = 0;

            for (int i = 0; i < 2; i++)
            {
                SetMonitorScreenColor(errorColor, flashEmissionIntensity);
                yield return new WaitForSeconds(0.22f);
                SetMonitorScreenColor(Color.black, 0f);
                yield return new WaitForSeconds(0.16f);
            }

            if (levers != null)
            {
                foreach (var lever in levers)
                {
                    if (lever != null) lever.ResetLever();
                }
            }

            SetMonitorScreenColor(standbyColor, 0.6f);
            isResetting = false;
            monitorRoutine = null;
        }

        private void ShuffleSequence()
        {
            sequence = new[] { PuzzleColor.Purple, PuzzleColor.Yellow, PuzzleColor.Cyan };
            for (int i = sequence.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (sequence[i], sequence[j]) = (sequence[j], sequence[i]);
            }
        }

        private Color GetColor(PuzzleColor pc)
        {
            return pc switch
            {
                PuzzleColor.Purple => purpleColor,
                PuzzleColor.Yellow => yellowColor,
                PuzzleColor.Cyan => cyanColor,
                _ => standbyColor
            };
        }

        private void SetMonitorScreenColor(Color color, float intensity)
        {
            SetRendererColor(monitorRenderer, monitorScreenMaterialIndex, color, intensity, screenBlock);
        }

        private static void SetRendererColor(Renderer r, int materialIndex, Color color, float intensity, MaterialPropertyBlock block)
        {
            if (r == null) return;
            r.GetPropertyBlock(block, materialIndex);
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionColorId, intensity > 0f ? color * Mathf.LinearToGammaSpace(intensity) : Color.black);
            r.SetPropertyBlock(block, materialIndex);
        }
    }
}
