using UnityEngine;
using UnityEngine.InputSystem;

namespace Eco.Player
{
    /// <summary>
    /// Rapid directional dodge (Dash) used to evade incoming attacks, lunges, and AoE strikes.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerDash : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private Key dashKey = Key.C;

        [Header("Dash Settings")]
        [SerializeField] private float dashSpeed = 22f;
        [SerializeField] private float dashDuration = 0.18f;
        [SerializeField] private float dashCooldown = 0.9f;

        private PlayerController controller;
        private Vector3 dashDirection;
        private float dashTimer;
        private float lastDashTime = -999f;

        public bool IsDashing => dashTimer > 0f;
        public float CooldownRemaining => Mathf.Max(0f, (lastDashTime + dashCooldown) - Time.time);

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            if (IsDashing)
            {
                StopDash();
            }
        }

        private void Update()
        {
            if (IsDashing)
            {
                UpdateDash();
                return;
            }

            if (Keyboard.current == null || Cursor.lockState != CursorLockMode.Locked) return;

            if (Keyboard.current[dashKey].wasPressedThisFrame && Time.time >= lastDashTime + dashCooldown)
            {
                StartDash();
            }
        }

        private void StartDash()
        {
            Vector3 inputDir = controller.PlanarMoveDirection;
            dashDirection = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : transform.forward;

            dashTimer = dashDuration;
            lastDashTime = Time.time;

            controller.SetVerticalVelocity(0f);
            controller.BeginMovementOverride();
        }

        private void UpdateDash()
        {
            controller.MoveCharacter(dashDirection * (dashSpeed * Time.deltaTime));
            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0f)
            {
                StopDash();
            }
        }

        private void StopDash()
        {
            dashTimer = 0f;
            controller.EndMovementOverride();
        }
    }
}
