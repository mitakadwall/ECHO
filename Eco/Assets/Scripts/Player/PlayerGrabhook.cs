using UnityEngine;
using UnityEngine.InputSystem;

namespace Eco.Player
{
    /// <summary>
    /// Fires a grabhook toward hookable structures or enemies, pulling the player
    /// along a visible cable to cross gaps, reach high structures, or close distance.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerGrabhook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform hookOrigin;
        [SerializeField] private LineRenderer cableRenderer;

        [Header("Hook Settings")]
        [SerializeField] private LayerMask hookableLayer = ~0;
        [SerializeField] private string hookableTag = "Hookable";
        [SerializeField] private bool requireTag = true;
        [SerializeField] private float maxHookDistance = 28f;
        [SerializeField] private float aimAssistRadius = 0.35f;
        [SerializeField] private float pullSpeed = 26f;
        [SerializeField] private float stopDistance = 2.0f;
        [SerializeField] private float maxPullDuration = 1.6f;
        [SerializeField] private float hookCooldown = 1.2f;
        [SerializeField] private float releaseJumpBoost = 6.5f;

        private PlayerController controller;
        private Transform hookedTarget;
        private Vector3 hookedLocalPoint;
        private Vector3 hookedWorldPoint;
        private bool isPulling;
        private float pullTimer;
        private float lastHookTime = -999f;

        public bool IsPulling => isPulling;
        public float CooldownRemaining => Mathf.Max(0f, (lastHookTime + hookCooldown) - Time.time);

        private void Awake()
        {
            controller = GetComponent<PlayerController>();

            if (cableRenderer == null)
            {
                cableRenderer = GetComponent<LineRenderer>();
            }

            if (cableRenderer != null)
            {
                cableRenderer.positionCount = 2;
                cableRenderer.enabled = false;
            }
        }

        private void OnDisable()
        {
            if (isPulling)
            {
                StopPull(false);
            }
        }

        private void Update()
        {
            if (isPulling)
            {
                UpdatePull();
                return;
            }

            if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;

            if (Mouse.current.rightButton.wasPressedThisFrame && Time.time >= lastHookTime + hookCooldown)
            {
                TryFireHook();
            }
        }

        private void TryFireHook()
        {
            Transform cam = controller.CameraRoot;
            if (cam == null) return;

            bool hitValid = Physics.SphereCast(
                cam.position,
                aimAssistRadius,
                cam.forward,
                out RaycastHit hit,
                maxHookDistance,
                hookableLayer,
                QueryTriggerInteraction.Ignore);

            if (!hitValid) return;
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform) return;
            if (requireTag && !string.IsNullOrEmpty(hookableTag) && !hit.collider.CompareTag(hookableTag)) return;

            StartPull(hit.collider.transform, hit.point);
        }

        private void StartPull(Transform target, Vector3 worldHitPoint)
        {
            isPulling = true;
            pullTimer = maxPullDuration;
            lastHookTime = Time.time;

            hookedTarget = target;
            hookedWorldPoint = worldHitPoint;
            hookedLocalPoint = target != null ? target.InverseTransformPoint(worldHitPoint) : worldHitPoint;

            controller.SetVerticalVelocity(0f);
            controller.ResetAirJumps();
            controller.BeginMovementOverride();

            if (cableRenderer != null)
            {
                cableRenderer.enabled = true;
                UpdateCableVisual();
            }
        }

        private void UpdatePull()
        {
            if (hookedTarget != null)
            {
                hookedWorldPoint = hookedTarget.TransformPoint(hookedLocalPoint);
            }

            Vector3 toTarget = hookedWorldPoint - transform.position;
            float distance = toTarget.magnitude;

            if (distance <= stopDistance || pullTimer <= 0f)
            {
                StopPull(false);
                return;
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StopPull(true);
                return;
            }

            Vector3 direction = toTarget / distance;
            controller.MoveCharacter(direction * (pullSpeed * Time.deltaTime));
            pullTimer -= Time.deltaTime;

            UpdateCableVisual();
        }

        private void StopPull(bool applyReleaseJump)
        {
            isPulling = false;
            hookedTarget = null;
            controller.EndMovementOverride();

            if (applyReleaseJump)
            {
                controller.SetVerticalVelocity(releaseJumpBoost);
                controller.ResetAirJumps();
            }
            else
            {
                controller.SetVerticalVelocity(2.5f);
            }

            if (cableRenderer != null)
            {
                cableRenderer.enabled = false;
            }
        }

        private void UpdateCableVisual()
        {
            if (cableRenderer == null || !cableRenderer.enabled) return;

            Vector3 startPos = hookOrigin != null
                ? hookOrigin.position
                : (controller.CameraRoot != null ? controller.CameraRoot.position - Vector3.up * 0.25f : transform.position);

            cableRenderer.SetPosition(0, startPos);
            cableRenderer.SetPosition(1, hookedWorldPoint);
        }
    }
}
