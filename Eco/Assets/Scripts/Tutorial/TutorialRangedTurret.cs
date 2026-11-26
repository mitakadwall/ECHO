using UnityEngine;
using Eco.Enemies.Combat;
using Eco.Player;

namespace Eco.Tutorial
{
    /// <summary>
    /// M6 training ranged turret attached to 'ranged M6'.
    /// Activates the floor shield projection ('zona M6') when the player approaches,
    /// and fires Watcher PulseProjectiles from the turret's cylinder barrel so the player
    /// can practice blocking with the shield.
    /// </summary>
    public class TutorialRangedTurret : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PulseProjectile projectilePrefab;
        [SerializeField] private GameObject shieldZoneObject;
        [SerializeField] private Vector3 muzzleWorldOffset = new Vector3(1.50f, 0.25f, 0f);

        [Header("Activation & Firing")]
        [SerializeField] private float zoneRevealDistance = 13.5f;
        [SerializeField] private float fireMaxForwardDistance = 12.5f;
        [SerializeField] private float fireMinForwardDistance = 0.6f;
        [SerializeField] private float fireHalfLaneWidth = 2.6f;
        [SerializeField] private float burstInterval = 2.0f;
        [SerializeField] private int shotsPerBurst = 3;
        [SerializeField] private float timeBetweenShots = 0.22f;
        [SerializeField] private float projectileDamage = 8f;
        [SerializeField] private float projectileSpeed = 24f;

        private Transform playerTransform;
        private CharacterController playerController;
        private Vector3 muzzleWorldPos;
        private bool isActivated;
        private int shotsRemainingInBurst;
        private float nextShotTime;
        private float nextBurstTime;

        public bool IsActivated => isActivated;

        public void Configure(PulseProjectile prefab, GameObject shieldZone, Vector3 muzzlePos)
        {
            projectilePrefab = prefab;
            shieldZoneObject = shieldZone;
            muzzleWorldOffset = muzzlePos - transform.position;
        }

        private void Awake()
        {
            Renderer r = GetComponent<Renderer>();
            if (r != null)
            {
                // Barrel tip on the +X front face of ranged M6, level with player chest height
                muzzleWorldPos = new Vector3(r.bounds.max.x + 0.12f, r.bounds.center.y + 0.08f, r.bounds.center.z);
            }
            else
            {
                muzzleWorldPos = transform.position + muzzleWorldOffset;
            }
        }

        private void Start()
        {
            // Keep the shield projection hidden until the player approaches ranged M6
            if (shieldZoneObject != null)
            {
                shieldZoneObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null)
                {
                    playerTransform = pc.transform;
                    playerController = pc.GetComponent<CharacterController>();
                }
                else return;
            }

            Vector3 targetPos = GetPlayerChestWorldPosition();
            Vector3 offsetFromMuzzle = targetPos - muzzleWorldPos;

            // ranged M6 faces +X toward zona M6 (same Z = -4.5)
            float forwardDist = offsetFromMuzzle.x;
            float lateralDist = Mathf.Abs(offsetFromMuzzle.z);
            float verticalDist = Mathf.Abs(offsetFromMuzzle.y);

            // Show floor shield projection when player approaches the ranged area
            bool nearRangedArea = forwardDist >= -1.0f && forwardDist <= zoneRevealDistance && lateralDist <= 5.0f && verticalDist <= 3.5f;
            if (shieldZoneObject != null && shieldZoneObject.activeSelf != nearRangedArea)
            {
                shieldZoneObject.SetActive(nearRangedArea);
            }

            // Only fire when the player is actually IN FRONT of the turret barrel lane and visible
            bool inFrontLane = forwardDist >= fireMinForwardDistance
                && forwardDist <= fireMaxForwardDistance
                && lateralDist <= fireHalfLaneWidth
                && verticalDist <= 2.5f
                && HasClearLineOfSight(targetPos);

            if (inFrontLane && !isActivated)
            {
                isActivated = true;
                nextBurstTime = Time.time + 0.55f;
                shotsRemainingInBurst = 0;
            }
            else if (!inFrontLane && isActivated)
            {
                isActivated = false;
                shotsRemainingInBurst = 0;
            }

            if (!isActivated || projectilePrefab == null) return;

            if (shotsRemainingInBurst > 0)
            {
                if (Time.time >= nextShotTime)
                {
                    FireShot(targetPos);
                    shotsRemainingInBurst--;
                    nextShotTime = Time.time + timeBetweenShots;
                    if (shotsRemainingInBurst <= 0)
                    {
                        nextBurstTime = Time.time + burstInterval;
                    }
                }
            }
            else if (Time.time >= nextBurstTime)
            {
                shotsRemainingInBurst = shotsPerBurst;
                nextShotTime = Time.time;
            }
        }

        private Vector3 GetPlayerChestWorldPosition()
        {
            if (playerController != null)
            {
                return playerController.bounds.center;
            }
            return playerTransform.position;
        }

        private bool HasClearLineOfSight(Vector3 targetPos)
        {
            Vector3 toTarget = targetPos - muzzleWorldPos;
            float dist = toTarget.magnitude;
            if (dist < 0.1f) return true;

            if (Physics.Raycast(muzzleWorldPos, toTarget / dist, out RaycastHit hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.transform == playerTransform || hit.collider.transform.IsChildOf(playerTransform);
            }
            return true;
        }

        private void FireShot(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - muzzleWorldPos).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.right;

            PulseProjectile proj = Instantiate(projectilePrefab, muzzleWorldPos, Quaternion.LookRotation(dir));
            proj.Launch(dir, transform, projectileDamage, projectileSpeed);
        }
    }
}
