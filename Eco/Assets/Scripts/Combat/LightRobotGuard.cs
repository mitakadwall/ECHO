using UnityEngine;

namespace Eco.Combat
{
    /// <summary>
    /// Guard behavior for light robots. Keeps Health.IsBlocking active until broken by
    /// the player's Impact strike, leaving the robot vulnerable for a duration.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class LightRobotGuard : MonoBehaviour, IImpactTarget
    {
        [SerializeField] private float guardBreakDuration = 3.5f;
        [SerializeField] private Color guardingColor = new Color(0.25f, 0.65f, 1f, 1f);
        [SerializeField] private Color brokenColor = new Color(1f, 0.6f, 0.15f, 1f);

        private Health health;
        private Renderer cachedRenderer;
        private float guardBrokenTimer;

        public bool IsGuardBroken => guardBrokenTimer > 0f;

        private void Awake()
        {
            health = GetComponent<Health>();
            cachedRenderer = GetComponentInChildren<Renderer>();
            SetGuardState(true);
        }

        private void OnEnable()
        {
            health.OnBlockedHit += HandleBlockedHit;
        }

        private void OnDisable()
        {
            health.OnBlockedHit -= HandleBlockedHit;
        }

        private void Update()
        {
            if (guardBrokenTimer <= 0f) return;

            guardBrokenTimer -= Time.deltaTime;
            if (guardBrokenTimer <= 0f)
            {
                SetGuardState(true);
                Debug.Log($"[LightRobotGuard] '{name}' restored its defense.");
            }
        }

        public void ReceiveImpact(float impactDamage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (!IsGuardBroken)
            {
                guardBrokenTimer = guardBreakDuration;
                SetGuardState(false);
                Debug.Log($"[LightRobotGuard] '{name}' defense broken by Impact for {guardBreakDuration:F1}s!");
            }
            else
            {
                guardBrokenTimer = guardBreakDuration;
            }
        }

        private void HandleBlockedHit()
        {
            Debug.Log($"[LightRobotGuard] '{name}' blocked regular attack. Use Impact (F) to break guard!");
        }

        private void SetGuardState(bool guarding)
        {
            health.IsBlocking = guarding;
            if (cachedRenderer != null)
            {
                cachedRenderer.material.color = guarding ? guardingColor : brokenColor;
            }
        }
    }
}
