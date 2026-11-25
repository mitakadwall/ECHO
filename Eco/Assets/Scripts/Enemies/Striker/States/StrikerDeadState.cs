using Eco.Enemies.FSM;

namespace Eco.Enemies.Striker.States
{
    /// <summary>
    /// Terminal state upon reaching 0 HP: disables navigation/colliders, turns off blades & visor, and plays death clip.
    /// </summary>
    public class StrikerDeadState : IEnemyState
    {
        private readonly StrikerController striker;

        public StrikerDeadState(StrikerController striker)
        {
            this.striker = striker;
        }

        public void Enter()
        {
            striker.ReleasePinnedPlayer();
            striker.StopAgent();
            if (striker.Agent != null)
            {
                striker.Agent.enabled = false;
            }

            striker.SetBladeTelegraph(-1f);
            striker.DisableColliders();
            striker.TriggerDeathAnimation();
        }

        public void Tick(float deltaTime)
        {
        }

        public void Exit()
        {
        }
    }
}
