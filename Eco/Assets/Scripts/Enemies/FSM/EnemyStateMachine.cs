using System;

namespace Eco.Enemies.FSM
{
    /// <summary>
    /// Lightweight Finite State Machine runner for enemy AI.
    /// </summary>
    public class EnemyStateMachine
    {
        public IEnemyState CurrentState { get; private set; }

        public event Action<IEnemyState, IEnemyState> OnStateChanged;

        public void Initialize(IEnemyState initialState)
        {
            if (initialState == null) return;
            CurrentState = initialState;
            CurrentState.Enter();
            OnStateChanged?.Invoke(null, CurrentState);
        }

        public void ChangeState(IEnemyState nextState)
        {
            if (nextState == null || nextState == CurrentState) return;

            IEnemyState previous = CurrentState;
            previous?.Exit();

            CurrentState = nextState;
            CurrentState.Enter();
            OnStateChanged?.Invoke(previous, CurrentState);
        }

        public void Tick(float deltaTime)
        {
            CurrentState?.Tick(deltaTime);
        }
    }
}
