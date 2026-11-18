using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Eco.Combat;

namespace Eco.Player
{
    /// <summary>
    /// Handles first-person melee attacks (Left Mouse), Impact strike (F key) that breaks
    /// light robot defenses and fragile grates, shield blocking (Q key), and Cinemachine camera shake.
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

        [Header("Impact Settings")]
        [SerializeField] private Key impactKey = Key.F;
        [SerializeField] private float impactDamage = 30f;
        [SerializeField] private float impactRange = 2.8f;
        [SerializeField] private float impactRadius = 0.55f;
        [SerializeField] private float impactCooldown = 1.1f;

        [Header("Camera Disorientation Shake")]
        [SerializeField] private Vector3 blockedShakeVelocity = new Vector3(0.08f, 0.06f, 0.04f);
        [SerializeField] private Vector3 damagedShakeVelocity = new Vector3(0.14f, 0.11f, 0.06f);
        [SerializeField] private Vector3 impactShakeVelocity = new Vector3(0.05f, 0.04f, 0.08f);

        private Health playerHealth;
        private CinemachineImpulseSource impulseSource;
        private float lastAttackTime = -999f;
        private float lastImpactTime = -999f;
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
            HandleImpactInput();
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

        private void HandleImpactInput()
        {
            if (Keyboard.current == null || Cursor.lockState != CursorLockMode.Locked) return;
            if (playerHealth.IsBlocking) return;

            if (Keyboard.current[impactKey].wasPressedThisFrame && Time.time >= lastImpactTime + impactCooldown)
            {
                PerformImpactStrike();
            }
        }

        private void PerformMeleeAttack()
        {
            lastAttackTime = Time.time;
            swingTimer = 0.2f;

            if (TryFindClosestHit(meleeRadius, meleeRange, out RaycastHit bestHit))
            {
                IDamageable damageable = bestHit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(meleeDamage);
                    Debug.Log($"[PlayerCombat] Melee hit dealt {meleeDamage} damage.");
                }
            }
        }

        private void PerformImpactStrike()
        {
            lastImpactTime = Time.time;
            swingTimer = 0.2f;

            if (!TryFindClosestHit(impactRadius, impactRange, out RaycastHit bestHit)) return;

            IImpactTarget impactTarget = bestHit.collider.GetComponentInParent<IImpactTarget>();
            if (impactTarget != null)
            {
                impactTarget.ReceiveImpact(impactDamage, bestHit.point, attackOrigin.forward);
            }

            IDamageable damageable = bestHit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(impactDamage);
            }

            TriggerDisorientationShake(impactShakeVelocity);
            Debug.Log($"[PlayerCombat] Impact strike hit '{bestHit.collider.name}'.");
        }

        private bool TryFindClosestHit(float radius, float range, out RaycastHit closestHit)
        {
            closestHit = default;
            if (attackOrigin == null) return false;

            RaycastHit[] hits = Physics.SphereCastAll(
                attackOrigin.position,
                radius,
                attackOrigin.forward,
                range,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.MaxValue;
            bool found = false;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform)
                {
                    continue;
                }

                if (hit.collider.GetComponentInParent<IDamageable>() == null
                    && hit.collider.GetComponentInParent<IImpactTarget>() == null)
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                    found = true;
                }
            }

            return found;
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
