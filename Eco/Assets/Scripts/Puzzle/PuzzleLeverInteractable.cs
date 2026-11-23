using UnityEngine;
using Eco.Interaction;

namespace Eco.Puzzle
{
    /// <summary>
    /// Interactable color-coded puzzle lever. Tilts downward when pulled with E and notifies Pavimento5PuzzleController.
    /// </summary>
    public class PuzzleLeverInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Pavimento5PuzzleController puzzleController;
        [SerializeField] private PuzzleColor leverColor = PuzzleColor.Cyan;
        [SerializeField] private string colorDisplayName = "Ciano";
        [SerializeField] private float pullAngleDeltaX = -57f;
        [SerializeField] private float tiltSpeed = 10f;

        private Quaternion restLocalRot;
        private Quaternion pulledLocalRot;
        private bool isPulled;

        public PuzzleColor LeverColor => leverColor;
        public bool IsPulled => isPulled;
        public string PromptText => $"Acionar Alavanca {colorDisplayName}";

        private void Awake()
        {
            restLocalRot = transform.localRotation;
            pulledLocalRot = restLocalRot * Quaternion.Euler(pullAngleDeltaX, 0f, 0f);
        }

        private void Update()
        {
            Quaternion target = isPulled ? pulledLocalRot : restLocalRot;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * tiltSpeed);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (puzzleController == null || puzzleController.IsSolved || puzzleController.IsBusy)
                return false;
            return !isPulled;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            isPulled = true;
            puzzleController.NotifyLeverPulled(this);
        }

        public void ResetLever()
        {
            isPulled = false;
        }
    }
}
