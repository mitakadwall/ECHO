using Eco.Enemies.FSM;

namespace Eco.Enemies.Boss.States
{
    public class BossDeadState : IEnemyState
    {
        private readonly BossCarcassController boss;

        public BossDeadState(BossCarcassController boss)
        {
            this.boss = boss;
        }

        public void Enter()
        {
            boss.StopAgent();
            if (boss.Agent != null) boss.Agent.enabled = false;
            boss.DisableColliders();
            boss.TurnOffPistonEffects();
            boss.TriggerDeathAnimation();
        }

        public void Tick(float deltaTime)
        {
        }

        public void Exit()
        {
        }
    }
}
