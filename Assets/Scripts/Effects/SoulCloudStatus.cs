namespace SlidingSiege
{
    /// Expanded Soul item: attacks that hit any of the enemy's 8-neighbor
    /// halo cells also count as hitting the enemy. Lasts one enemy phase.
    /// (Previously shadowed the base TurnsRemaining with its own field,
    /// which made the runner treat it as permanent — fixed.)
    public class SoulCloudStatus : StatusEffect
    {
        public SoulCloudStatus(int turns = 1) : base(turns) { }
    }
}
