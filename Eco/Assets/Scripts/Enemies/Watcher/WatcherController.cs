using UnityEngine;
using UnityEngine.AI;
using Eco.Combat;
using Eco.Enemies.Combat;
using Eco.Enemies.FSM;
using Eco.Enemies.Perception;
using Eco.Enemies.Watcher.States;
using Eco.Save;

namespace Eco.Enemies.Watcher
{
    /// <summary>
    /// Unit-04 — Watcher ranged security robot controller driven by a Finite State Machine
    /// (Patrol, Alert, Attack with 3-round bursts & cover seeking, Retreat, Dead).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyVisionSensor))]
    public class WatcherController : MonoBehaviour, IAlertReceiver, IImpactTarget
    {
        [Header("Identity & Persistence")]
        [SerializeField] private string enemyId = "unit04_watcher_01";

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Transform weaponBone;
        [SerializeField] private Light muzzleFlashLight;
        [SerializeField] private PulseProjectile projectilePrefab;

        [Header("Patrol Settings")]
        [SerializeField] private Transform[] patrolWaypoints;
        [SerializeField] private float patrolSpeed = 2.1f;
        [SerializeField] private float waypointWaitTime = 2.0f;
        [SerializeField] private float waypointReachThreshold = 0.55f;

        [Header("Alert Settings")]
        [SerializeField] private float alertDuration = 1.15f;
        [SerializeField] private float alarmBroadcastRadius = 22f;

        [Header("Attack & Burst Settings")]
        [SerializeField] private int burstCount = 3;
        [SerializeField] private float timeBetweenShots = 0.16f;
        [SerializeField] private float burstCooldown = 1.85f;
        [SerializeField] private float projectileDamage = 8f;
        [SerializeField] private float projectileSpeed = 28f;
        [SerializeField] private float attackRange = 18f;
        [SerializeField] private float loseSightDuration = 5.5f;
        [SerializeField] private float combatMoveSpeed = 3.2f;
        [SerializeField] private float coverSearchRadius = 8f;
        [SerializeField] private float impactStaggerDuration = 0.85f;

        [Header("Visuals & Orientation")]
        [SerializeField] private float turnSpeed = 9f;
        [SerializeField] private Color patrolVisorColor = new Color(1.0f, 0.65f, 0.08f, 1f);
        [SerializeField] private Color alertVisorColor = new Color(1.0f, 0.10f, 0.06f, 1f);
        [SerializeField] private float weaponRecoilAngle = 10f;
        [SerializeField] private float muzzleFlashDuration = 0.06f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AlertHash = Animator.StringToHash("Alert");
        private static readonly int DieHash = Animator.StringToHash("Die");

        private NavMeshAgent agent;
        private EnemyHealth health;
        private EnemyVisionSensor vision;
        private EnemyStateMachine stateMachine;
        private Collider[] cachedColliders;

        private WatcherPatrolState patrolState;
        private WatcherAlertState alertState;
        private WatcherAttackState attackState;
        private WatcherDeadState deadState;

        private Quaternion weaponRestLocalRot;
        private bool hasWeaponRestRot;
        private float currentRecoil;
        private float muzzleFlashTimer;
        private float staggerTimer;
        private bool isInitialized;

        // Public accessors for states and editor/debug inspection
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

        public float AlertDuration => alertDuration;
        public float AlarmBroadcastRadius => alarmBroadcastRadius;

        public int BurstCount => burstCount;
        public float TimeBetweenShots => timeBetweenShots;
        public float BurstCooldown => burstCooldown;
        public float AttackRange => attackRange;
        public float LoseSightDuration => loseSightDuration;
        public float CombatMoveSpeed => combatMoveSpeed;
        public float CoverSearchRadius => coverSearchRadius;

        public bool IsStaggered => staggerTimer > 0f;

        public WatcherPatrolState PatrolState => patrolState;
        public WatcherAlertState AlertState => alertState;
        public WatcherAttackState AttackState => attackState;
        public WatcherDeadState DeadState => deadState;

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

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (weaponBone != null)
            {
                weaponRestLocalRot = weaponBone.localRotation;
                hasWeaponRestRot = true;
            }

            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = false;
            }

            stateMachine = new EnemyStateMachine();
            patrolState = new WatcherPatrolState(this);
            alertState = new WatcherAlertState(this);
            attackState = new WatcherAttackState(this);
            deadState = new WatcherDeadState(this);
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

            UpdateWeaponAndMuzzleVisuals(Time.deltaTime);

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
            Transform muzzle,
            Transform weapon,
            Light flashLight,
            PulseProjectile projectile,
            Transform[] waypoints)
        {
            enemyId = id;
            animator = anim;
            muzzlePoint = muzzle;
            weaponBone = weapon;
            muzzleFlashLight = flashLight;
            projectilePrefab = projectile;
            patrolWaypoints = waypoints;
        }

        public void SetPatrolWaypoints(Transform[] waypoints)
        {
            patrolWaypoints = waypoints;
        }

        public void ChangeState(IEnemyState nextState)
        {
            stateMachine.ChangeState(nextState);
        }

        public void SetVisorPatrolMode()
        {
            health?.SetVisorColor(patrolVisorColor);
        }

        public void SetVisorCombatMode()
        {
            health?.SetVisorColor(alertVisorColor);
        }

        public void TriggerAlertAnimation()
        {
            if (animator == null) return;
            animator.ResetTrigger(AlertHash);
            animator.SetTrigger(AlertHash);
        }

        public void TriggerDeathAnimation()
        {
            if (animator == null) return;
            animator.SetFloat(SpeedHash, 0f);
            animator.SetTrigger(DieHash);
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
        /// Fires a single pulse projectile from the tip of the integrated rifle (muzzlePoint).
        /// </summary>
        public void FireSingleShot(Vector3 targetWorldPoint)
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning($"[WatcherController] '{name}' has no PulseProjectile prefab assigned.");
                return;
            }

            Vector3 spawnPos = muzzlePoint != null
                ? muzzlePoint.position
                : (transform.position + Vector3.up * 1.35f + transform.forward * 0.7f);

            Vector3 aimDir = (targetWorldPoint - spawnPos);
            if (aimDir.sqrMagnitude < 0.001f)
            {
                aimDir = transform.forward;
            }

            PulseProjectile proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(aimDir.normalized));
            proj.Launch(aimDir.normalized, transform, projectileDamage, projectileSpeed);

            // Visual kickback on the integrated rifle bone + muzzle flash
            currentRecoil = weaponRecoilAngle;
            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = true;
                muzzleFlashTimer = muzzleFlashDuration;
            }
        }

        public void BroadcastAlarmToAllies()
        {
            Vector3 targetPos = vision.PlayerTransform != null
                ? vision.PlayerTransform.position
                : vision.LastKnownPlayerPosition;

            int alerted = EnemyAlertSystem.BroadcastAlert(transform.position, alarmBroadcastRadius, targetPos, gameObject);
            if (alerted > 0)
            {
                Debug.Log($"[WatcherController] '{name}' acionou alarme e alertou {alerted} unidade(s) próxima(s).");
            }
        }

        /// <summary>
        /// Called when another allied enemy broadcasts an alarm within range.
        /// Transitions directly to combat without re-broadcasting an infinite alarm loop.
        /// </summary>
        public void OnExternalAlert(Vector3 targetPosition, GameObject alertSource)
        {
            if (health == null || health.IsDead) return;
            if (stateMachine == null || stateMachine.CurrentState != patrolState) return;

            vision.RegisterExternalSighting(targetPosition);
            SetVisorCombatMode();
            ChangeState(attackState);
        }

        /// <summary>
        /// Player Impact strike (F) interrupts the Watcher's current burst and briefly staggers it.
        /// </summary>
        public void ReceiveImpact(float impactDamage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (health == null || health.IsDead) return;

            staggerTimer = impactStaggerDuration;
            attackState?.InterruptBurst();

            if (stateMachine != null && stateMachine.CurrentState == patrolState)
            {
                vision.ResolvePlayer();
                if (vision.PlayerTransform != null)
                {
                    vision.RegisterExternalSighting(vision.PlayerTransform.position);
                }
                ChangeState(alertState);
            }

            Debug.Log($"[WatcherController] '{name}' sofreu Impacto — rajada interrompida por {impactStaggerDuration:F2}s.");
        }

        private void HandleDamaged(float amount)
        {
            if (health.IsDead) return;

            // Taking damage while patrolling immediately alerts the Watcher (e.g., Grabhook or melee).
            if (stateMachine != null && stateMachine.CurrentState == patrolState)
            {
                vision.ResolvePlayer();
                if (vision.PlayerTransform != null)
                {
                    vision.RegisterExternalSighting(vision.PlayerTransform.position);
                }
                ChangeState(alertState);
            }
            else if (stateMachine != null && stateMachine.CurrentState == attackState)
            {
                attackState.NotifyDamaged();
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
                float maxRef = Mathf.Max(0.1f, combatMoveSpeed);
                normalizedSpeed = Mathf.Clamp01(agent.velocity.magnitude / maxRef);
            }

            animator.SetFloat(SpeedHash, normalizedSpeed, 0.1f, Time.deltaTime);
        }

        private void UpdateWeaponAndMuzzleVisuals(float deltaTime)
        {
            if (muzzleFlashTimer > 0f)
            {
                muzzleFlashTimer -= deltaTime;
                if (muzzleFlashTimer <= 0f && muzzleFlashLight != null)
                {
                    muzzleFlashLight.enabled = false;
                }
            }

            if (hasWeaponRestRot && weaponBone != null)
            {
                if (currentRecoil > 0.01f)
                {
                    currentRecoil = Mathf.MoveTowards(currentRecoil, 0f, deltaTime * 55f);
                    weaponBone.localRotation = weaponRestLocalRot * Quaternion.Euler(-currentRecoil, 0f, 0f);
                }
                else if (weaponBone.localRotation != weaponRestLocalRot)
                {
                    weaponBone.localRotation = weaponRestLocalRot;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Alarm broadcast radius
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, alarmBroadcastRadius);

            // Patrol route
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.85f);
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    if (patrolWaypoints[i] == null) continue;
                    Gizmos.DrawSphere(patrolWaypoints[i].position, 0.25f);
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
