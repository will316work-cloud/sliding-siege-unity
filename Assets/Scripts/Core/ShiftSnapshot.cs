// ============================================================
// SHIFT SNAPSHOT  (*** PATCHED in Bunch 2 — replaces Bunch 1 file ***)
// CHANGE LOG vs Bunch 1: Grid element type is List<OccupantRef>.
// See Bunch 1 header for translation notes (pushShiftHistory /
// revertLastShift in grid-logic.js).
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public class ShiftSnapshot
{
    /// <summary>Deep-enough copy per JS snapshotGrid(): new per-cell lists;
    /// OccupantRef is a value-type struct so sharing is inherently safe.</summary>
    public List<OccupantRef>[,] Grid;

    public HashSet<int> LinesShiftedThisTurn;
    public HashSet<int> RowsTouched;
    public HashSet<int> ColsTouched;
    public int DecayStepCounter;
    public Dictionary<int, Vector2Int> EnemyAnchors;
    public HashSet<int> TouchedIndices;
}
