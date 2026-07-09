namespace SlidingSiege
{
    /// Base class for enemy status effects. Turn-limited statuses set
    /// TurnsRemaining; EnemyPhaseRunner ticks them down at the end of each
    /// enemy phase and removes the expired ones.
    public abstract class StatusEffect
    {
        /// Enemy phases left before this status expires; negative = permanent.
        public int TurnsRemaining = -1;

        /// Multiplier applied to damage this enemy takes (1 = no change).
        public virtual float DamageTakenMultiplier => 1f;

        /// True if this status prevents the enemy from acting (stun etc.).
        public virtual bool PreventsAction => false;
    }
}
