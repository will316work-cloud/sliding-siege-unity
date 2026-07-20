namespace SlidingSiege
{
    /// Base class for enemy status effects. Turn-limited statuses pass a
    /// positive turn count; Enemy.TickStatuses (called by EnemyPhaseRunner
    /// at the end of each phase) ticks them down and drops expired ones.
    public abstract class StatusEffect
    {
        protected StatusEffect(int turns = -1) => TurnsRemaining = turns;

        /// Enemy phases left before this status expires; negative = permanent.
        public int TurnsRemaining { get; private set; }

        /// Advances the countdown one phase; true once the status expired.
        /// Permanent statuses (negative) never tick or expire.
        public bool TickAndCheckExpired()
        {
            if (TurnsRemaining < 0) return false;
            TurnsRemaining--;
            return TurnsRemaining <= 0;
        }

        /// Multiplier applied to damage this enemy takes (1 = no change).
        public virtual float DamageTakenMultiplier => 1f;

        /// True if this status prevents the enemy from acting (stun etc.).
        public virtual bool PreventsAction => false;

        /// Independent copy (own expiry countdown) for transferring a
        /// status onto another enemy, e.g. SpawnAbility replication.
        public StatusEffect Clone() => (StatusEffect)MemberwiseClone();
    }
}
