namespace Eco.Enemies.FSM
{
    /// <summary>
    /// Contract for a single state in an enemy Finite State Machine.
    /// </summary>
    public interface IEnemyState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
