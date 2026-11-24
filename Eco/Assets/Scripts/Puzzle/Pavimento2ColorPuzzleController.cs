using System.Collections;
using UnityEngine;

namespace Eco.Puzzle
{
    public enum ColorCylinderType
    {
        Cyan,
        Yellow,
        Green
    }

    /// <summary>
    /// Controls the Pavimento 2 Genius-style color sequence puzzle on 'puzzle cores':
    /// - First interaction with 'puzzle cores' generates a random 5-color sequence (Cyan, Yellow, Green)
    ///   and flashes the corresponding cylinder submeshes in order.
    /// - Afterwards, only the 3 cylinders accept interaction (E).
    /// - Wrong cylinder flashes all cylinders red, resets progress, generates a new sequence and replays it.
    /// - Completing the sequence lights all cylinders green and destroys the 'bloqueado' barrier.
    /// </summary>
    public class Pavimento2ColorPuzzleController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Renderer puzzleRenderer;
        [SerializeField] private int cyanMaterialIndex = 1;
        [SerializeField] private int yellowMaterialIndex = 2;
        [SerializeField] private int greenMaterialIndex = 3;
        [SerializeField] private GameObject blockedObject;

        [Header("Sequence Settings")]
        [SerializeField] private int sequenceLength = 5;

        [Header("Timings")]
        [SerializeField] private float startDelay = 0.35f;
        [SerializeField] private float flashDuration = 0.65f;
        [SerializeField] private float gapDuration = 0.30f;
        [SerializeField] private float clickFeedbackDuration = 0.25f;

        [Header("HDR Colors")]
        [SerializeField] private Color cyanColor = new Color(0.08f, 0.95f, 1.0f);
        [SerializeField] private Color yellowColor = new Color(1.0f, 0.72f, 0.06f);
        [SerializeField] private Color greenColor = new Color(0.10f, 1.0f, 0.45f);
        [SerializeField] private Color errorColor = new Color(1.0f, 0.08f, 0.05f);
        [SerializeField] private Color solvedColor = new Color(0.10f, 1.0f, 0.45f);

        [Header("Intensities")]
        [SerializeField] private float idleBaseMultiplier = 0.45f;
        [SerializeField] private float idleEmissionIntensity = 0.35f;
        [SerializeField] private float offBaseMultiplier = 0.18f;
        [SerializeField] private float flashEmissionIntensity = 4.2f;
        [SerializeField] private float solvedEmissionIntensity = 3.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock cyanBlock;
        private MaterialPropertyBlock yellowBlock;
        private MaterialPropertyBlock greenBlock;

        private ColorCylinderType[] sequence;
        private Coroutine activeRoutine;
        private Coroutine clickFeedbackRoutine;

        private int currentStep;
        private bool hasStarted;
        private bool isDisplayingSequence;
        private bool isResetting;
        private bool isSolved;

        public bool HasStarted => hasStarted;
        public bool IsSolved => isSolved;
        public bool IsBusy => isDisplayingSequence || isResetting;
        public bool IsAcceptingInput => hasStarted && !isSolved && !isDisplayingSequence && !isResetting;
        public int CurrentStep => currentStep;
        public ColorCylinderType[] CurrentSequence => sequence;

        private void Awake()
        {
            cyanBlock = new MaterialPropertyBlock();
            yellowBlock = new MaterialPropertyBlock();
            greenBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            SetAllCylindersIdle();
        }

        public void StartPuzzle()
        {
            if (hasStarted || isSolved) return;

            hasStarted = true;
            currentStep = 0;
            GenerateRandomSequence();
            PlayCurrentSequence();
        }

        public void RestoreSolvedState()
        {
            if (isSolved) return;
            hasStarted = true;
            SolvePuzzle();
        }

        public void SubmitCylinder(ColorCylinderType chosenColor)
        {
            if (!IsAcceptingInput || sequence == null || sequence.Length == 0) return;

            if (chosenColor == sequence[currentStep])
            {
                currentStep++;
                Debug.Log($"[Pavimento2Puzzle] Cilindro correto ({chosenColor}) [{currentStep}/{sequence.Length}]");

                if (currentStep >= sequence.Length)
                {
                    SolvePuzzle();
                }
                else
                {
                    TriggerClickFeedback(chosenColor);
                }
            }
            else
            {
                Debug.Log($"[Pavimento2Puzzle] Ordem incorreta! Esperado {sequence[currentStep]}, acionado {chosenColor}. Gerando nova sequência.");
                if (clickFeedbackRoutine != null)
                {
                    StopCoroutine(clickFeedbackRoutine);
                    clickFeedbackRoutine = null;
                }
                if (activeRoutine != null)
                {
                    StopCoroutine(activeRoutine);
                }
                activeRoutine = StartCoroutine(ErrorAndReplayRoutine());
            }
        }

        private void GenerateRandomSequence()
        {
            int len = Mathf.Max(1, sequenceLength);
            sequence = new ColorCylinderType[len];
            for (int i = 0; i < len; i++)
            {
                sequence[i] = (ColorCylinderType)Random.Range(0, 3);
            }

            Debug.Log($"[Pavimento2Puzzle] Nova sequência gerada: {string.Join(" -> ", sequence)}");
        }

        private void PlayCurrentSequence()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }
            activeRoutine = StartCoroutine(FlashSequenceRoutine());
        }

        private void TriggerClickFeedback(ColorCylinderType color)
        {
            if (clickFeedbackRoutine != null)
            {
                StopCoroutine(clickFeedbackRoutine);
                SetAllCylindersIdle();
            }
            clickFeedbackRoutine = StartCoroutine(BriefClickFlashRoutine(color));
        }

        private void SolvePuzzle()
        {
            isSolved = true;
            isDisplayingSequence = false;
            isResetting = false;

            if (clickFeedbackRoutine != null)
            {
                StopCoroutine(clickFeedbackRoutine);
                clickFeedbackRoutine = null;
            }
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            SetCylinderVisual(ColorCylinderType.Cyan, solvedColor, solvedEmissionIntensity);
            SetCylinderVisual(ColorCylinderType.Yellow, solvedColor, solvedEmissionIntensity);
            SetCylinderVisual(ColorCylinderType.Green, solvedColor, solvedEmissionIntensity);

            if (blockedObject != null)
            {
                Destroy(blockedObject);
                blockedObject = null;
            }

            Debug.Log("[Pavimento2Puzzle] Puzzle resolvido! Objeto 'bloqueado' destruído. Acesso ao Pavimento 1 liberado.");
        }

        private IEnumerator FlashSequenceRoutine()
        {
            isDisplayingSequence = true;
            SetAllCylindersDimOff();
            yield return new WaitForSeconds(startDelay);

            for (int i = 0; i < sequence.Length; i++)
            {
                ColorCylinderType stepColor = sequence[i];
                SetCylinderVisual(stepColor, GetBaseColor(stepColor), flashEmissionIntensity);
                yield return new WaitForSeconds(flashDuration);

                SetCylinderOff(stepColor);
                yield return new WaitForSeconds(gapDuration);
            }

            SetAllCylindersIdle();
            isDisplayingSequence = false;
            activeRoutine = null;
        }

        private IEnumerator BriefClickFlashRoutine(ColorCylinderType color)
        {
            SetCylinderVisual(color, GetBaseColor(color), flashEmissionIntensity * 0.9f);
            yield return new WaitForSeconds(clickFeedbackDuration);
            if (!isSolved && !isDisplayingSequence && !isResetting)
            {
                SetCylinderIdle(color);
            }
            clickFeedbackRoutine = null;
        }

        private IEnumerator ErrorAndReplayRoutine()
        {
            isResetting = true;
            isDisplayingSequence = false;
            currentStep = 0;

            for (int i = 0; i < 2; i++)
            {
                SetAllCylindersColor(errorColor, flashEmissionIntensity);
                yield return new WaitForSeconds(0.22f);
                SetAllCylindersDimOff();
                yield return new WaitForSeconds(0.18f);
            }

            yield return new WaitForSeconds(0.25f);

            GenerateRandomSequence();
            isResetting = false;
            activeRoutine = StartCoroutine(FlashSequenceRoutine());
        }

        private void SetAllCylindersIdle()
        {
            SetCylinderIdle(ColorCylinderType.Cyan);
            SetCylinderIdle(ColorCylinderType.Yellow);
            SetCylinderIdle(ColorCylinderType.Green);
        }

        private void SetAllCylindersDimOff()
        {
            SetCylinderOff(ColorCylinderType.Cyan);
            SetCylinderOff(ColorCylinderType.Yellow);
            SetCylinderOff(ColorCylinderType.Green);
        }

        private void SetAllCylindersColor(Color color, float intensity)
        {
            SetCylinderVisual(ColorCylinderType.Cyan, color, intensity);
            SetCylinderVisual(ColorCylinderType.Yellow, color, intensity);
            SetCylinderVisual(ColorCylinderType.Green, color, intensity);
        }

        private void SetCylinderIdle(ColorCylinderType type)
        {
            Color baseCol = GetBaseColor(type);
            SetCylinderVisual(type, baseCol * idleBaseMultiplier, idleEmissionIntensity);
        }

        private void SetCylinderOff(ColorCylinderType type)
        {
            Color baseCol = GetBaseColor(type);
            SetCylinderVisual(type, baseCol * offBaseMultiplier, 0f);
        }

        private void SetCylinderVisual(ColorCylinderType type, Color color, float intensity)
        {
            if (puzzleRenderer == null) return;

            int matIndex = GetMaterialIndex(type);
            MaterialPropertyBlock block = GetBlock(type);
            if (matIndex < 0 || matIndex >= puzzleRenderer.sharedMaterials.Length) return;

            puzzleRenderer.GetPropertyBlock(block, matIndex);
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionColorId, intensity > 0f ? color * Mathf.LinearToGammaSpace(intensity) : Color.black);
            puzzleRenderer.SetPropertyBlock(block, matIndex);
        }

        private Color GetBaseColor(ColorCylinderType type)
        {
            return type switch
            {
                ColorCylinderType.Cyan => cyanColor,
                ColorCylinderType.Yellow => yellowColor,
                ColorCylinderType.Green => greenColor,
                _ => Color.white
            };
        }

        private int GetMaterialIndex(ColorCylinderType type)
        {
            return type switch
            {
                ColorCylinderType.Cyan => cyanMaterialIndex,
                ColorCylinderType.Yellow => yellowMaterialIndex,
                ColorCylinderType.Green => greenMaterialIndex,
                _ => -1
            };
        }

        private MaterialPropertyBlock GetBlock(ColorCylinderType type)
        {
            return type switch
            {
                ColorCylinderType.Cyan => cyanBlock,
                ColorCylinderType.Yellow => yellowBlock,
                ColorCylinderType.Green => greenBlock,
                _ => cyanBlock
            };
        }
    }
}
