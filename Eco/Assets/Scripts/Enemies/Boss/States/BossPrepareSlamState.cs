using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Boss.States
{
    /// <summary>
    /// Plays 'Rig_Personagem|preparacao' (deals ZERO damage).
    /// If the player lands an Impact strike (F) during this window, BossCarcassController
    /// cancels the attack and staggers the boss before BossSlamAttackState ever begins.
    /// </summary>
    public class BossPrepareSlamState : IEnemyState
    {
        private readonly BossCarcassController boss;
        private float elapsed;

        public BossPrepareSlamState(BossCarcassController boss)
        {
            this.boss = boss;
        }

        public void Enter()
        {
            elapsed = 0f;
            boss.StopAgent();
            boss.TriggerPrepareAnimation();
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            if (boss.Vision.PlayerTransform != null && elapsed < boss.PrepareDuration * 0.75f)
            {
                boss.RotateTowards(boss.Vision.PlayerTransform.position, deltaTime, 0.9f);
            }

            if (elapsed >= boss.PrepareDuration)
            {
                boss.ChangeState(boss.SlamAttackState);
            }
        }

        public void Exit()
        {
        }
    }
}
