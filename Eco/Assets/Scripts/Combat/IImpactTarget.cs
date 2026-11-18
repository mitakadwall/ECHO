using UnityEngine;

namespace Eco.Combat
{
    /// <summary>
    /// Contract for targets that react specifically to the player's Impact strike
    /// (e.g. fragile metal grates and light robot defenses).
    /// </summary>
    public interface IImpactTarget
    {
        void ReceiveImpact(float impactDamage, Vector3 hitPoint, Vector3 hitDirection);
    }
}
