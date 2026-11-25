using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Boss.States
{
    /// <summary>
    /// Triggered once at <= 50% HP: plays 'Rig_Personagem|transicao' and activates Phase 2 Overheat.
    /// </summary>
    public class BossPhaseTransitionState : IEnemyState
    {
        private readonly BossCarcassController boss;
        private float elapsed;

        public BossPhaseTransitionState(BossCarcassController boss)
        {
            this.boss = boss;
        }

        public void Enter()
        {
            elapsed = 0f;
            boss.StopAgent();
            boss.SetChargingAnimation(false);
            boss.TriggerPhaseTransitionAnimation();
            boss.EnterPhase2Overheat();
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            if (elapsed >= boss.PhaseTransitionDuration)
            {
                boss.ChangeState(boss.WalkState);
            }
        }

        public void Exit()
        {
        }
    }
}
