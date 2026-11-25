using UnityEngine;
using Eco.Enemies.FSM;

namespace Eco.Enemies.Watcher.States
{
    /// <summary>
    /// Triggered when the Watcher spots the player: plays the alert animation, switches visor
    /// to combat red, and broadcasts an alarm to nearby allied enemies.
    /// </summary>
    public class WatcherAlertState : IEnemyState
    {
        private readonly WatcherController watcher;
        private float elapsed;
        private bool alarmSent;

        public WatcherAlertState(WatcherController watcher)
        {
            this.watcher = watcher;
        }

        public void Enter()
        {
            elapsed = 0f;
            alarmSent = false;

            watcher.SetVisorCombatMode();
            watcher.StopAgent();
            watcher.TriggerAlertAnimation();
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            Vector3 lookTarget = watcher.Vision.PlayerTransform != null
                ? watcher.Vision.PlayerTransform.position
                : watcher.Vision.LastKnownPlayerPosition;
            watcher.RotateTowards(lookTarget, deltaTime);

            // Trigger alarm broadcast slightly into the alert animation
            if (!alarmSent && elapsed >= 0.25f)
            {
                alarmSent = true;
                watcher.BroadcastAlarmToAllies();
            }

            if (elapsed >= watcher.AlertDuration)
            {
                watcher.ChangeState(watcher.AttackState);
            }
        }

        public void Exit()
        {
            if (!alarmSent && !watcher.Health.IsDead)
            {
                alarmSent = true;
                watcher.BroadcastAlarmToAllies();
            }
        }
    }
}
