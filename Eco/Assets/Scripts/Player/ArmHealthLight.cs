using UnityEngine;
using Eco.Combat;

namespace Eco.Player
{
    /// <summary>
    /// Updates the diegetic forearm light (luz) color and HDR emission based on player health:
    /// High (>60%) = Cyan, Medium (30%-60%) = Orange, Low (<30%) = Pulsing Red.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class ArmHealthLight : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Renderer[] lightRenderers;

        [Header("Health Colors")]
        [SerializeField] private Color highHealthColor = new Color(0.08f, 0.95f, 1.0f);
        [SerializeField] private Color mediumHealthColor = new Color(1.0f, 0.52f, 0.05f);
        [SerializeField] private Color lowHealthColor = new Color(1.0f, 0.06f, 0.04f);

        [Header("Thresholds & Glow")]
        [SerializeField, Range(0f, 1f)] private float highThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.3f;
        [SerializeField] private float emissionIntensity = 3.0f;
        [SerializeField] private float lowHealthPulseSpeed = 7.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Health health;
        private MaterialPropertyBlock propBlock;
        private float currentRatio = 1f;

        private void Awake()
        {
            health = GetComponent<Health>();
            propBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnHealthChanged += HandleHealthChanged;
                UpdateRatio(health.CurrentHealth, health.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void Start()
        {
            if (health != null)
            {
                UpdateRatio(health.CurrentHealth, health.MaxHealth);
            }
        }

        private void Update()
        {
            if (currentRatio < lowThreshold && lightRenderers != null && lightRenderers.Length > 0)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * lowHealthPulseSpeed);
                ApplyColor(lowHealthColor, emissionIntensity * pulse);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            UpdateRatio(current, max);
        }

        private void UpdateRatio(float current, float max)
        {
            currentRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            Color targetColor = currentRatio > highThreshold
                ? highHealthColor
                : (currentRatio >= lowThreshold ? mediumHealthColor : lowHealthColor);

            ApplyColor(targetColor, emissionIntensity);
        }

        private void ApplyColor(Color color, float intensity)
        {
            if (lightRenderers == null) return;

            Color hdrEmission = color * Mathf.LinearToGammaSpace(intensity);
            foreach (Renderer r in lightRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(propBlock);
                propBlock.SetColor(BaseColorId, color);
                propBlock.SetColor(EmissionColorId, hdrEmission);
                r.SetPropertyBlock(propBlock);
            }
        }
    }
}
