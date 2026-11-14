using UnityEngine;
using UnityEngine.InputSystem;

namespace Eco.Player
{
    /// <summary>
    /// First-person character controller using CharacterController and the new Input System
    /// (Keyboard.current / Mouse.current) paired with a Cinemachine camera root.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 5.0f;
        [SerializeField] private float sprintSpeed = 8.5f;
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -19.62f;

        [Header("Look Settings")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float minPitch = -85.0f;
        [SerializeField] private float maxPitch = 85.0f;

        private CharacterController characterController;
        private Vector3 velocity;
        private float pitch;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            SetCursorLocked(true);
        }

        private void Update()
        {
            HandleCursorToggle();
            HandleMouseLook();
            HandleMovement();
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
            if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

            // Yaw (horizontal) on player body
            transform.Rotate(Vector3.up * mouseDelta.x);

            // Pitch (vertical) on camera root
            pitch = Mathf.Clamp(pitch - mouseDelta.y, minPitch, maxPitch);
            if (cameraRoot != null)
            {
                cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            if (Keyboard.current == null) return;

            bool isGrounded = characterController.isGrounded;
            if (isGrounded && velocity.y < 0f)
            {
                velocity.y = -2f;
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

            bool isSprinting = Keyboard.current.leftShiftKey.isPressed;
            float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

            Vector3 move = transform.right * inputVector.x + transform.forward * inputVector.y;
            characterController.Move(move * (currentSpeed * Time.deltaTime));

            if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }
    }
}
