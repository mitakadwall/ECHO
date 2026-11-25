using UnityEngine;
using UnityEngine.AI;
using Eco.Combat;
using Eco.Endgame;
using Eco.Enemies.Boss.States;
using Eco.Enemies.FSM;
using Eco.Enemies.Perception;
using Eco.Save;

namespace Eco.Enemies.Boss
{
    /// <summary>
    /// Overseer "Carcass-0" boss controller in Pavimento 01 (Câmara de Prensagem).
    /// 220 HP, 40 damage attacks, Impact-cancellable ground-slam preparation,
    /// column-breaking linear charge (30 self-damage if baited into a column),
    /// Phase 2 Overheat below 50% HP (+35% backstab bonus, faster moves, wider shockwaves),
    /// and unlocks EndgameTerminalInteractable only upon death.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyVisionSensor))]
    public class BossCarcassController : MonoBehaviour, IImpactTarget
    {
        public const string DefaultBossId = "boss_carcass_0";

        [Header("Identity & Persistence")]
        [SerializeField] private string enemyId = DefaultBossId;

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private ParticleSystem[] pistonSteamParticles;
        [SerializeField] private ParticleSystem[] pistonEmberParticles;
        [SerializeField] private Light overheatBackLight;

        [Header("Arena Activation")]
        [SerializeField] private float arenaActivationDistance = 18f;

        [Header("Movement & Phases")]
        [SerializeField] private float phase1WalkSpeed = 2.6f;
        [SerializeField] private float phase2WalkSpeed = 4.0f;
        [SerializeField] private float turnSpeed = 5.5f;
        [SerializeField] private float phase2HealthThreshold = 0.5f;
        [SerializeField] private float phase2AnimSpeed = 1.25f;
        [SerializeField] private float phase2BackDamageBonus = 0.35f;
        [SerializeField] private float phaseTransitionDuration = 2.4f;

        [Header("Ground Slam Attack (40 Damage)")]
        [SerializeField] private float attackDamage = 40f;
        [SerializeField] private float slamTriggerRange = 4.2f;
        [SerializeField] private float prepareDuration = 1.35f;
        [SerializeField] private float slamDuration = 1.55f;
        [SerializeField] private float slamImpactNormalizedTime = 0.45f;
        [SerializeField] private float phase1SlamRadius = 4.8f;
        [SerializeField] private float phase2SlamRadius = 7.4f;
        [SerializeField] private float shockwaveMaxHeightDelta = 1.9f;
        [SerializeField] private float impactCancelStaggerDuration = 1.6f;

        [Header("Linear Charge Attack (40 Damage / 30 Column Crash Self-Damage)")]
        [SerializeField] private float chargeMinDistance = 6.5f;
        [SerializeField] private float chargeMaxDistance = 17f;
        [SerializeField] private float chargeCooldown = 7.5f;
        [SerializeField] private float phase1ChargeSpeed = 9.5f;
        [SerializeField] private float phase2ChargeSpeed = 12.5f;
        [SerializeField] private float maxChargeDuration = 2.1f;
        [SerializeField] private float chargeHitRadius = 1.75f;
        [SerializeField] private float columnCrashStunDuration = 1.85f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
        private static readonly int PrepareHash = Animator.StringToHash("Prepare");
        private static readonly int SlamHash = Animator.StringToHash("Slam");
        private static readonly int CancelAttackHash = Animator.StringToHash("CancelAttack");
        private static readonly int PhaseTransitionHash = Animator.StringToHash("PhaseTransition");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int AnimSpeedHash = Animator.StringToHash("AnimSpeed");

        private NavMeshAgent agent;
        private EnemyHealth health;
        private EnemyVisionSensor vision;
        private EnemyStateMachine stateMachine;
        private Collider[] cachedColliders;

        private BossIdleState idleState;
        private BossWalkState walkState;
        private BossPrepareSlamState prepareSlamState;
        private BossSlamAttackState slamAttackState;
        private BossChargeState chargeState;
        private BossPhaseTransitionState phaseTransitionState;
        private BossDeadState deadState;

        private bool isFightActive;
        private bool isPhase2;
        private bool phaseTransitionTriggered;
        private bool applyingBonusDamage;
        private float staggerTimer;
        private float nextChargeAllowedTime;
        private bool isInitialized;

        // Public accessors
        public string EnemyId => enemyId;
        public NavMeshAgent Agent => agent;
        public EnemyHealth Health => health;
        public EnemyVisionSensor Vision => vision;
        public Animator Animator => animator;
        public EnemyStateMachine StateMachine => stateMachine;

        public bool IsFightActive => isFightActive;
        public bool IsPhase2 => isPhase2;
        public bool IsStaggered => staggerTimer > 0f;

        public float ArenaActivationDistance => arenaActivationDistance;
        public float CurrentWalkSpeed => isPhase2 ? phase2WalkSpeed : phase1WalkSpeed;
        public float CurrentChargeSpeed => isPhase2 ? phase2ChargeSpeed : phase1ChargeSpeed;
        public float CurrentSlamRadius => isPhase2 ? phase2SlamRadius : phase1SlamRadius;

        public float AttackDamage => attackDamage;
        public float SlamTriggerRange => slamTriggerRange;
        public float PrepareDuration => isPhase2 ? prepareDuration * 0.85f : prepareDuration;
        public float SlamDuration => isPhase2 ? slamDuration * 0.85f : slamDuration;
        public float SlamImpactNormalizedTime => slamImpactNormalizedTime;
        public float PhaseTransitionDuration => phaseTransitionDuration;

        public float ChargeMinDistance => chargeMinDistance;
        public float ChargeMaxDistance => chargeMaxDistance;
        public float MaxChargeDuration => maxChargeDuration;
        public float ChargeHitRadius => chargeHitRadius;
        public bool CanStartCharge => Time.time >= nextChargeAllowedTime;

        public BossIdleState IdleState => idleState;
        public BossWalkState WalkState => walkState;
        public BossPrepareSlamState PrepareSlamState => prepareSlamState;
        public BossSlamAttackState SlamAttackState => slamAttackState;
        public BossChargeState ChargeState => chargeState;
        public BossPhaseTransitionState PhaseTransitionState => phaseTransitionState;
        public BossDeadState DeadState => deadState;

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

            stateMachine = new EnemyStateMachine();
            idleState = new BossIdleState(this);
            walkState = new BossWalkState(this);
            prepareSlamState = new BossPrepareSlamState(this);
            slamAttackState = new BossSlamAttackState(this);
            chargeState = new BossChargeState(this);
            phaseTransitionState = new BossPhaseTransitionState(this);
            deadState = new BossDeadState(this);
        }

        private void OnEnable()
        {
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
                SetAnimatorPhaseSpeed(1f);
                ApplyPistonEffects(phase2: false, alive: true);
                nextChargeAllowedTime = Time.time + 3.5f;
                stateMachine.Initialize(idleState);
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
            ParticleSystem[] steamParticles,
            ParticleSystem[] emberParticles,
            Light backLight)
        {
            enemyId = id;
            animator = anim;
            pistonSteamParticles = steamParticles;
            pistonEmberParticles = emberParticles;
            overheatBackLight = backLight;
        }

        public void ActivateFight()
        {
            isFightActive = true;
        }

        public void ChangeState(IEnemyState nextState)
        {
            stateMachine.ChangeState(nextState);
        }

        public void EnterPhase2Overheat()
        {
            isPhase2 = true;
            SetAnimatorPhaseSpeed(phase2AnimSpeed);
            ApplyPistonEffects(phase2: true, alive: true);
            Debug.Log("[Carcass-0] FASE 2 — SUPERAQUECIMENTO ATIVADA!");
        }

        public void MarkChargeCooldown()
        {
            nextChargeAllowedTime = Time.time + (isPhase2 ? chargeCooldown * 0.72f : chargeCooldown);
        }

        public void ApplyStagger(float duration)
        {
            staggerTimer = Mathf.Max(staggerTimer, duration);
        }

        public void SetChargingAnimation(bool charging)
        {
            if (animator != null) animator.SetBool(IsChargingHash, charging);
        }

        public void TriggerPrepareAnimation()
        {
            if (animator == null) return;
            animator.ResetTrigger(CancelAttackHash);
            animator.ResetTrigger(SlamHash);
            animator.SetTrigger(PrepareHash);
        }

        public void TriggerSlamAnimation()
        {
            if (animator == null) return;
            animator.SetTrigger(SlamHash);
        }

        public void TriggerCancelAttackAnimation()
        {
            if (animator == null) return;
            animator.ResetTrigger(PrepareHash);
            animator.ResetTrigger(SlamHash);
            animator.SetTrigger(CancelAttackHash);
        }

        public void TriggerPhaseTransitionAnimation()
        {
            if (animator == null) return;
            animator.SetBool(IsChargingHash, false);
            animator.SetTrigger(PhaseTransitionHash);
        }

        public void TriggerDeathAnimation()
        {
            if (animator == null) return;
            animator.SetBool(IsChargingHash, false);
            animator.SetFloat(SpeedHash, 0f);
            animator.SetTrigger(DieHash);
        }

        public void SetAnimatorPhaseSpeed(float speed)
        {
            if (animator != null) animator.SetFloat(AnimSpeedHash, speed);
        }

        public void RotateTowards(Vector3 worldPoint, float deltaTime, float speedMultiplier = 1f)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * speedMultiplier * deltaTime);
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

        public void TurnOffPistonEffects()
        {
            ApplyPistonEffects(phase2: false, alive: false);
        }

        /// <summary>
        /// Executes the heavy ground-slam shockwave (40 damage).
        /// Players standing on elevated platforms above shockwaveMaxHeightDelta evade the ground wave.
        /// </summary>
        public void ExecuteGroundSlamShockwave()
        {
            float radius = CurrentSlamRadius;
            Vector3 origin = transform.position + transform.forward * 1.15f;
            BossShockwaveEffect.Spawn(origin, radius, isPhase2);

            if (vision.PlayerTransform == null)
            {
                vision.ResolvePlayer();
                if (vision.PlayerTransform == null) return;
            }

            Vector3 playerPos = vision.PlayerTransform.position;
            float verticalDelta = playerPos.y - transform.position.y;
            if (verticalDelta > shockwaveMaxHeightDelta)
            {
                return; // Player is safely on an elevated catwalk/platform
            }

            Vector3 flatDelta = playerPos - origin;
            flatDelta.y = 0f;
            if (flatDelta.magnitude <= radius)
            {
                ApplyDamageToPlayer(attackDamage);
            }
        }

        public void ApplyDamageToPlayer(float damage)
        {
            if (vision.PlayerTransform == null)
            {
                vision.ResolvePlayer();
                if (vision.PlayerTransform == null) return;
            }

            IDamageable dmg = vision.PlayerTransform.GetComponentInParent<IDamageable>();
            if (dmg == null) dmg = vision.PlayerTransform.GetComponent<IDamageable>();
            dmg?.TakeDamage(damage);
        }

        /// <summary>
        /// Called when Carcass-0's linear charge crashes into a BreakableColumn.
        /// Destroys the column, deals 30 self-damage to Carcass-0, and stuns it.
        /// </summary>
        public void HandleColumnCrash(BreakableColumn column, Vector3 impactPoint, Vector3 moveDir)
        {
            if (column == null || !column.TryBreak(impactPoint, moveDir)) return;

            ApplyStagger(columnCrashStunDuration);
            float selfDmg = column.SelfDamageToBoss;
            if (selfDmg > 0f && !health.IsDead)
            {
                applyingBonusDamage = true; // Do not apply backstab bonus to self-crash damage
                health.TakeDamage(selfDmg);
                applyingBonusDamage = false;
            }
        }

        /// <summary>
        /// Player Impact strike (F): cancels Carcass-0's ground-slam if hit during 'preparacao' (PrepareSlamState).
        /// </summary>
        public void ReceiveImpact(float impactDamage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (health == null || health.IsDead) return;

            ActivateFight();

            if (stateMachine != null && stateMachine.CurrentState == prepareSlamState)
            {
                TriggerCancelAttackAnimation();
                ApplyStagger(impactCancelStaggerDuration);
                ChangeState(walkState);
                Debug.Log($"[Carcass-0] Ataque CANCELADO pelo Impacto do jogador! Stagger de {impactCancelStaggerDuration:F1}s.");
            }
        }

        private void HandleDamaged(float amount)
        {
            if (health.IsDead) return;

            ActivateFight();

            // Phase 2 back-attack bonus (+35%) when hitting the exposed overheated pistons from behind
            if (isPhase2 && !applyingBonusDamage && vision.PlayerTransform != null)
            {
                Vector3 toPlayer = vision.PlayerTransform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f && Vector3.Dot(transform.forward, toPlayer.normalized) < -0.25f)
                {
                    float bonus = amount * phase2BackDamageBonus;
                    if (bonus > 0.1f)
                    {
                        applyingBonusDamage = true;
                        health.TakeDamage(bonus);
                        applyingBonusDamage = false;
                        Debug.Log($"[Carcass-0] Dano crítico nos pistões traseiros (+{bonus:F1} bônus)!");
                    }
                }
            }

            if (health.IsDead) return;

            if (stateMachine != null && stateMachine.CurrentState == idleState)
            {
                ChangeState(walkState);
            }

            // Trigger Phase 2 transition once when dropping to or below 50% HP (110 HP)
            if (!phaseTransitionTriggered && health.HealthNormalized <= phase2HealthThreshold)
            {
                phaseTransitionTriggered = true;
                ChangeState(phaseTransitionState);
            }
        }

        private void HandleDied()
        {
            isFightActive = false;

            if (EcoSaveManager.Instance != null && !string.IsNullOrEmpty(enemyId))
            {
                EcoSaveManager.Instance.MarkEnemyDefeated(enemyId);
            }

            ChangeState(deadState);

            if (EndgameTerminalInteractable.Instance != null)
            {
                EndgameTerminalInteractable.Instance.UnlockAfterBossDefeated();
            }
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
            isFightActive = false;
            health?.ForceKillSilent();
            TurnOffPistonEffects();
            DisableColliders();
            if (agent != null) agent.enabled = false;
            gameObject.SetActive(false);

            if (EndgameTerminalInteractable.Instance != null)
            {
                EndgameTerminalInteractable.Instance.UnlockAfterBossDefeated();
            }
        }

        private void ApplyPistonEffects(bool phase2, bool alive)
        {
            if (pistonSteamParticles != null)
            {
                foreach (ParticleSystem ps in pistonSteamParticles)
                {
                    if (ps == null) continue;
                    var em = ps.emission;
                    if (!alive)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                    else
                    {
                        em.rateOverTime = phase2 ? 26f : 7f;
                        if (!ps.isPlaying) ps.Play();
                    }
                }
            }

            if (pistonEmberParticles != null)
            {
                foreach (ParticleSystem ps in pistonEmberParticles)
                {
                    if (ps == null) continue;
                    if (!alive || !phase2)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                    else
                    {
                        var em = ps.emission;
                        em.rateOverTime = 24f;
                        if (!ps.isPlaying) ps.Play();
                    }
                }
            }

            if (overheatBackLight != null)
            {
                overheatBackLight.enabled = alive && phase2;
                overheatBackLight.intensity = phase2 ? 3.5f : 0f;
            }
        }

        private void UpdateAnimatorLocomotion()
        {
            if (animator == null || health.IsDead) return;

            float normalizedSpeed = 0f;
            if (IsOnNavMeshReady() && !agent.isStopped)
            {
                float maxRef = Mathf.Max(0.1f, CurrentWalkSpeed);
                normalizedSpeed = Mathf.Clamp01(agent.velocity.magnitude / maxRef);
            }

            animator.SetFloat(SpeedHash, normalizedSpeed, 0.12f, Time.deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, phase1SlamRadius);

            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, phase2SlamRadius);
        }
    }
}
