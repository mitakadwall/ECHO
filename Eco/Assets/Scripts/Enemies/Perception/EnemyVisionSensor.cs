using UnityEngine;

namespace Eco.Enemies.Perception
{
    /// <summary>
    /// Line-of-sight and field-of-view visual sensor for enemy units.
    /// </summary>
    public class EnemyVisionSensor : MonoBehaviour
    {
        [Header("Vision Settings")]
        [SerializeField] private float viewDistance = 22f;
        [SerializeField, Range(10f, 360f)] private float viewAngle = 140f;
        [SerializeField] private float proximityDetectDistance = 4.5f;
        [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.80f, 0f);
        [SerializeField] private float aimVerticalBias = 0.05f;
        [SerializeField] private LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        private Transform playerTransform;
        private Collider playerCollider;
        private bool canSeePlayer;
        private Vector3 lastKnownPlayerPosition;
        private float lastSeenTime = -999f;

        public Transform PlayerTransform => playerTransform;
        public bool CanSeePlayer => canSeePlayer;
        public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
        public float TimeSinceLastSeen => Time.time - lastSeenTime;
        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public Vector3 EyePosition => transform.TransformPoint(eyeOffset);

        private void Start()
        {
            ResolvePlayer();
        }

        public void ResolvePlayer()
        {
            if (playerTransform != null) return;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerCollider = playerObj.GetComponent<CharacterController>();
                if (playerCollider == null)
                {
                    playerCollider = playerObj.GetComponentInChildren<Collider>();
                }
            }
        }

        /// <summary>
        /// Updates line-of-sight against the player. Returns true if visible this tick.
        /// </summary>
        public bool Sense()
        {
            if (playerTransform == null)
            {
                ResolvePlayer();
                if (playerTransform == null)
                {
                    canSeePlayer = false;
                    return false;
                }
            }

            Vector3 eyePos = EyePosition;
            Vector3 targetPos = GetPlayerAimPoint();
            Vector3 toTarget = targetPos - eyePos;
            float distance = toTarget.magnitude;

            if (distance > viewDistance || distance <= 0.001f)
            {
                canSeePlayer = false;
                return false;
            }

            // Close proximity awareness (detects player nearby even outside front cone if unobstructed)
            bool withinProximity = distance <= proximityDetectDistance;

            if (!withinProximity)
            {
                Vector3 flatForward = transform.forward;
                flatForward.y = 0f;
                Vector3 flatToTarget = toTarget;
                flatToTarget.y = 0f;

                if (flatForward.sqrMagnitude > 0.001f && flatToTarget.sqrMagnitude > 0.001f)
                {
                    float angle = Vector3.Angle(flatForward.normalized, flatToTarget.normalized);
                    if (angle > viewAngle * 0.5f)
                    {
                        canSeePlayer = false;
                        return false;
                    }
                }
            }

            if (HasLineOfSight(eyePos, targetPos))
            {
                canSeePlayer = true;
                lastKnownPlayerPosition = playerTransform.position;
                lastSeenTime = Time.time;
                return true;
            }

            canSeePlayer = false;
            return false;
        }

        public bool HasLineOfSightToPlayer()
        {
            if (playerTransform == null)
            {
                ResolvePlayer();
                if (playerTransform == null) return false;
            }

            return HasLineOfSight(EyePosition, GetPlayerAimPoint());
        }

        public bool HasLineOfSight(Vector3 fromPoint, Vector3 toPoint)
        {
            Vector3 dir = toPoint - fromPoint;
            float dist = dir.magnitude;
            if (dist <= 0.001f) return true;

            if (Physics.Raycast(fromPoint, dir / dist, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform)
                {
                    return true;
                }

                if (playerTransform != null &&
                    (hit.collider.transform == playerTransform || hit.collider.transform.IsChildOf(playerTransform)))
                {
                    return true;
                }

                return false;
            }

            return true;
        }

        public Vector3 GetPlayerAimPoint()
        {
            if (playerTransform == null) return transform.position + transform.forward * 5f;
            if (playerCollider != null)
            {
                return playerCollider.bounds.center + Vector3.up * aimVerticalBias;
            }
            return playerTransform.position + Vector3.up * aimVerticalBias;
        }

        public void RegisterExternalSighting(Vector3 worldPosition)
        {
            lastKnownPlayerPosition = worldPosition;
            lastSeenTime = Time.time;
        }

        public float GetDistanceToPlayer()
        {
            if (playerTransform == null) return float.MaxValue;
            return Vector3.Distance(transform.position, playerTransform.position);
        }
    }
}
