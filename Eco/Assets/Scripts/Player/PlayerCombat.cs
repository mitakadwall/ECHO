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
        [SerializeField] private Transform rightArmPivot;
        [SerializeField] private Transform weaponVisual;
        [SerializeField] private Transform shieldVisual;

        [Header("Equipment State")]
        [SerializeField] private bool startWithWeapon = false;
        [SerializeField] private bool startWithShield = false;

        public bool HasWeapon { get; private set; }
        public bool HasShield { get; private set; }

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
        private float swingElapsed = -1f;
        private float swingDuration = 0.36f;
        private bool isImpactSwing;

        private Vector3 armRestLocalPos;
        private Quaternion armRestLocalRot;
        private Vector3 shieldRestLocalPos;

        private void Awake()
        {
            playerHealth = GetComponent<Health>();
            impulseSource = GetComponent<CinemachineImpulseSource>();

            Transform activePivot = rightArmPivot != null ? rightArmPivot : weaponVisual;
            if (activePivot != null)
            {
                armRestLocalPos = activePivot.localPosition;
                armRestLocalRot = activePivot.localRotation;
            }

            if (shieldVisual != null)
            {
                shieldRestLocalPos = shieldVisual.localPosition;
            }

            HasWeapon = startWithWeapon;
            HasShield = startWithShield;
            SyncEquipmentVisuals();
        }

        public void EquipWeapon()
        {
            HasWeapon = true;
            SyncEquipmentVisuals();
        }

        public void EquipShield()
        {
            HasShield = true;
            SyncEquipmentVisuals();
        }

        private void SyncEquipmentVisuals()
        {
            if (weaponVisual != null) weaponVisual.gameObject.SetActive(HasWeapon);
            if (shieldVisual != null) shieldVisual.gameObject.SetActive(HasShield);
            if (!HasShield && playerHealth != null) playerHealth.IsBlocking = false;
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
            if (!HasShield)
            {
                playerHealth.IsBlocking = false;
                return;
            }
            if (Keyboard.current == null) return;

            bool isBlocking = Cursor.lockState == CursorLockMode.Locked && Keyboard.current.qKey.isPressed;
            playerHealth.IsBlocking = isBlocking;
        }

        private void HandleMeleeInput()
        {
            if (!HasWeapon) return;
            if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;
            if (playerHealth.IsBlocking) return;

            if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= lastAttackTime + attackCooldown)
            {
                PerformMeleeAttack();
            }
        }

        private void HandleImpactInput()
        {
            if (!HasWeapon) return;
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
            swingElapsed = 0f;
            swingDuration = 0.36f;
            isImpactSwing = false;

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
            swingElapsed = 0f;
            swingDuration = 0.44f;
            isImpactSwing = true;

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
            Transform activePivot = rightArmPivot != null ? rightArmPivot : weaponVisual;
            if (activePivot != null)
            {
                if (swingElapsed >= 0f)
                {
                    swingElapsed += Time.deltaTime;
                    float u = Mathf.Clamp01(swingElapsed / swingDuration);
                    Vector3 posOffset;
                    Vector3 rotOffset;

                    if (u < 0.22f)
                    {
                        // Phase 1: Wind-up back and up
                        float p = Mathf.SmoothStep(0f, 1f, u / 0.22f);
                        posOffset = new Vector3(0.10f, 0.08f, -0.07f) * p;
                        rotOffset = new Vector3(-28f, 22f, -15f) * p;
                    }
                    else if (u < 0.56f)
                    {
                        // Phase 2: Fast diagonal slash across view
                        float p = Mathf.SmoothStep(0f, 1f, (u - 0.22f) / 0.34f);
                        float power = isImpactSwing ? 1.25f : 1.0f;
                        posOffset = Vector3.Lerp(
                            new Vector3(0.10f, 0.08f, -0.07f),
                            new Vector3(-0.34f * power, -0.10f, 0.24f * power),
                            p);
                        rotOffset = Vector3.Lerp(
                            new Vector3(-28f, 22f, -15f),
                            new Vector3(58f * power, -52f, 28f),
                            p);
                    }
                    else
                    {
                        // Phase 3: Smooth follow-through back to rest
                        float p = Mathf.SmoothStep(0f, 1f, (u - 0.56f) / 0.44f);
                        float power = isImpactSwing ? 1.25f : 1.0f;
                        posOffset = Vector3.Lerp(new Vector3(-0.34f * power, -0.10f, 0.24f * power), Vector3.zero, p);
                        rotOffset = Vector3.Lerp(new Vector3(58f * power, -52f, 28f), Vector3.zero, p);
                    }

                    activePivot.localPosition = armRestLocalPos + posOffset;
                    activePivot.localRotation = armRestLocalRot * Quaternion.Euler(rotOffset);

                    if (u >= 1f)
                    {
                        swingElapsed = -1f;
                    }
                }
                else
                {
                    // Subtle breathing sway when idle
                    float swayY = Mathf.Sin(Time.time * 2.2f) * 0.005f;
                    Vector3 targetPos = armRestLocalPos + new Vector3(0f, swayY, 0f);
                    activePivot.localPosition = Vector3.Lerp(activePivot.localPosition, targetPos, Time.deltaTime * 12f);
                    activePivot.localRotation = Quaternion.Slerp(activePivot.localRotation, armRestLocalRot, Time.deltaTime * 12f);
                }
            }

            if (shieldVisual != null)
            {
                Vector3 targetPos = playerHealth.IsBlocking
                    ? new Vector3(-0.10f, -0.04f, 0.50f)
                    : shieldRestLocalPos;
                shieldVisual.localPosition = Vector3.Lerp(shieldVisual.localPosition, targetPos, Time.deltaTime * 14f);
            }
        }
    }
}
