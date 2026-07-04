// ============================================================
// OCCUPANT REF  (NEW in Bunch 2)
// Translated from: the { kind: 'enemy'|'sporeCloud', id } objects
// that grid-logic.js stores in every state.grid[r][c] array.
//
// A struct: value-equality for free, no allocation, and safe to
// share between shift-history snapshots exactly like the JS refs
// ("immutable-by-convention — never mutated in place, always
// replaced wholesale by add/removeXRefAt").
// ============================================================

public enum OccupantKind { Enemy, SporeCloud }

[System.Serializable]
public struct OccupantRef
{
    public OccupantKind Kind;
    public int Id;

    public OccupantRef(OccupantKind kind, int id) { Kind = kind; Id = id; }

    public static OccupantRef EnemyRef(int id) => new OccupantRef(OccupantKind.Enemy, id);
    public static OccupantRef SporeRef(int id) => new OccupantRef(OccupantKind.SporeCloud, id);

    public override string ToString() => $"{Kind}:{Id}";
}
