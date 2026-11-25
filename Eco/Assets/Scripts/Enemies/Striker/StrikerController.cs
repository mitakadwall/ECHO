using UnityEngine;
using UnityEngine.AI;
using Eco.Combat;
using Eco.Enemies.FSM;
using Eco.Enemies.Perception;
using Eco.Enemies.Striker.States;
using Eco.Player;
using Eco.Save;

namespace Eco.Enemies.Striker
{
    /// <summary>
    /// Unit-09 — Striker heavy melee security robot controller driven by a Finite State Machine
    /// (Patrol, Charge, Heavy Horizontal Attack with visible thermal-blade windup, Dead).
    /// Shares EnemyHealth (150 HP + diegetic visor), EnemyAlertSystem, Grabhook damage, and Save persistence.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyVisionSensor))]
    public class StrikerController : MonoBehaviour, IAlertReceiver, IImpactTarget
    {
        [Header("Identity & Persistence")]
        [SerializeField] private string enemyId = "unit09_striker_01";

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Renderer[] bladeRenderers;
        [SerializeField] private Light bladeTelegraphLight;

        [Header("Patrol Settings")]
        [SerializeField] private Transform[] patrolWaypoints;
        [SerializeField] private float patrolSpeed = 2.0f;
        [SerializeField] private float waypointWaitTime = 2.2f;
        [SerializeField] private float waypointReachThreshold = 0.55f;

        [Header("Alarm Settings")]
        [SerializeField] private float alarmBroadcastRadius = 22f;

        [Header("Charge Settings")]
        [SerializeField] private float chargeSpeed = 5.4f;
        [SerializeField] private float loseSightDuration = 6.0f;

        [Header("Heavy Melee 2-Hit Pin Combo")]
        [SerializeField] private float firstHitDamage = 10f;
        [SerializeField] private float secondHitDamage = 25f;
        [SerializeField] private float attackRange = 2.65f;
        [SerializeField] private float attackHitRadius = 3.0f;
        [SerializeField] private float attackArcAngle = 125f;
        [SerializeField] private float attackDuration = 2.875f;
        [SerializeField, Range(0.1f, 0.7f)] private float firstHitNormalizedTime = 0.45f;
        [SerializeField, Range(0.6f, 0.98f)] private float secondHitNormalizedTime = 0.85f;
        [SerializeField] private float postAttackPause = 0.35f;
        [SerializeField] private float impactStaggerDuration = 0.9f;

        [Header("Visuals & Orientation")]
        [SerializeField] private float turnSpeed = 10f;
        [SerializeField] private Color patrolVisorColor = new Color(1.0f, 0.65f, 0.08f, 1f);
        [SerializeField] private Color combatVisorColor = new Color(1.0f, 0.10f, 0.06f, 1f);
        [SerializeField] private Color bladeTelegraphColor = new Color(1.0f, 0.25f, 0.12f, 1f);
        [SerializeField] private float bladeIdleEmission = 0f;
        [SerializeField] private float bladeWindupEmission = 0f;
        [SerializeField] private float bladeLightIntensity = 0.55f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private NavMeshAgent agent;
        private EnemyHealth health;
        private EnemyVisionSensor vision;
        private EnemyStateMachine stateMachine;
        private Collider[] cachedColliders;
        private MaterialPropertyBlock bladePropertyBlock;

        private StrikerPatrolState patrolState;
        private StrikerChargeState chargeState;
        private StrikerAttackState attackState;
        private StrikerDeadState deadState;

        private float staggerTimer;
        private bool hasBroadcastAlarm;
        private bool isInitialized;
        private PlayerController pinnedPlayer;

        // Public accessors
        public string EnemyId => enemyId;
        public NavMeshAgent Agent => agent;
        public EnemyHealth Health => health;
        public EnemyVisionSensor Vision => vision;
        public Animator Animator => animator;
        public EnemyStateMachine StateMachine => stateMachine;

        public Transform[] PatrolWaypoints => patrolWaypoints;
        public float PatrolSpeed => patrolSpeed;
        public float WaypointWaitTime => waypointWaitTime;
        public float WaypointReachThreshold => waypointReachThreshold;

        public float ChargeSpeed => chargeSpeed;
        public float LoseSightDuration => loseSightDuration;

        public float FirstHitDamage => firstHitDamage;
        public float SecondHitDamage => secondHitDamage;
        public float AttackRange => attackRange;
        public float AttackHitRadius => attackHitRadius;
        public float AttackArcAngle => attackArcAngle;
        public float AttackDuration => attackDuration;
        public float FirstHitNormalizedTime => firstHitNormalizedTime;
        public float SecondHitNormalizedTime => secondHitNormalizedTime;
        public float PostAttackPause => postAttackPause;
        public bool IsPlayerPinned => pinnedPlayer != null;

        public bool IsStaggered => staggerTimer > 0f;

        public StrikerPatrolState PatrolState => patrolState;
        public StrikerChargeState ChargeState => chargeState;
        public StrikerAttackState AttackState => attackState;
        public StrikerDeadState DeadState => deadState;

        // IAlertReceiver implementation
        public Vector3 Position => transform.position;
        public bool IsAlertedOrDead =>
            health == null ||
            health.IsDead ||
            stateMachine == null ||
            stateMachine.CurrentState != patrolState;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            vision = GetComponent<EnemyVisionSensor>();
            cachedColliders = GetComponentsInChildren<Collider>(true);
            bladePropertyBlock = new MaterialPropertyBlock();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (bladeTelegraphLight != null)
            {
                bladeTelegraphLight.enabled = false;
            }

            stateMachine = new EnemyStateMachine();
            patrolState = new StrikerPatrolState(this);
            chargeState = new StrikerChargeState(this);
            attackState = new StrikerAttackState(this);
            deadState = new StrikerDeadState(this);
        }

        private void OnEnable()
        {
            EnemyAlertSystem.Register(this);
            if (health != null)
            {
                health.OnDamaged += HandleDamaged;
                health.OnDied += HandleDied;
            }
            if (EcoSaveManager.Instance != null)
            {
                EcoSaveManager.Instance.OnGameLoaded += HandleGameLoaded;
            }
        }

        private void OnDisable()
        {
            ReleasePinnedPlayer();
            EnemyAlertSystem.Unregister(this);
            if (health != null)
            {
                health.OnDamaged -= HandleDamaged;
                health.OnDied -= HandleDied;
            }
            if (EcoSaveManager.Instance != null)
            {
                EcoSaveManager.Instance.OnGameLoaded -= HandleGameLoaded;
            }
        }

        private void Start()
        {
            if (EcoSaveManager.Instance != null)
            {
                EcoSaveManager.Instance.OnGameLoaded -= HandleGameLoaded;
                EcoSaveManager.Instance.OnGameLoaded += HandleGameLoaded;

                if (EcoSaveManager.Instance.IsEnemyDefeated(enemyId))
                {
                    ApplyAlreadyDefeatedState();
                    return;
                }
            }

            if (!isInitialized)
            {
                isInitialized = true;
                SetVisorPatrolMode();
                SetBladeTelegraph(0f);
                stateMachine.Initialize(patrolState);
            }
        }

        private void Update()
        {
            if (!isInitialized) return;

            if (staggerTimer > 0f)
            {
                staggerTimer = Mathf.Max(0f, staggerTimer - Time.deltaTime);
            }

            if (!health.IsDead)
            {
                vision.Sense();
            }

            stateMachine.Tick(Time.deltaTime);
            UpdateAnimatorLocomotion();
        }

        public void ConfigureSetup(
            string id,
            Animator anim,
            Renderer[] blades,
            Light telegraphLight,
            Transform[] waypoints)
        {
            enemyId = id;
            animator = anim;
            bladeRenderers = blades;
            bladeTelegraphLight = telegraphLight;
            patrolWaypoints = waypoints;
        }

        public void ChangeState(IEnemyState nextState)
        {
            stateMachine.ChangeState(nextState);
        }

        public void SetVisorPatrolMode()
        {
            hasBroadcastAlarm = false;
            health?.SetVisorColor(patrolVisorColor);
        }

        public void SetVisorCombatMode()
        {
            health?.SetVisorColor(combatVisorColor);
        }

        public void SetChargingAnimation(bool charging)
        {
            if (animator != null)
            {
                animator.SetBool(IsChargingHash, charging);
            }
        }

        public void TriggerAttackAnimation()
        {
            if (animator == null) return;
            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(AttackHash);
        }

        public void TriggerDeathAnimation()
        {
            if (animator == null) return;
            animator.SetBool(IsChargingHash, false);
            animator.SetFloat(SpeedHash, 0f);
            animator.SetTrigger(DieHash);
        }

        /// <summary>
        /// Drives the thermal glow on the Striker's blades (0 = idle, 1 = full attack windup, -1 = off/dead).
        /// </summary>
        public void SetBladeTelegraph(float normalizedCharge)
        {
            bool dead = normalizedCharge < 0f || (health != null && health.IsDead);
            float t = Mathf.Clamp01(normalizedCharge);
            float intensity = dead ? 0f : Mathf.Lerp(bladeIdleEmission, bladeWindupEmission, t);
            Color hdr = dead ? Color.black : bladeTelegraphColor * Mathf.LinearToGammaSpace(intensity);

            if (bladeRenderers != null)
            {
                if (bladePropertyBlock == null) bladePropertyBlock = new MaterialPropertyBlock();

                foreach (Renderer r in bladeRenderers)
                {
                    if (r == null) continue;
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        string n = mats[i].name.ToLowerInvariant();
                        if (n.Contains("lamina"))
                        {
                            r.GetPropertyBlock(bladePropertyBlock, i);
                            bladePropertyBlock.SetColor(EmissionColorId, hdr);
                            r.SetPropertyBlock(bladePropertyBlock, i);
                        }
                    }
                }
            }

            if (bladeTelegraphLight != null)
            {
                bladeTelegraphLight.enabled = !dead && t > 0.05f;
                bladeTelegraphLight.color = bladeTelegraphColor;
                bladeTelegraphLight.intensity = bladeLightIntensity * t;
            }
        }

        public void RotateTowards(Vector3 worldPoint, float deltaTime)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * deltaTime);
        }

        public bool IsOnNavMeshReady()
        {
            return agent != null && agent.enabled && agent.isOnNavMesh;
        }

        public void StopAgent()
        {
            if (!IsOnNavMeshReady()) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        public void DisableColliders()
        {
            if (cachedColliders == null) return;
            foreach (Collider col in cachedColliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        /// <summary>
        /// Broadcasts an alert to nearby allied enemies (Watchers and other Strikers) even without an alert animation.
        /// </summary>
        public void BroadcastAlarmToAllies()
        {
            if (hasBroadcastAlarm) return;
            hasBroadcastAlarm = true;

            Vector3 targetPos = vision.PlayerTransform != null
                ? vision.PlayerTransform.position
                : vision.LastKnownPlayerPosition;

            int alerted = EnemyAlertSystem.BroadcastAlert(transform.position, alarmBroadcastRadius, targetPos, gameObject);
            if (alerted > 0)
            {
                Debug.Log($"[StrikerController] '{name}' detectou o jogador e alertou {alerted} unidade(s) próxima(s).");
            }
        }

        /// <summary>
        /// First arm strike: deals 10 damage and pins the player in place (blocking movement
        /// until the animation finishes, the Striker dies, or the Striker is staggered by Impact).
        /// </summary>
        public bool ExecuteFirstPinHit()
        {
            if (!IsPlayerInMeleeArc()) return false;

            Health playerHealth = vision.PlayerTransform.GetComponentInParent<Health>();
            if (playerHealth == null) playerHealth = vision.PlayerTransform.GetComponent<Health>();

            float hpBefore = playerHealth != null ? playerHealth.CurrentHealth : 0f;
            IDamageable damageable = playerHealth != null
                ? (IDamageable)playerHealth
                : vision.PlayerTransform.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(firstHitDamage);
            }

            // Only pin if the hit was not blocked by the player's shield
            bool hitConnected = playerHealth == null || playerHealth.CurrentHealth < hpBefore;
            if (hitConnected)
            {
                PinPlayer();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Second arm heavy strike near the end of the animation: deals 25 damage (total 35).
        /// </summary>
        public void ExecuteSecondHeavyHit()
        {
            if (vision.PlayerTransform == null)
            {
                vision.ResolvePlayer();
                if (vision.PlayerTransform == null) return;
            }

            // Guaranteed hit if still pinned; otherwise check melee arc
            if (!IsPlayerPinned && !IsPlayerInMeleeArc()) return;

            IDamageable playerDamageable = vision.PlayerTransform.GetComponentInParent<IDamageable>();
            if (playerDamageable == null)
            {
                playerDamageable = vision.PlayerTransform.GetComponent<IDamageable>();
            }

            if (playerDamageable != null)
            {
                playerDamageable.TakeDamage(secondHitDamage);
            }
        }

        public void PinPlayer()
        {
            if (pinnedPlayer != null || vision.PlayerTransform == null) return;

            PlayerController pc = vision.PlayerTransform.GetComponentInParent<PlayerController>();
            if (pc == null) pc = vision.PlayerTransform.GetComponent<PlayerController>();
            if (pc == null) return;

            pinnedPlayer = pc;
            pinnedPlayer.BeginMovementOverride();
            pinnedPlayer.SetVerticalVelocity(0f);
            Debug.Log($"[StrikerController] '{name}' prendeu o jogador com o primeiro golpe!");
        }

        public void ReleasePinnedPlayer()
        {
            if (pinnedPlayer == null) return;

            pinnedPlayer.EndMovementOverride();
            pinnedPlayer = null;
        }

        private bool IsPlayerInMeleeArc()
        {
            if (vision.PlayerTransform == null)
            {
                vision.ResolvePlayer();
                if (vision.PlayerTransform == null) return false;
            }

            Vector3 playerPos = vision.GetPlayerAimPoint();
            Vector3 flatToPlayer = playerPos - transform.position;
            flatToPlayer.y = 0f;
            float dist = flatToPlayer.magnitude;

            if (dist > attackHitRadius) return false;

            if (dist > 0.05f)
            {
                float angle = Vector3.Angle(transform.forward, flatToPlayer / dist);
                if (angle > attackArcAngle * 0.5f) return false;
            }

            return vision.HasLineOfSightToPlayer();
        }

        /// <summary>
        /// Triggered when another allied unit (Watcher or Striker) broadcasts an alarm nearby.
        /// </summary>
        public void OnExternalAlert(Vector3 targetPosition, GameObject alertSource)
        {
            if (health == null || health.IsDead) return;
            if (stateMachine == null || stateMachine.CurrentState != patrolState) return;

            hasBroadcastAlarm = true; // Avoid cascading alarm loop
            vision.RegisterExternalSighting(targetPosition);
            SetVisorCombatMode();
            ChangeState(chargeState);
        }

        /// <summary>
        /// Player Impact strike (F) interrupts a winding-up heavy slash and briefly staggers the Striker.
        /// </summary>
        public void ReceiveImpact(float impactDamage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (health == null || health.IsDead) return;

            staggerTimer = impactStaggerDuration;
            ReleasePinnedPlayer();
            attackState?.InterruptAttack();
            SetBladeTelegraph(0f);

            if (stateMachine != null && stateMachine.CurrentState == patrolState)
            {
                vision.ResolvePlayer();
                if (vision.PlayerTransform != null)
                {
                    vision.RegisterExternalSighting(vision.PlayerTransform.position);
                }
                BroadcastAlarmToAllies();
                ChangeState(chargeState);
            }

            Debug.Log($"[StrikerController] '{name}' sofreu Impacto — ataque interrompido por {impactStaggerDuration:F2}s.");
        }

        private void HandleDamaged(float amount)
        {
            if (health.IsDead) return;

            if (stateMachine != null && stateMachine.CurrentState == patrolState)
            {
                vision.ResolvePlayer();
                if (vision.PlayerTransform != null)
                {
                    vision.RegisterExternalSighting(vision.PlayerTransform.position);
                }
                BroadcastAlarmToAllies();
                ChangeState(chargeState);
            }
        }

        private void HandleDied()
        {
            if (EcoSaveManager.Instance != null && !string.IsNullOrEmpty(enemyId))
            {
                EcoSaveManager.Instance.MarkEnemyDefeated(enemyId);
            }

            ChangeState(deadState);
        }

        private void HandleGameLoaded(EcoSaveData data)
        {
            if (EcoSaveManager.Instance != null && EcoSaveManager.Instance.IsEnemyDefeated(enemyId))
            {
                ApplyAlreadyDefeatedState();
            }
        }

        private void ApplyAlreadyDefeatedState()
        {
            health?.ForceKillSilent();
            SetBladeTelegraph(-1f);
            DisableColliders();
            if (agent != null) agent.enabled = false;
            gameObject.SetActive(false);
        }

        private void UpdateAnimatorLocomotion()
        {
            if (animator == null || health.IsDead) return;

            float normalizedSpeed = 0f;
            if (IsOnNavMeshReady() && !agent.isStopped)
            {
                float maxRef = Mathf.Max(0.1f, patrolSpeed);
                normalizedSpeed = Mathf.Clamp01(agent.velocity.magnitude / maxRef);
            }

            animator.SetFloat(SpeedHash, normalizedSpeed, 0.1f, Time.deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            // Attack reach
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, attackHitRadius);

            // Alarm broadcast radius
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, alarmBroadcastRadius);

            // Patrol waypoints
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.85f);
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    if (patrolWaypoints[i] == null) continue;
                    Gizmos.DrawSphere(patrolWaypoints[i].position, 0.22f);
                    Transform next = patrolWaypoints[(i + 1) % patrolWaypoints.Length];
                    if (next != null)
                    {
                        Gizmos.DrawLine(patrolWaypoints[i].position, next.position);
                    }
                }
            }
        }
    }
}
