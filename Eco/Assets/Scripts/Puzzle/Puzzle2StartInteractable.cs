using UnityEngine;
using Eco.Interaction;

namespace Eco.Puzzle
{
    /// <summary>
    /// First-time interactable placed on 'puzzle cores'.
    /// Starts the Genius color sequence once; afterwards only the individual cylinders are interactable.
    /// </summary>
    public class Puzzle2StartInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Pavimento2ColorPuzzleController puzzleController;

        public string PromptText => "Iniciar Puzzle de Cores";

        public bool CanInteract(GameObject interactor)
        {
            return puzzleController != null && !puzzleController.HasStarted && !puzzleController.IsSolved;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            puzzleController.StartPuzzle();
        }
    }
}
