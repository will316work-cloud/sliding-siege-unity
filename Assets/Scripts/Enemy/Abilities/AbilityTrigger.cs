namespace SlidingSiege
{
    /// When an EnemyAbility runs. EnemyPhase abilities are queued by
    /// EnemyPhaseRunner in order-index order; every other trigger is
    /// event-driven — AbilityTriggerDispatcher queues them the moment the
    /// event fires and flushes as soon as it is safe (right after a player
    /// attack resolves, or once the enemy phase finishes).
    public enum AbilityTrigger
    {
        EnemyPhase = 0,
        OnSpawn = 1,
        OnDamaged = 2,
        OnCritical = 3,
        OnDeath = 4,
    }
}
