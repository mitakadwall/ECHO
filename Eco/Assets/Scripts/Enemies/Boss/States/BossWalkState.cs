using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Boss.States
{
    public class BossWalkState : IEnemyState
    {
        private readonly BossCarcassController boss;
        private float nextRepathTime;

        public BossWalkState(BossCarcassController boss)
        {
            this.boss = boss;
        }

        public void Enter()
        {
            nextRepathTime = 0f;
            boss.SetChargingAnimation(false);

            if (boss.IsOnNavMeshReady())
            {
                boss.Agent.updateRotation = true;
                boss.Agent.speed = boss.CurrentWalkSpeed;
                boss.Agent.stoppingDistance = Mathf.Max(1.8f, boss.SlamTriggerRange * 0.72f);
                boss.Agent.isStopped = false;
            }
        }

        public void Tick(float deltaTime)
        {
            if (boss.Vision.PlayerTransform == null)
            {
                boss.Vision.ResolvePlayer();
                if (boss.Vision.PlayerTransform == null) return;
            }

            if (boss.IsStaggered)
            {
                boss.StopAgent();
                return;
            }

            float dist = boss.Vision.GetDistanceToPlayer();

            // Close enough for heavy ground slam (begins with cancellable 'preparacao')
            if (dist <= boss.SlamTriggerRange)
            {
                boss.ChangeState(boss.PrepareSlamState);
                return;
            }

            // Medium range with cooldown ready -> linear charge aimed at player
            if (boss.CanStartCharge && dist >= boss.ChargeMinDistance && dist <= boss.ChargeMaxDistance && boss.Vision.HasLineOfSightToPlayer())
            {
                boss.ChangeState(boss.ChargeState);
                return;
            }

            if (boss.IsOnNavMeshReady() && Time.time >= nextRepathTime)
            {
                nextRepathTime = Time.time + 0.2f;
                boss.Agent.speed = boss.CurrentWalkSpeed;
                boss.Agent.isStopped = false;
                boss.Agent.SetDestination(boss.Vision.PlayerTransform.position);
            }
        }

        public void Exit()
        {
        }
    }
}
