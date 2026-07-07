namespace SlidingSiege
{
    /// Where a SpawnAbility looks for valid spawn cells.
    public enum SpawnPlacementMode
    {
        AdjacentToOwner, // 8-neighborhood halo around the owner's footprint
        AnywhereFree,    // any cell where the spawned footprint fits
    }
}
