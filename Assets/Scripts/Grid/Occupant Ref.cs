namespace SlidingSiege
{
    /// Immutable ref stored in a cell's occupant list.
    public struct OccupantRef
    {
        public OccupantKind Kind { get; }
        public int Id { get; }
        public OccupantRef(OccupantKind kind, int id) { Kind = kind; Id = id; }
    }
}
