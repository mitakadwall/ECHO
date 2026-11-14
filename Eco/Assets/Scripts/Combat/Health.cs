using System;
using UnityEngine;

namespace Eco.Combat
{
    /// <summary>
    /// Reusable health component with blocking support and decoupled combat events.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;

        private float currentHealth;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => currentHealth <= 0f;

        /// <summary>
        /// When true, incoming damage is completely negated and triggers OnBlockedHit instead.
        /// </summary>
        public bool IsBlocking { get; set; }

        public event Action<float, float> OnHealthChanged;
        public event Action<float> OnDamaged;
        public event Action OnBlockedHit;
        public event Action OnDied;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead) return;

            if (IsBlocking)
            {
                OnBlockedHit?.Invoke();
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnDamaged?.Invoke(amount);

            if (currentHealth <= 0f)
            {
                OnDied?.Invoke();
            }
        }
    }
}
