using UnityEngine;

namespace Eco.Combat
{
    /// <summary>
    /// Simple training dummy that receives player melee damage and periodically attacks
    /// nearby players so shield blocking and camera disorientation can be tested immediately.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TestEnemyAttacker : MonoBehaviour
    {
        [Header("Attack Settings")]
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float attackRange = 3.2f;
        [SerializeField] private float attackInterval = 2.5f;

        private Health selfHealth;
        private Renderer cachedRenderer;
        private Color baseColor = Color.white;
        private float flashTimer;
        private float nextAttackTime;
        private Transform playerTransform;
        private IDamageable playerDamageable;

        private void Awake()
        {
            selfHealth = GetComponent<Health>();
            cachedRenderer = GetComponentInChildren<Renderer>();
            if (cachedRenderer != null)
            {
                baseColor = cachedRenderer.material.color;
            }
        }

        private void OnEnable()
        {
            selfHealth.OnDamaged += HandleDamaged;
            selfHealth.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            selfHealth.OnDamaged -= HandleDamaged;
            selfHealth.OnDied -= HandleDied;
        }

        private void Start()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerDamageable = playerObj.GetComponent<IDamageable>();
            }
            nextAttackTime = Time.time + attackInterval;
        }

        private void Update()
        {
            UpdateHitFlash();
            TryAttackPlayer();
        }

        private void TryAttackPlayer()
        {
            if (playerTransform == null || playerDamageable == null) return;
            if (Time.time < nextAttackTime) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= attackRange)
            {
                nextAttackTime = Time.time + attackInterval;
                playerDamageable.TakeDamage(attackDamage);
            }
        }

        private void HandleDamaged(float amount)
        {
            flashTimer = 0.15f;
            if (cachedRenderer != null)
            {
                cachedRenderer.material.color = Color.red;
            }
            Debug.Log($"[TestEnemyAttacker] '{name}' took {amount} damage. Remaining HP: {selfHealth.CurrentHealth}/{selfHealth.MaxHealth}");
        }

        private void HandleDied()
        {
            Debug.Log($"[TestEnemyAttacker] '{name}' defeated! Resetting HP for continuous testing.");
            selfHealth.ResetHealth();
        }

        private void UpdateHitFlash()
        {
            if (flashTimer <= 0f || cachedRenderer == null) return;

            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                cachedRenderer.material.color = baseColor;
            }
        }
    }
}
