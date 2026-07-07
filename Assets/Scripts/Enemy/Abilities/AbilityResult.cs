namespace SlidingSiege
{
    /// Mutable result handle passed into EnemyAbility.Execute so coroutine
    /// abilities can report success (post-delays only apply on success).
    public class AbilityResult
    {
        public bool Success;
    }
}
