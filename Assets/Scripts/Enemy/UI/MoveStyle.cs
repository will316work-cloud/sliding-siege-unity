namespace SlidingSiege
{
    /// How a GridState.MoveEnemy call should be visualized.
    public enum MoveStyle
    {
        Instant, // snap to the new position (e.g. Teleport)
        Slide,   // wrap-split DOTween slide (e.g. ability moves, Gravity Orb)
    }
}
