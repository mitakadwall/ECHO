using UnityEngine;
using Eco.Player;

namespace Eco.Interaction
{
    public enum EquipmentSlot
    {
        Weapon,
        Shield
    }

    /// <summary>
    /// World pickup placed in the scene (e.g. metal pipe or metal tray on the equipment table).
    /// Equips the corresponding slot on PlayerCombat when the player presses E nearby.
    /// </summary>
    public class EquipmentPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private EquipmentSlot slot = EquipmentSlot.Weapon;
        [SerializeField] private string displayName = "Equipamento";

        public EquipmentSlot Slot => slot;
        public string DisplayName => displayName;
        public string PromptText => $"Pegar {displayName}";

        public void Configure(EquipmentSlot targetSlot, string itemLabel)
        {
            slot = targetSlot;
            displayName = itemLabel;
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!gameObject.activeInHierarchy || interactor == null) return false;
            var combat = interactor.GetComponent<PlayerCombat>();
            if (combat == null) return false;
            return slot == EquipmentSlot.Weapon ? !combat.HasWeapon : !combat.HasShield;
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            TryPickUp(interactor.GetComponent<PlayerCombat>());
        }

        public bool TryPickUp(PlayerCombat combat)
        {
            if (combat == null) return false;

            if (slot == EquipmentSlot.Weapon)
            {
                if (combat.HasWeapon) return false;
                combat.EquipWeapon();
            }
            else
            {
                if (combat.HasShield) return false;
                combat.EquipShield();
            }

            Debug.Log($"[EquipmentPickup] Equipped {displayName} ({slot}).");
            gameObject.SetActive(false);
            return true;
        }
    }
}
