using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Watcher.States
{
    /// <summary>
    /// Patrols fixed waypoints along corridors/catwalks and scans visually for the player.
    /// </summary>
    public class WatcherPatrolState : IEnemyState
    {
        private readonly WatcherController watcher;
        private int currentWaypointIndex;
        private float waitTimer;
        private bool isWaiting;

        public WatcherPatrolState(WatcherController watcher)
        {
            this.watcher = watcher;
        }

        public void Enter()
        {
            watcher.SetVisorPatrolMode();
            isWaiting = false;
            waitTimer = 0f;

            if (watcher.IsOnNavMeshReady())
            {
                watcher.Agent.updateRotation = true;
                watcher.Agent.speed = watcher.PatrolSpeed;
                watcher.Agent.stoppingDistance = 0.2f;
                watcher.Agent.isStopped = false;
                MoveToCurrentWaypoint();
            }
        }

        public void Tick(float deltaTime)
        {
            if (watcher.Vision.CanSeePlayer)
            {
                watcher.ChangeState(watcher.AlertState);
                return;
            }

            if (watcher.PatrolWaypoints == null || watcher.PatrolWaypoints.Length == 0)
            {
                return;
            }

            if (!watcher.IsOnNavMeshReady()) return;

            if (isWaiting)
            {
                waitTimer -= deltaTime;
                if (waitTimer <= 0f)
                {
                    isWaiting = false;
                    currentWaypointIndex = (currentWaypointIndex + 1) % watcher.PatrolWaypoints.Length;
                    MoveToCurrentWaypoint();
                }
                return;
            }

            if (!watcher.Agent.pathPending && watcher.Agent.remainingDistance <= watcher.WaypointReachThreshold)
            {
                isWaiting = true;
                waitTimer = watcher.WaypointWaitTime;
                watcher.Agent.isStopped = true;
            }
        }

        public void Exit()
        {
            isWaiting = false;
        }

        private void MoveToCurrentWaypoint()
        {
            if (watcher.PatrolWaypoints == null || watcher.PatrolWaypoints.Length == 0) return;

            int safeIndex = currentWaypointIndex % watcher.PatrolWaypoints.Length;
            Transform target = watcher.PatrolWaypoints[safeIndex];
            if (target == null) return;

            watcher.Agent.isStopped = false;
            watcher.Agent.SetDestination(target.position);
        }
    }
}
