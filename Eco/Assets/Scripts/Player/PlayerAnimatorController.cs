using UnityEngine;
using UnityEngine.InputSystem;

namespace Eco.Player
{
    /// <summary>
    /// Drives the Eco character Animator (idle / caminhada) based on PlayerController movement state.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimatorController : MonoBehaviour
    {
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private float dampTime = 0.1f;
        [SerializeField] private float sprintAnimSpeed = 1.45f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

        private PlayerController controller;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            if (characterAnimator == null || controller == null) return;

            float moveMag = controller.IsMovementOverridden ? 0f : controller.CurrentMoveInput.magnitude;
            bool isSprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && moveMag > 0.1f;

            characterAnimator.SetFloat(SpeedHash, moveMag, dampTime, Time.deltaTime);
            characterAnimator.SetBool(IsGroundedHash, controller.IsGrounded);
            characterAnimator.speed = isSprinting ? sprintAnimSpeed : 1f;
        }
    }
}
