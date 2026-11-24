using UnityEngine;
using Eco.Interaction;

namespace Eco.Puzzle
{
    /// <summary>
    /// Interactable cylinder hitbox (Cyan, Yellow, or Green) on 'puzzle cores'.
    /// Only becomes interactable after the puzzle has started and finished flashing the sequence.
    /// </summary>
    public class Puzzle2CylinderInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Pavimento2ColorPuzzleController puzzleController;
        [SerializeField] private ColorCylinderType cylinderColor = ColorCylinderType.Cyan;
        [SerializeField] private string colorDisplayName = "Ciano";

        public ColorCylinderType CylinderColor => cylinderColor;
        public string PromptText => $"Acionar Cilindro {colorDisplayName}";

        public bool CanInteract(GameObject interactor)
        {
            return puzzleController != null && puzzleController.IsAcceptingInput;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            puzzleController.SubmitCylinder(cylinderColor);
        }
    }
}
