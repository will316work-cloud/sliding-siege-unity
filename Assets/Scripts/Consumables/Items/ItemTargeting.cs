namespace SlidingSiege
{
    /// What an item needs targeted before it can be confirmed.
    public enum ItemTargeting
    {
        None,          // no target (Extra Swing)
        Cell,          // any cell (Gravity Orb)
        Enemy,         // a cell containing an enemy (Expanded Soul, Damage Multiplier)
        EnemyThenCell, // an enemy, then a destination cell (Teleport)
    }
}
