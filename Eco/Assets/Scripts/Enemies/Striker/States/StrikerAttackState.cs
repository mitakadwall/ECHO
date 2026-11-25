using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Striker.States
{
    /// <summary>
    /// 2-hit heavy combo synced to the full attack animation (~2.875s):
    /// 1) First arm strikes mid-animation for 10 damage and pins the player in place.
    /// 2) Second arm delivers the heavy 25-damage blow near the end of the animation,
    ///    releasing the player when the animation finishes (or immediately on Striker death/Impact).
    /// </summary>
    public class StrikerAttackState : IEnemyState
    {
        private readonly StrikerController striker;
        private float elapsed;
        private bool firstHitDone;
        private bool secondHitDone;
        private bool interrupted;

        public StrikerAttackState(StrikerController striker)
        {
            this.striker = striker;
        }

        public void Enter()
        {
            elapsed = 0f;
            firstHitDone = false;
            secondHitDone = false;
            interrupted = false;

            striker.SetVisorCombatMode();
            striker.SetChargingAnimation(false);
            striker.StopAgent();
            striker.TriggerAttackAnimation();
        }

        public void Tick(float deltaTime)
        {
            if (striker.IsStaggered || interrupted)
            {
                striker.ReleasePinnedPlayer();
                striker.SetBladeTelegraph(0f);
                if (!striker.IsStaggered)
                {
                    striker.ChangeState(striker.ChargeState);
                }
                return;
            }

            elapsed += deltaTime;
            float duration = Mathf.Max(0.5f, striker.AttackDuration);
            float firstHitTime = duration * striker.FirstHitNormalizedTime;
            float secondHitTime = duration * striker.SecondHitNormalizedTime;

            // Rotate smoothly toward player before the first arm lands
            if (!firstHitDone)
            {
                if (striker.Vision.PlayerTransform != null)
                {
                    striker.RotateTowards(striker.Vision.PlayerTransform.position, deltaTime);
                }

                float windupT = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, firstHitTime));
                striker.SetBladeTelegraph(windupT * 0.6f);

                if (elapsed >= firstHitTime)
                {
                    firstHitDone = true;
                    striker.ExecuteFirstPinHit();
                }
                return;
            }

            // Second arm heavy blow near the end of the animation
            if (!secondHitDone)
            {
                if (striker.Vision.PlayerTransform != null)
                {
                    striker.RotateTowards(striker.Vision.PlayerTransform.position, deltaTime);
                }

                float secondT = Mathf.Clamp01((elapsed - firstHitTime) / Mathf.Max(0.05f, secondHitTime - firstHitTime));
                striker.SetBladeTelegraph(Mathf.Lerp(0.6f, 1f, secondT));

                if (elapsed >= secondHitTime)
                {
                    secondHitDone = true;
                    striker.ExecuteSecondHeavyHit();
                    striker.SetBladeTelegraph(0f);
                }
                return;
            }

            // Release player as soon as the attack animation finishes
            if (elapsed >= duration)
            {
                striker.ReleasePinnedPlayer();
            }

            // Brief pause after animation ends before deciding next state
            if (elapsed >= duration + striker.PostAttackPause)
            {
                float dist = striker.Vision.GetDistanceToPlayer();
                if (dist <= striker.AttackRange * 1.05f && striker.Vision.HasLineOfSightToPlayer())
                {
                    Enter();
                }
                else
                {
                    striker.ChangeState(striker.ChargeState);
                }
            }
        }

        public void Exit()
        {
            striker.ReleasePinnedPlayer();
            striker.SetBladeTelegraph(0f);
        }

        public void InterruptAttack()
        {
            interrupted = true;
            firstHitDone = true;
            secondHitDone = true;
            striker.ReleasePinnedPlayer();
        }
    }
}
