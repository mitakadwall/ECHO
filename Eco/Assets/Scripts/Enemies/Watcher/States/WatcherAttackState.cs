using UnityEngine;
using UnityEngine.AI;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Watcher.States
{
    /// <summary>
    /// Ranged combat state: fires 3-round ballistic bursts from the integrated rifle and
    /// repositions to nearby cover between bursts (or when damaged).
    /// </summary>
    public class WatcherAttackState : IEnemyState
    {
        private enum SubPhase
        {
            AimingAndFiring,
            SeekingCover,
            AdvancingToSight
        }

        private readonly WatcherController watcher;
        private SubPhase phase;
        private int shotsFiredInBurst;
        private float nextShotTime;
        private float nextBurstAllowedTime;
        private float coverSearchCooldown;
        private bool wantsCoverImmediately;

        public WatcherAttackState(WatcherController watcher)
        {
            this.watcher = watcher;
        }

        public void Enter()
        {
            watcher.SetVisorCombatMode();

            phase = SubPhase.AimingAndFiring;
            shotsFiredInBurst = 0;
            nextShotTime = Time.time + 0.2f;
            nextBurstAllowedTime = Time.time;
            wantsCoverImmediately = false;

            if (watcher.IsOnNavMeshReady())
            {
                watcher.Agent.updateRotation = true;
                watcher.Agent.speed = watcher.CombatMoveSpeed;
                watcher.Agent.stoppingDistance = 0.35f;
                watcher.Agent.isStopped = true;
            }
        }

        public void Tick(float deltaTime)
        {
            if (watcher.Vision.PlayerTransform == null)
            {
                watcher.Vision.ResolvePlayer();
                if (watcher.Vision.PlayerTransform == null)
                {
                    watcher.ChangeState(watcher.PatrolState);
                    return;
                }
            }

            // Staggered by player's Impact (F): pause firing and movement until recovery
            if (watcher.IsStaggered)
            {
                watcher.StopAgent();
                return;
            }

            float distanceToPlayer = watcher.Vision.GetDistanceToPlayer();
            bool hasLOS = watcher.Vision.HasLineOfSightToPlayer();

            // Lost sight for too long -> return to patrol
            if (!hasLOS && watcher.Vision.TimeSinceLastSeen > watcher.LoseSightDuration)
            {
                watcher.ChangeState(watcher.PatrolState);
                return;
            }

            // Rotate toward player while firing or waiting in cover
            if (phase == SubPhase.AimingAndFiring)
            {
                watcher.RotateTowards(watcher.Vision.PlayerTransform.position, deltaTime);
            }

            if (wantsCoverImmediately && shotsFiredInBurst == 0)
            {
                wantsCoverImmediately = false;
                if (TryMoveToCover())
                {
                    phase = SubPhase.SeekingCover;
                    nextBurstAllowedTime = Mathf.Max(nextBurstAllowedTime, Time.time + watcher.BurstCooldown * 0.7f);
                    return;
                }
            }

            switch (phase)
            {
                case SubPhase.AimingAndFiring:
                    TickFiringPhase(distanceToPlayer, hasLOS);
                    break;

                case SubPhase.SeekingCover:
                    TickCoverPhase(deltaTime);
                    break;

                case SubPhase.AdvancingToSight:
                    TickAdvancePhase(distanceToPlayer, hasLOS);
                    break;
            }
        }

        public void Exit()
        {
            shotsFiredInBurst = 0;
            wantsCoverImmediately = false;
        }

        public void InterruptBurst()
        {
            shotsFiredInBurst = 0;
            nextBurstAllowedTime = Time.time + watcher.BurstCooldown * 0.75f;
            wantsCoverImmediately = true;
        }

        public void NotifyDamaged()
        {
            wantsCoverImmediately = true;
        }

        private void TickFiringPhase(float distanceToPlayer, bool hasLOS)
        {
            if (!hasLOS || distanceToPlayer > watcher.AttackRange)
            {
                // Reposition toward last known player position to regain line of sight
                if (watcher.IsOnNavMeshReady())
                {
                    watcher.Agent.isStopped = false;
                    watcher.Agent.SetDestination(watcher.Vision.LastKnownPlayerPosition);
                    phase = SubPhase.AdvancingToSight;
                }
                return;
            }

            watcher.StopAgent();

            if (Time.time < nextBurstAllowedTime) return;
            if (Time.time < nextShotTime) return;

            watcher.FireSingleShot(watcher.Vision.GetPlayerAimPoint());
            shotsFiredInBurst++;
            nextShotTime = Time.time + watcher.TimeBetweenShots;

            if (shotsFiredInBurst >= watcher.BurstCount)
            {
                shotsFiredInBurst = 0;
                nextBurstAllowedTime = Time.time + watcher.BurstCooldown;

                if (TryMoveToCover())
                {
                    phase = SubPhase.SeekingCover;
                }
            }
        }

        private void TickCoverPhase(float deltaTime)
        {
            if (watcher.IsOnNavMeshReady() && !watcher.Agent.pathPending && watcher.Agent.remainingDistance <= 0.5f)
            {
                watcher.Agent.isStopped = true;
                watcher.RotateTowards(watcher.Vision.PlayerTransform.position, deltaTime);
            }

            // Emerge from cover when ready for the next burst
            if (Time.time >= nextBurstAllowedTime)
            {
                phase = SubPhase.AimingAndFiring;
            }
        }

        private void TickAdvancePhase(float distanceToPlayer, bool hasLOS)
        {
            if (hasLOS && distanceToPlayer <= watcher.AttackRange * 0.9f)
            {
                watcher.StopAgent();
                phase = SubPhase.AimingAndFiring;
                return;
            }

            if (watcher.IsOnNavMeshReady() && !watcher.Agent.pathPending && watcher.Agent.remainingDistance <= 0.8f)
            {
                watcher.Agent.SetDestination(watcher.Vision.LastKnownPlayerPosition);
            }
        }

        /// <summary>
        /// Samples NavMesh points around the Watcher to find a cover spot whose line of sight
        /// to the player is blocked by level geometry (pillars, crates, walls).
        /// </summary>
        private bool TryMoveToCover()
        {
            if (!watcher.IsOnNavMeshReady() || Time.time < coverSearchCooldown) return false;
            coverSearchCooldown = Time.time + 0.8f;

            Vector3 playerAim = watcher.Vision.GetPlayerAimPoint();
            Vector3 origin = watcher.transform.position;
            float radius = watcher.CoverSearchRadius;

            Vector3 bestCover = Vector3.zero;
            float bestScore = float.MinValue;
            bool foundCover = false;

            const int sampleAngles = 10;
            for (int i = 0; i < sampleAngles; i++)
            {
                float angle = (360f / sampleAngles) * i;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 candidate = origin + dir * Random.Range(radius * 0.45f, radius);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
                {
                    continue;
                }

                float distToPlayer = Vector3.Distance(navHit.position, watcher.Vision.PlayerTransform.position);
                if (distToPlayer < 3.5f)
                {
                    continue;
                }

                Vector3 coverEyePos = navHit.position + Vector3.up * 1.45f;
                bool blockedFromPlayer = !watcher.Vision.HasLineOfSight(coverEyePos, playerAim);
                if (!blockedFromPlayer) continue;

                float distFromSelf = Vector3.Distance(origin, navHit.position);
                float score = distToPlayer - distFromSelf * 0.6f;

                if (score > bestScore)
                {
                    NavMeshPath path = new NavMeshPath();
                    if (watcher.Agent.CalculatePath(navHit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                    {
                        bestScore = score;
                        bestCover = navHit.position;
                        foundCover = true;
                    }
                }
            }

            if (foundCover)
            {
                watcher.Agent.isStopped = false;
                watcher.Agent.SetDestination(bestCover);
                return true;
            }

            return false;
        }
    }
}
