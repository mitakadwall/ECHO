using Eco.Enemies.FSM;

namespace Eco.Enemies.Watcher.States
{
    /// <summary>
    /// Terminal state upon reaching 0 HP: stops navigation, disables colliders, and triggers the death animation.
    /// Visor illumination is turned off automatically by EnemyHealth.
    /// </summary>
    public class WatcherDeadState : IEnemyState
    {
        private readonly WatcherController watcher;

        public WatcherDeadState(WatcherController watcher)
        {
            this.watcher = watcher;
        }

        public void Enter()
        {
            watcher.StopAgent();
            if (watcher.Agent != null)
            {
                watcher.Agent.enabled = false;
            }

            watcher.DisableColliders();
            watcher.TriggerDeathAnimation();
        }

        public void Tick(float deltaTime)
        {
            // Terminal state
        }

        public void Exit()
        {
        }
    }
}
