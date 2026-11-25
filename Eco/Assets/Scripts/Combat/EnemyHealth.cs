using System;
using UnityEngine;

namespace Eco.Combat
{
    /// <summary>
    /// Dedicated health component for enemies (default 100 HP) with diegetic visor
    /// illumination that dims proportionally as HP drops and turns off completely on death.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Visor Diegetic Illumination")]
        [SerializeField] private Renderer visorRenderer;
        [SerializeField] private int visorMaterialIndex = 0;
        [SerializeField] private Light visorLight;
        [SerializeField] private Color visorColor = new Color(1.0f, 0.65f, 0.08f, 1f);
        [SerializeField] private float fullHealthEmissionIntensity = 4.5f;
        [SerializeField] private float minAliveEmissionIntensity = 0.35f;
        [SerializeField] private float fullHealthLightIntensity = 2.2f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private float currentHealth;
        private MaterialPropertyBlock propertyBlock;
        private Color activeVisorColor;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        public bool IsDead => currentHealth <= 0f;

        public event Action<float, float> OnHealthChanged;
        public event Action<float> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            currentHealth = maxHealth;
            activeVisorColor = visorColor;
            propertyBlock = new MaterialPropertyBlock();
            AutoFindVisorReferences();
            UpdateVisorIllumination();
        }

        public void ConfigureVisor(Renderer targetRenderer, int materialIndex, Light targetLight = null)
        {
            visorRenderer = targetRenderer;
            visorMaterialIndex = materialIndex;
            if (targetLight != null)
            {
                visorLight = targetLight;
            }
            UpdateVisorIllumination();
        }

        /// <summary>
        /// Changes the visor hue (e.g. amber on patrol, red on alert/combat) while
        /// preserving brightness scaling based on remaining health.
        /// </summary>
        public void SetVisorColor(Color newColor)
        {
            activeVisorColor = newColor;
            UpdateVisorIllumination();
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            UpdateVisorIllumination();
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            UpdateVisorIllumination();

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnDamaged?.Invoke(amount);

            if (currentHealth <= 0f)
            {
                OnDied?.Invoke();
            }
        }

        public void ForceKillSilent()
        {
            currentHealth = 0f;
            UpdateVisorIllumination();
        }

        private void AutoFindVisorReferences()
        {
            if (visorRenderer == null)
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    if (r == null) continue;
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] != null && mats[i].name.ToLowerInvariant().Contains("visor"))
                        {
                            visorRenderer = r;
                            visorMaterialIndex = i;
                            break;
                        }
                    }
                    if (visorRenderer != null) break;
                }
            }

            if (visorLight == null)
            {
                Transform lightTr = transform.Find("VisorLight");
                if (lightTr != null)
                {
                    visorLight = lightTr.GetComponent<Light>();
                }
            }
        }

        private void UpdateVisorIllumination()
        {
            float ratio = HealthNormalized;
            bool alive = !IsDead && ratio > 0f;

            // Smooth curve: strong glow at full HP, dims steadily as HP drops, completely off at 0 HP.
            float emissionIntensity = alive
                ? Mathf.Lerp(minAliveEmissionIntensity, fullHealthEmissionIntensity, ratio)
                : 0f;

            Color emissionColor = alive
                ? activeVisorColor * Mathf.LinearToGammaSpace(emissionIntensity)
                : Color.black;

            Color baseTint = alive
                ? Color.Lerp(activeVisorColor * 0.25f, activeVisorColor, ratio)
                : new Color(0.03f, 0.03f, 0.04f, 1f);

            if (visorRenderer != null)
            {
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

                Material[] mats = visorRenderer.sharedMaterials;
                int safeIndex = (mats != null && mats.Length > 0)
                    ? Mathf.Clamp(visorMaterialIndex, 0, mats.Length - 1)
                    : 0;

                visorRenderer.GetPropertyBlock(propertyBlock, safeIndex);
                propertyBlock.SetColor(EmissionColorId, emissionColor);
                propertyBlock.SetColor(BaseColorId, baseTint);
                propertyBlock.SetColor(ColorId, baseTint);
                visorRenderer.SetPropertyBlock(propertyBlock, safeIndex);
            }

            if (visorLight != null)
            {
                visorLight.enabled = alive;
                visorLight.color = activeVisorColor;
                visorLight.intensity = alive ? fullHealthLightIntensity * ratio : 0f;
            }
        }
    }
}
