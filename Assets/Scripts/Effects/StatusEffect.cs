namespace SlidingSiege
{
    /// Base class for enemy status effects. No turn ticking yet (no turn
    /// system); expiry hooks can be added when turns land.
    public abstract class StatusEffect
    {
        /// Multiplier applied to damage this enemy takes (1 = no change).
        public virtual float DamageTakenMultiplier => 1f;
    }
}
