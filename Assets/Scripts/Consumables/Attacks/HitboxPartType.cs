namespace SlidingSiege
{
    /// What cells a HitboxPart covers, relative to its origin (anchor + Offset).
    public enum HitboxPartType
    {
        Row,          // full row through the origin
        Column,       // full column through the origin
        DiagonalUp,   // full ↗ diagonal through the origin
        DiagonalDown, // full ↘ diagonal through the origin
        Grid,         // explicit GridCells offsets from the origin
        Rectangle,    // RectSize area with its top-left at the origin (full or perimeter)
    }
}
