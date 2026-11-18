using UnityEngine;

namespace Eco.Combat
{
    /// <summary>
    /// Fragile metal grate that blocks a passage until shattered by an Impact strike.
    /// Regular melee attacks do not break it.
    /// </summary>
    public class FragileGrate : MonoBehaviour, IImpactTarget
    {
        [SerializeField] private float breakThreshold = 10f;
        [SerializeField] private bool destroyOnBreak = false;

        private bool isBroken;

        public bool IsBroken => isBroken;

        public void ReceiveImpact(float impactDamage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (isBroken || impactDamage < breakThreshold) return;

            isBroken = true;
            Debug.Log($"[FragileGrate] '{name}' shattered by Impact!");

            if (destroyOnBreak)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
