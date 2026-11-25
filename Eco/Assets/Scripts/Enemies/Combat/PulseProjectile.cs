using UnityEngine;
using Eco.Combat;

namespace Eco.Enemies.Combat
{
    /// <summary>
    /// Ballistic pulse round fired from the Unit-04 Watcher's integrated rifle.
    /// Uses continuous raycast stepping to prevent high-speed tunneling and respects IDamageable (including Player shield).
    /// </summary>
    public class PulseProjectile : MonoBehaviour
    {
        [Header("Ballistics")]
        [SerializeField] private float speed = 28f;
        [SerializeField] private float damage = 8f;
        [SerializeField] private float maxLifetime = 3.5f;
        [SerializeField] private float hitRadius = 0.14f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

        private Vector3 moveDirection;
        private Transform ownerRoot;
        private float spawnTime;
        private bool launched;

        public void Launch(Vector3 direction, Transform owner, float overrideDamage = -1f, float overrideSpeed = -1f)
        {
            moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            ownerRoot = owner;
            if (overrideDamage > 0f) damage = overrideDamage;
            if (overrideSpeed > 0f) speed = overrideSpeed;

            transform.rotation = Quaternion.LookRotation(moveDirection);
            spawnTime = Time.time;
            launched = true;
        }

        private void Update()
        {
            if (!launched) return;

            if (Time.time - spawnTime >= maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            float stepDistance = speed * Time.deltaTime;
            Vector3 currentPos = transform.position;

            if (Physics.SphereCast(currentPos, hitRadius, moveDirection, out RaycastHit hit, stepDistance, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (ownerRoot != null && (hit.collider.transform == ownerRoot || hit.collider.transform.IsChildOf(ownerRoot)))
                {
                    transform.position = currentPos + moveDirection * stepDistance;
                    return;
                }

                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage);
                }

                transform.position = hit.point;
                Destroy(gameObject);
                return;
            }

            transform.position = currentPos + moveDirection * stepDistance;
        }
    }
}
