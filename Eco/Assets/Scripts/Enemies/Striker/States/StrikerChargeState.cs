using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Striker.States
{
    /// <summary>
    /// High-speed pursuit state: sprints toward the player using the accelerated 'investida' animation.
    /// </summary>
    public class StrikerChargeState : IEnemyState
    {
        private readonly StrikerController striker;
        private float nextRepathTime;

        public StrikerChargeState(StrikerController striker)
        {
            this.striker = striker;
        }

        public void Enter()
        {
            nextRepathTime = 0f;
            striker.SetVisorCombatMode();
            striker.SetChargingAnimation(true);
            striker.SetBladeTelegraph(0.15f);

            if (striker.IsOnNavMeshReady())
            {
                striker.Agent.updateRotation = true;
                striker.Agent.speed = striker.ChargeSpeed;
                striker.Agent.stoppingDistance = Mathf.Max(0.4f, striker.AttackRange * 0.72f);
                striker.Agent.isStopped = false;
            }
        }

        public void Tick(float deltaTime)
        {
            if (striker.Vision.PlayerTransform == null)
            {
                striker.Vision.ResolvePlayer();
                if (striker.Vision.PlayerTransform == null)
                {
                    striker.ChangeState(striker.PatrolState);
                    return;
                }
            }

            if (striker.IsStaggered)
            {
                striker.StopAgent();
                return;
            }

            float distToPlayer = striker.Vision.GetDistanceToPlayer();
            bool hasLOS = striker.Vision.HasLineOfSightToPlayer();

            if (distToPlayer <= striker.AttackRange && hasLOS)
            {
                striker.ChangeState(striker.AttackState);
                return;
            }

            if (!hasLOS && striker.Vision.TimeSinceLastSeen > striker.LoseSightDuration)
            {
                striker.ChangeState(striker.PatrolState);
                return;
            }

            if (striker.IsOnNavMeshReady() && Time.time >= nextRepathTime)
            {
                nextRepathTime = Time.time + 0.15f;
                Vector3 dest = hasLOS
                    ? striker.Vision.PlayerTransform.position
                    : striker.Vision.LastKnownPlayerPosition;
                striker.Agent.isStopped = false;
                striker.Agent.SetDestination(dest);
            }
        }

        public void Exit()
        {
            striker.SetChargingAnimation(false);
        }
    }
}
