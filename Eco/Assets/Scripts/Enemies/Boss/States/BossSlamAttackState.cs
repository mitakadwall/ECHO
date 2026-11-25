using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Boss.States
{
    /// <summary>
    /// Plays 'Rig_Personagem|golpe' and unleashes the 40-damage ground shockwave.
    /// </summary>
    public class BossSlamAttackState : IEnemyState
    {
        private readonly BossCarcassController boss;
        private float elapsed;
        private bool shockwaveTriggered;

        public BossSlamAttackState(BossCarcassController boss)
        {
            this.boss = boss;
        }

        public void Enter()
        {
            elapsed = 0f;
            shockwaveTriggered = false;
            boss.StopAgent();
            boss.TriggerSlamAnimation();
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            float duration = Mathf.Max(0.4f, boss.SlamDuration);
            float impactTime = duration * boss.SlamImpactNormalizedTime;

            if (!shockwaveTriggered && elapsed >= impactTime)
            {
                shockwaveTriggered = true;
                boss.ExecuteGroundSlamShockwave();
            }

            if (elapsed >= duration)
            {
                boss.ChangeState(boss.WalkState);
            }
        }

        public void Exit()
        {
        }
    }
}
