using UnityEngine;
using Eco.Combat;
using Eco.Save;

namespace Eco.Interaction
{
    /// <summary>
    /// Interactive Energy Cell ('celula de energia') that restores +25% of player MaxHealth
    /// when pressing E, automatically updating ECO's ArmHealthLight (Red -> Yellow/Orange -> Cyan).
    /// Consumed after a single use and persisted in EcoSaveManager.
    /// </summary>
    public class EnergyCellInteractable : MonoBehaviour, IInteractable
    {
        [Header("Energy Cell Settings")]
        [SerializeField] private string cellId;
        [SerializeField, Range(0.05f, 1f)] private float healFraction = 0.25f;
        [SerializeField] private Color chargedGlowColor = new Color(0.10f, 0.95f, 0.72f);
        [SerializeField] private float chargedEmissionIntensity = 2.4f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Renderer meshRenderer;
        private MaterialPropertyBlock propBlock;
        private int glowMaterialIndex = -1;
        private bool isConsumed;

        public string CellId => cellId;
        public bool IsConsumed => isConsumed;

        public string PromptText => "Absorver Célula de Energia (+25% Vida)";

        public void Configure(string id)
        {
            cellId = id;
        }

        private void Awake()
        {
            meshRenderer = GetComponent<Renderer>();
            propBlock = new MaterialPropertyBlock();

            if (string.IsNullOrEmpty(cellId))
            {
                cellId = gameObject.name.Trim().ToLowerInvariant().Replace(" ", "_");
            }

            DetectGlowSlot();
        }

        private void Start()
        {
            if (EcoSaveManager.Instance != null && EcoSaveManager.Instance.IsEnergyCellCollected(cellId))
            {
                isConsumed = true;
                ApplyGlow(Color.black, 0f);
            }
        }

        private void Update()
        {
            if (isConsumed || glowMaterialIndex < 0) return;

            float pulse = chargedEmissionIntensity * (0.78f + 0.22f * Mathf.Sin(Time.time * 3.0f));
            ApplyGlow(chargedGlowColor, pulse);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (isConsumed || interactor == null) return false;
            Health health = interactor.GetComponent<Health>();
            if (health == null || health.IsDead) return false;
            return health.CurrentHealth < health.MaxHealth;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            Health health = interactor.GetComponent<Health>();
            if (health == null) return;

            float healAmount = health.MaxHealth * healFraction;
            float actualHealed = health.Heal(healAmount);

            isConsumed = true;
            ApplyGlow(Color.black, 0f);

            if (EcoSaveManager.Instance != null)
            {
                EcoSaveManager.Instance.MarkEnergyCellCollected(cellId);
            }

            Debug.Log($"[EnergyCell] '{gameObject.name}' absorvida (+{actualHealed:F0} HP -> {health.CurrentHealth:F0}/{health.MaxHealth:F0}).");
        }

        private void DetectGlowSlot()
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
    }
}
