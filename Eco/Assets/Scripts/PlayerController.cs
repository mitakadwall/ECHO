using UnityEngine;
using UnityEngine.InputSystem;

namespace Eco.Player
{
    /// <summary>
    /// First-person character controller using CharacterController and the new Input System
    /// (Keyboard.current / Mouse.current) paired with a Cinemachine camera root.
    /// Supports Double Jump and clean extension hooks for Dash, Wall Run, and Grabhook.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 5.0f;
        [SerializeField] private float sprintSpeed = 8.5f;
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float doubleJumpHeight = 1.4f;
        [SerializeField] private int maxAirJumps = 1;
        [SerializeField] private float gravity = -19.62f;

        [Header("Look Settings")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float minPitch = -85.0f;
        [SerializeField] private float maxPitch = 85.0f;
        [SerializeField] private float cameraRollSpeed = 10.0f;

        private CharacterController characterController;
        private Vector3 velocity;
        private float pitch;
        private float currentRoll;
        private float targetRoll;
        private int airJumpsRemaining;
        private int movementOverrideCount;
        private bool isControlLocked;

        public CharacterController CharacterController => characterController;
        public Transform CameraRoot => cameraRoot;
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsMovementOverridden => movementOverrideCount > 0;
        public bool IsControlLocked => isControlLocked;
        public float Gravity => gravity;
        public Vector3 Velocity => velocity;
        public Vector2 CurrentMoveInput { get; private set; }
        public Vector3 PlanarMoveDirection { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            airJumpsRemaining = maxAirJumps;
            SetCursorLocked(true);
        }

        private void Update()
        {
            if (isControlLocked)
            {
                CurrentMoveInput = Vector2.zero;
                PlanarMoveDirection = Vector3.zero;
                return;
            }

            HandleCursorToggle();
            HandleMouseLook();
            ReadMoveInput();

            if (!IsMovementOverridden)
            {
                HandleMovement();
            }
        }

        private void HandleCursorToggle()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                bool shouldLock = Cursor.lockState != CursorLockMode.Locked;
                SetCursorLocked(shouldLock);
            }
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleMouseLook()
        {
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(Vector3.up * mouseDelta.x);
                pitch = Mathf.Clamp(pitch - mouseDelta.y, minPitch, maxPitch);
            }

            currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * cameraRollSpeed);

            if (cameraRoot != null)
            {
                cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, currentRoll);
            }
        }

        private void ReadMoveInput()
        {
            if (Keyboard.current == null)
            {
                CurrentMoveInput = Vector2.zero;
                PlanarMoveDirection = Vector3.zero;
                return;
            }

            Vector2 inputVector = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) inputVector.y += 1f;
            if (Keyboard.current.sKey.isPressed) inputVector.y -= 1f;
            if (Keyboard.current.aKey.isPressed) inputVector.x -= 1f;
            if (Keyboard.current.dKey.isPressed) inputVector.x += 1f;

            if (inputVector.sqrMagnitude > 1f)
            {
                inputVector.Normalize();
            }

            CurrentMoveInput = inputVector;
            PlanarMoveDirection = transform.right * inputVector.x + transform.forward * inputVector.y;
        }

        private void HandleMovement()
        {
            if (Keyboard.current == null) return;

            bool isGrounded = characterController.isGrounded;
            if (isGrounded)
            {
                if (velocity.y < 0f)
                {
                    velocity.y = -2f;
                }
                airJumpsRemaining = maxAirJumps;
            }

            bool isSprinting = Keyboard.current.leftShiftKey.isPressed;
            float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

            characterController.Move(PlanarMoveDirection * (currentSpeed * Time.deltaTime));

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (isGrounded)
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
                else if (airJumpsRemaining > 0)
                {
                    airJumpsRemaining--;
                    velocity.y = Mathf.Sqrt(doubleJumpHeight * -2f * gravity);
                }
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        public void BeginMovementOverride()
        {
            movementOverrideCount++;
        }

        public void EndMovementOverride()
        {
            movementOverrideCount = Mathf.Max(0, movementOverrideCount - 1);
        }

        public void SetVerticalVelocity(float yVelocity)
        {
            velocity.y = yVelocity;
        }

        public void SetVelocity(Vector3 newVelocity)
        {
            velocity = newVelocity;
        }

        public void ResetAirJumps()
        {
            airJumpsRemaining = maxAirJumps;
        }

        public void SetCameraRoll(float rollDegrees)
        {
            targetRoll = rollDegrees;
        }

        public CollisionFlags MoveCharacter(Vector3 motion)
        {
            return characterController.Move(motion);
        }

        /// <summary>
        /// Locks or unlocks player movement, mouse look, and Escape cursor toggling
        /// while interacting with diegetic terminals.
        /// </summary>
        public void SetControlLocked(bool locked)
        {
            if (isControlLocked == locked) return;

            isControlLocked = locked;
            if (locked)
            {
                BeginMovementOverride();
                velocity = Vector3.zero;
                CurrentMoveInput = Vector2.zero;
                PlanarMoveDirection = Vector3.zero;
                SetCursorLocked(false);
            }
            else
            {
                EndMovementOverride();
                SetCursorLocked(true);
            }
        }

        /// <summary>
        /// Safely teleports the CharacterController to a saved position and yaw.
        /// </summary>
        public void TeleportTo(Vector3 worldPosition, float yawDegrees)
        {
            bool wasEnabled = characterController != null && characterController.enabled;
            if (wasEnabled) characterController.enabled = false;

            transform.position = worldPosition;
            transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            velocity = Vector3.zero;

            if (wasEnabled) characterController.enabled = true;
        }
    }
}
