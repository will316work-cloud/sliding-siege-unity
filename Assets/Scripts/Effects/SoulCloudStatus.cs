namespace SlidingSiege
{
    /// Expanded Soul item: attacks that hit any of the enemy's 8-neighbor
    /// halo cells also count as hitting the enemy. Turn countdown is a stub
    /// until the turn system exists.
    public class SoulCloudStatus : StatusEffect
    {
        public int TurnsRemaining = 1;
    }
}
