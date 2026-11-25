using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Boss.States
{
    public class BossIdleState : IEnemyState
    {
        private readonly BossCarcassController boss;

        public BossIdleState(BossCarcassController boss)
        {
            this.boss = boss;
        }

        public void Enter()
        {
            boss.SetChargingAnimation(false);
            boss.StopAgent();
        }

        public void Tick(float deltaTime)
        {
            if (boss.Vision.PlayerTransform == null)
            {
                boss.Vision.ResolvePlayer();
                if (boss.Vision.PlayerTransform == null) return;
            }

            float dist = boss.Vision.GetDistanceToPlayer();
            if (boss.Vision.CanSeePlayer || dist <= boss.ArenaActivationDistance)
            {
                boss.ActivateFight();
                boss.ChangeState(boss.WalkState);
            }
        }

        public void Exit()
        {
        }
    }
}
