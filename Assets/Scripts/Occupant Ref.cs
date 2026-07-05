namespace SlidingSiege
{
    /// Immutable-by-convention ref stored in a cell's occupant list.
    public struct OccupantRef
    {
        public OccupantKind Kind;
        public int Id;
        public OccupantRef(OccupantKind kind, int id) { Kind = kind; Id = id; }
    }
}
