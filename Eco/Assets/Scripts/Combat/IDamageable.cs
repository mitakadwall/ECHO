namespace Eco.Combat
{
    /// <summary>
    /// Contract for any entity capable of receiving damage.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
    }
}
