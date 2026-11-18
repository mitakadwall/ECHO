using UnityEngine;
using UnityEngine.InputSystem;

namespace Eco.Player
{
    /// <summary>
    /// Allows horizontal wall running along valid vertical surfaces while airborne,
    /// with smooth camera roll tilt and wall-jump exit impulse.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerWallRun : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private LayerMask wallRunLayer = ~0;
        [SerializeField] private string wallRunTag = "WallRunnable";
        [SerializeField] private bool requireTag = true;
        [SerializeField] private float wallCheckDistance = 0.85f;
        [SerializeField] private float minHeightAboveGround = 0.6f;

        [Header("Wall Run Settings")]
        [SerializeField] private float wallRunSpeed = 9.5f;
        [SerializeField] private float wallRunGravity = -2.5f;
        [SerializeField] private float maxWallRunDuration = 2.0f;
        [SerializeField] private float wallJumpUpForce = 7.5f;
        [SerializeField] private float wallJumpSideForce = 8.0f;
        [SerializeField] private float wallExitCooldown = 0.25f;
        [SerializeField] private float cameraTiltAngle = 12f;

        private PlayerController controller;
        private bool isWallRunning;
        private bool wallOnLeft;
        private bool wallOnRight;
        private RaycastHit leftWallHit;
        private RaycastHit rightWallHit;
        private float wallRunTimer;
        private float exitCooldownTimer;
        private Vector3 wallJumpMomentum;
        private float wallJumpMomentumTimer;

        public bool IsWallRunning => isWallRunning;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            if (isWallRunning)
            {
                StopWallRun();
            }
        }

        private void Update()
        {
            if (exitCooldownTimer > 0f)
            {
                exitCooldownTimer -= Time.deltaTime;
            }

            if (wallJumpMomentumTimer > 0f)
            {
                wallJumpMomentumTimer -= Time.deltaTime;
                controller.MoveCharacter(wallJumpMomentum * Time.deltaTime);
            }

            CheckForWalls();

            bool pressingForward = Keyboard.current != null && Keyboard.current.wKey.isPressed;
            bool canWallRun = (wallOnLeft || wallOnRight)
                && pressingForward
                && !controller.IsGrounded
                && IsAboveMinimumHeight()
                && exitCooldownTimer <= 0f;

            if (!isWallRunning)
            {
                if (canWallRun && !controller.IsMovementOverridden)
                {
                    StartWallRun();
                }
            }
            else
            {
                if (!canWallRun || wallRunTimer <= 0f)
                {
                    StopWallRun();
                    return;
                }

                UpdateWallRun();
            }
        }

        private void CheckForWalls()
        {
            Vector3 origin = transform.position + Vector3.up * 0.9f;
            wallOnRight = Physics.Raycast(origin, transform.right, out rightWallHit, wallCheckDistance, wallRunLayer, QueryTriggerInteraction.Ignore)
                && IsValidWall(rightWallHit.collider);
            wallOnLeft = Physics.Raycast(origin, -transform.right, out leftWallHit, wallCheckDistance, wallRunLayer, QueryTriggerInteraction.Ignore)
                && IsValidWall(leftWallHit.collider);
        }

        private bool IsValidWall(Collider col)
        {
            if (!requireTag || string.IsNullOrEmpty(wallRunTag)) return true;
            return col.CompareTag(wallRunTag);
        }

        private bool IsAboveMinimumHeight()
        {
            return !Physics.Raycast(transform.position, Vector3.down, minHeightAboveGround, ~0, QueryTriggerInteraction.Ignore);
        }

        private void StartWallRun()
        {
            isWallRunning = true;
            wallRunTimer = maxWallRunDuration;
            controller.SetVerticalVelocity(0f);
            controller.ResetAirJumps();
            controller.BeginMovementOverride();
        }

        private void UpdateWallRun()
        {
            wallRunTimer -= Time.deltaTime;

            Vector3 wallNormal = wallOnRight ? rightWallHit.normal : leftWallHit.normal;
            Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);

            if (Vector3.Dot(transform.forward, wallForward) < 0f)
            {
                wallForward = -wallForward;
            }

            float tilt = wallOnRight ? cameraTiltAngle : -cameraTiltAngle;
            controller.SetCameraRoll(tilt);

            Vector3 motion = wallForward * wallRunSpeed;
            motion.y = wallRunGravity;
            controller.MoveCharacter(motion * Time.deltaTime);

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                PerformWallJump(wallNormal);
            }
        }

        private void PerformWallJump(Vector3 wallNormal)
        {
            StopWallRun();
            exitCooldownTimer = wallExitCooldown;
            controller.SetVerticalVelocity(wallJumpUpForce);
            controller.ResetAirJumps();

            wallJumpMomentum = wallNormal * wallJumpSideForce;
            wallJumpMomentumTimer = 0.22f;
        }

        private void StopWallRun()
        {
            isWallRunning = false;
            controller.SetCameraRoll(0f);
            controller.EndMovementOverride();
        }
    }
}
