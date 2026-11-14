using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Eco.Combat;

namespace Eco.Player
{
    /// <summary>
    /// Handles first-person melee attacks (Left Mouse), shield blocking (Q key),
    /// and light Cinemachine camera shake feedback when attacked.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private Transform weaponVisual;
        [SerializeField] private Transform shieldVisual;

        [Header("Melee Settings")]
        [SerializeField] private float meleeDamage = 20f;
        [SerializeField] private float meleeRange = 2.5f;
        [SerializeField] private float meleeRadius = 0.4f;
        [SerializeField] private float attackCooldown = 0.45f;

        [Header("Camera Disorientation Shake")]
        [SerializeField] private Vector3 blockedShakeVelocity = new Vector3(0.08f, 0.06f, 0.04f);
        [SerializeField] private Vector3 damagedShakeVelocity = new Vector3(0.14f, 0.11f, 0.06f);

        private Health playerHealth;
        private CinemachineImpulseSource impulseSource;
        private float lastAttackTime = -999f;
        private float swingTimer;

        private Vector3 weaponRestLocalPos;
        private Quaternion weaponRestLocalRot;
        private Vector3 shieldRestLocalPos;

        private void Awake()
        {
            playerHealth = GetComponent<Health>();
            impulseSource = GetComponent<CinemachineImpulseSource>();

            if (weaponVisual != null)
            {
                weaponRestLocalPos = weaponVisual.localPosition;
                weaponRestLocalRot = weaponVisual.localRotation;
            }

            if (shieldVisual != null)
            {
                shieldRestLocalPos = shieldVisual.localPosition;
            }
        }

        private void OnEnable()
        {
            playerHealth.OnBlockedHit += HandleBlockedHit;
            playerHealth.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            playerHealth.OnBlockedHit -= HandleBlockedHit;
            playerHealth.OnDamaged -= HandleDamaged;
        }

        private void Update()
        {
            HandleShieldInput();
            HandleMeleeInput();
            UpdateVisualFeedback();
        }

        private void HandleShieldInput()
        {
            if (Keyboard.current == null) return;

            bool isBlocking = Cursor.lockState == CursorLockMode.Locked && Keyboard.current.qKey.isPressed;
            playerHealth.IsBlocking = isBlocking;
        }

        private void HandleMeleeInput()
        {
            if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;
            if (playerHealth.IsBlocking) return;

            if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= lastAttackTime + attackCooldown)
            {
                PerformMeleeAttack();
            }
        }

        private void PerformMeleeAttack()
        {
            lastAttackTime = Time.time;
            swingTimer = 0.2f;

            if (attackOrigin == null) return;

            RaycastHit[] hits = Physics.SphereCastAll(
                attackOrigin.position,
                meleeRadius,
                attackOrigin.forward,
                meleeRange,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            IDamageable closestTarget = null;
            float closestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform)
                {
                    continue;
                }

                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestTarget = damageable;
                }
            }

            if (closestTarget != null)
            {
                closestTarget.TakeDamage(meleeDamage);
                Debug.Log($"[PlayerCombat] Melee hit dealt {meleeDamage} damage.");
            }
        }

        private void HandleBlockedHit()
        {
            TriggerDisorientationShake(blockedShakeVelocity);
            Debug.Log("[PlayerCombat] Attack blocked by shield (0 damage taken).");
        }

        private void HandleDamaged(float amount)
        {
            TriggerDisorientationShake(damagedShakeVelocity);
            Debug.Log($"[PlayerCombat] Player took {amount} damage. HP: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}");
        }

        private void TriggerDisorientationShake(Vector3 baseVelocity)
        {
            if (impulseSource == null) return;

            // Slight random sign variation so consecutive hits feel organic
            Vector3 randomized = new Vector3(
                baseVelocity.x * (Random.value > 0.5f ? 1f : -1f),
                baseVelocity.y * (Random.value > 0.5f ? 1f : -1f),
                baseVelocity.z);

            impulseSource.GenerateImpulseWithVelocity(randomized);
        }

        private void UpdateVisualFeedback()
        {
            if (weaponVisual != null)
            {
                if (swingTimer > 0f)
                {
                    swingTimer = Mathf.Max(0f, swingTimer - Time.deltaTime);
                    float t = Mathf.Sin((swingTimer / 0.2f) * Mathf.PI);
                    weaponVisual.localPosition = weaponRestLocalPos + new Vector3(-0.15f * t, -0.05f * t, 0.25f * t);
                    weaponVisual.localRotation = weaponRestLocalRot * Quaternion.Euler(45f * t, -35f * t, 0f);
                }
                else
                {
                    weaponVisual.localPosition = Vector3.Lerp(weaponVisual.localPosition, weaponRestLocalPos, Time.deltaTime * 16f);
                    weaponVisual.localRotation = Quaternion.Slerp(weaponVisual.localRotation, weaponRestLocalRot, Time.deltaTime * 16f);
                }
            }

            if (shieldVisual != null)
            {
                Vector3 targetPos = playerHealth.IsBlocking
                    ? new Vector3(-0.12f, -0.14f, 0.52f)
                    : shieldRestLocalPos;
                shieldVisual.localPosition = Vector3.Lerp(shieldVisual.localPosition, targetPos, Time.deltaTime * 14f);
            }
        }
    }
}
