using UnityEngine;
using Eco.Interaction;

namespace Eco.Puzzle
{
    /// <summary>
    /// Interactable monitor that replays the 3-color lever sequence whenever the player presses E.
    /// </summary>
    public class PuzzleMonitorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Pavimento5PuzzleController puzzleController;

        public string PromptText => puzzleController != null && puzzleController.IsSolved
            ? "Porta Desbloqueada"
            : "Ver sequência no Monitor";

        public bool CanInteract(GameObject interactor)
        {
            return puzzleController != null && !puzzleController.IsSolved;
        }

        public void Interact(GameObject interactor)
        {
            if (puzzleController == null) return;
            puzzleController.PlaySequenceOnMonitor();
        }
    }
}
