using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Striker.States
{
    /// <summary>
    /// Patrols doors and narrow passages; immediately alerts nearby allies and charges on spotting the player.
    /// </summary>
    public class StrikerPatrolState : IEnemyState
    {
        private readonly StrikerController striker;
        private int currentWaypointIndex;
        private float waitTimer;
        private bool isWaiting;

        public StrikerPatrolState(StrikerController striker)
        {
            this.striker = striker;
        }

        public void Enter()
        {
            striker.SetVisorPatrolMode();
            striker.SetChargingAnimation(false);
            striker.SetBladeTelegraph(0f);
            isWaiting = false;
            waitTimer = 0f;

            if (striker.IsOnNavMeshReady())
            {
                striker.Agent.updateRotation = true;
                striker.Agent.speed = striker.PatrolSpeed;
                striker.Agent.stoppingDistance = 0.2f;
                striker.Agent.isStopped = false;
                MoveToCurrentWaypoint();
            }
        }

        public void Tick(float deltaTime)
        {
            if (striker.Vision.CanSeePlayer)
            {
                striker.BroadcastAlarmToAllies();
                striker.ChangeState(striker.ChargeState);
                return;
            }

            if (striker.PatrolWaypoints == null || striker.PatrolWaypoints.Length == 0)
            {
                return;
            }

            if (!striker.IsOnNavMeshReady()) return;

            if (isWaiting)
            {
                waitTimer -= deltaTime;
                if (waitTimer <= 0f)
                {
                    isWaiting = false;
                    currentWaypointIndex = (currentWaypointIndex + 1) % striker.PatrolWaypoints.Length;
                    MoveToCurrentWaypoint();
                }
                return;
            }

            if (!striker.Agent.pathPending && striker.Agent.remainingDistance <= striker.WaypointReachThreshold)
            {
                isWaiting = true;
                waitTimer = striker.WaypointWaitTime;
                striker.Agent.isStopped = true;
            }
        }

        public void Exit()
        {
            isWaiting = false;
        }

        private void MoveToCurrentWaypoint()
        {
            if (striker.PatrolWaypoints == null || striker.PatrolWaypoints.Length == 0) return;

            int safeIndex = currentWaypointIndex % striker.PatrolWaypoints.Length;
            Transform target = striker.PatrolWaypoints[safeIndex];
            if (target == null) return;

            striker.Agent.isStopped = false;
            striker.Agent.SetDestination(target.position);
        }
    }
}
