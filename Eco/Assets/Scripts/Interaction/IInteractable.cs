using UnityEngine;

namespace Eco.Interaction
{
    /// <summary>
    /// Common contract for world objects the player can interact with using the Interact key (E).
    /// </summary>
    public interface IInteractable
    {
        string PromptText { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
