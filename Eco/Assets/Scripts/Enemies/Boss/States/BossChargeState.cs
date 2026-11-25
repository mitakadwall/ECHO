using UnityEngine;
using UnityEngine.AI;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Boss.States
{
    /// <summary>
    /// Linear hydraulic charge aimed directly at the player.
    /// Deals 40 damage if it hits the player; if the player hides behind a BreakableColumn
    /// and Carcass-0 slams into it, the column breaks, dealing 30 self-damage + stun to the boss.
    /// </summary>
    public class BossChargeState : IEnemyState
    {
        private readonly BossCarcassController boss;
        private Vector3 chargeDirection;
        private float elapsed;
        private bool hitPlayer;

        public BossChargeState(BossCarcassController boss)
        {
            this.boss = boss;
        }

        public void Enter()
        {
            elapsed = 0f;
            hitPlayer = false;
            boss.MarkChargeCooldown();
            boss.SetChargingAnimation(true);

            if (boss.Vision.PlayerTransform != null)
            {
                Vector3 toPlayer = boss.Vision.PlayerTransform.position - boss.transform.position;
                toPlayer.y = 0f;
                chargeDirection = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : boss.transform.forward;
            }
            else
            {
                chargeDirection = boss.transform.forward;
            }

            boss.transform.rotation = Quaternion.LookRotation(chargeDirection);

            if (boss.IsOnNavMeshReady())
            {
                boss.Agent.isStopped = true;
                boss.Agent.ResetPath();
                boss.Agent.updateRotation = false;
            }
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            float step = boss.CurrentChargeSpeed * deltaTime;
            Vector3 probeOrigin = boss.transform.position + Vector3.up * 1.4f;

            // Check collision ahead for BreakableColumn or solid walls
            if (Physics.SphereCast(probeOrigin, 0.95f, chargeDirection, out RaycastHit hit, step + 1.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(boss.transform) && hit.collider.transform != boss.transform)
                {
                    BreakableColumn column = hit.collider.GetComponentInParent<BreakableColumn>();
                    if (column != null && !column.IsBroken)
                    {
                        boss.HandleColumnCrash(column, hit.point, chargeDirection);
                        boss.ChangeState(boss.WalkState);
                        return;
                    }

                    bool isPlayer = boss.Vision.PlayerTransform != null &&
                                    (hit.collider.transform == boss.Vision.PlayerTransform || hit.collider.transform.IsChildOf(boss.Vision.PlayerTransform));

                    if (!isPlayer)
                    {
                        // Hit a wall/obstacle -> stop charge cleanly
                        boss.ApplyStagger(0.6f);
                        boss.ChangeState(boss.WalkState);
                        return;
                    }
                }
            }

            // Advance smoothly along NavMesh
            Vector3 nextPos = boss.transform.position + chargeDirection * step;
            if (boss.IsOnNavMeshReady())
            {
                if (NavMesh.SamplePosition(nextPos, out NavMeshHit navHit, 1.2f, NavMesh.AllAreas))
                {
                    boss.Agent.Move(navHit.position - boss.transform.position);
                }
                else
                {
                    boss.ChangeState(boss.WalkState);
                    return;
                }
            }
            else
            {
                boss.transform.position = nextPos;
            }

            // Check player impact along charge path
            if (!hitPlayer && boss.Vision.PlayerTransform != null)
            {
                Vector3 delta = boss.Vision.PlayerTransform.position - boss.transform.position;
                float vert = Mathf.Abs(delta.y);
                delta.y = 0f;
                if (delta.magnitude <= boss.ChargeHitRadius && vert <= 2.2f)
                {
                    hitPlayer = true;
                    boss.ApplyDamageToPlayer(boss.AttackDamage);
                }
            }

            if (elapsed >= boss.MaxChargeDuration)
            {
                boss.ChangeState(boss.WalkState);
            }
        }

        public void Exit()
        {
            boss.SetChargingAnimation(false);
            if (boss.IsOnNavMeshReady())
            {
                boss.Agent.updateRotation = true;
            }
        }
    }
}
