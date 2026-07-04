// ============================================================
// GRID — LOGIC
// Translated from: grid-logic.js (all 378 lines), plus isRolly()
// and rollyFootprintCells() from rolly-enemy-logic.js (grid code
// depends on them; Bunch 4 owns the rest of rolly behavior and
// reuses these exact methods).
//
// Static class — the direct equivalent of the JS module's global
// functions. All reads/writes go through GameManager.S.
//
// Hooks assigned by later bunches (defaults = "feature absent",
// matching a fresh JS run where those systems haven't triggered):
//   AnyLineInSetDisabledHook / IsLineDisabledHook → Bunch 8
//     (row-col-disabling-logic.js)
// JS's toast()/log()/render()/updateBonusDisplay() call sites map
// to GameEvents.Toast / .Log / .RaiseStateChanged /
// .RaiseHudMetersChanged respectively.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

public static class GridLogic
{
    private static GameState S => GameManager.S;

    // ---------------- Bunch 8 hooks (row/col disabling) ----------------
    /// <summary>JS: anyLineInSetDisabled(axisType, set). axisType: 'r' or 'c'.</summary>
    public static Func<char, HashSet<int>, bool> AnyLineInSetDisabledHook = (axis, set) => false;
    /// <summary>JS: isLineDisabled('row'|'col', idx) — used by rendering.</summary>
    public static Func<string, int, bool> IsLineDisabledHook = (axis, idx) => false;

    // ============================================================
    // Transparency & occupancy
    // ============================================================

    /// <summary>JS: isTransparentEnemyType(type) — reads the constructor
    /// static; here, the EnemyDefinition flag.</summary>
    public static bool IsTransparentEnemyType(string type)
    {
        return Registry.Enemies.TryGetValue(type, out var def) && def.IsTransparent;
    }

    /// <summary>JS: isTransparentOccupant(ref).</summary>
    public static bool IsTransparentOccupant(OccupantRef occ)
    {
        if (occ.Kind == OccupantKind.SporeCloud) return true;
        return S.Enemies.TryGetValue(occ.Id, out var en) && IsTransparentEnemyType(en.Type);
    }

    /// <summary>JS: cellHasNonTransparentOccupant(r, c) — "blocked for
    /// normal-enemy purposes".</summary>
    public static bool CellHasNonTransparentOccupant(int r, int c)
    {
        foreach (var occ in S.Grid[r, c])
            if (!IsTransparentOccupant(occ)) return true;
        return false;
    }

    // ============================================================
    // Ref add/remove (the ONLY sanctioned grid-cell mutations)
    // ============================================================

    public static void RemoveEnemyRefAt(int r, int c, int id)
    {
        S.Grid[r, c].RemoveAll(occ => occ.Kind == OccupantKind.Enemy && occ.Id == id);
    }

    public static void AddEnemyRefAt(int r, int c, int id)
    {
        S.Grid[r, c].Add(OccupantRef.EnemyRef(id));
    }

    public static void RemoveSporeCloudRefAt(int r, int c, int id)
    {
        S.Grid[r, c].RemoveAll(occ => occ.Kind == OccupantKind.SporeCloud && occ.Id == id);
    }

    public static void AddSporeCloudRefAt(int r, int c, int id)
    {
        S.Grid[r, c].Add(OccupantRef.SporeRef(id));
    }

    /// <summary>JS: placeEnemyAt(r, c, enemy). Writes the full footprint
    /// (with wrap) and registers the enemy. Callers must have already
    /// checked CellHasNonTransparentOccupant for non-transparent types —
    /// this function does NOT gate on that (same as JS).</summary>
    public static void PlaceEnemyAt(int r, int c, Enemy enemy)
    {
        for (int dr = 0; dr < enemy.Size.x; dr++)
        {
            for (int dc = 0; dc < enemy.Size.y; dc++)
            {
                int rr = (r + dr) % S.Rows;
                int cc = (c + dc) % S.Cols;
                AddEnemyRefAt(rr, cc, enemy.Id);
            }
        }
        enemy.Anchor = new Vector2Int(r, c);
        S.Enemies[enemy.Id] = enemy;
    }

    // ============================================================
    // Rolly helpers (from rolly-enemy-logic.js — shared with Bunch 4)
    // ============================================================

    /// <summary>JS: isRolly(en).</summary>
    public static bool IsRolly(Enemy en) => en != null && en.Type == "rolly";

    /// <summary>JS: rollyFootprintCells(en) — verbatim translation incl.
    /// the double-modulo negative-safe wrap.</summary>
    public static List<Vector2Int> RollyFootprintCells(Enemy en)
    {
        int ar = en.Anchor.x, ac = en.Anchor.y;
        if (en.StretchAxis == null) return new List<Vector2Int> { new Vector2Int(ar, ac) };
        var cells = new List<Vector2Int>();
        if (en.StretchAxis == "row")
        {
            for (int dc = -en.StretchBefore; dc <= en.StretchAfter; dc++)
                cells.Add(new Vector2Int(ar, ((ac + dc) % S.Cols + S.Cols) % S.Cols));
        }
        else
        {
            for (int dr = -en.StretchBefore; dr <= en.StretchAfter; dr++)
                cells.Add(new Vector2Int(((ar + dr) % S.Rows + S.Rows) % S.Rows, ac));
        }
        return cells;
    }

    // ============================================================
    // Footprint geometry
    // ============================================================

    /// <summary>JS: enemyWraps(en).</summary>
    public static bool EnemyWraps(Enemy en)
    {
        if (IsRolly(en) && en.StretchAxis != null)
        {
            if (en.StretchAxis == "row")
                return en.Anchor.y + en.StretchAfter >= S.Cols || en.Anchor.y - en.StretchBefore < 0;
            return en.Anchor.x + en.StretchAfter >= S.Rows || en.Anchor.x - en.StretchBefore < 0;
        }
        if (en.Size.x <= 1 && en.Size.y <= 1) return false;
        return (en.Anchor.x + en.Size.x > S.Rows) || (en.Anchor.y + en.Size.y > S.Cols);
    }

    /// <summary>JS: nonSquareFootprintSpan(en) — null unless a stretched rolly.</summary>
    public static RollySpan NonSquareFootprintSpan(Enemy en)
    {
        if (IsRolly(en) && en.StretchAxis != null)
        {
            return new RollySpan
            {
                Axis = en.StretchAxis,
                Before = en.StretchBefore,
                After = en.StretchAfter,
                LineLen = en.StretchBefore + en.StretchAfter + 1
            };
        }
        return null;
    }

    public class RollySpan
    {
        public string Axis;
        public int Before;
        public int After;
        public int LineLen;
    }

    /// <summary>JS: findAxisStart(indices, gridSize, size) — finds the start
    /// index of a contiguous (mod gridSize) run of `size` cells.</summary>
    public static int FindAxisStart(IEnumerable<int> indices, int gridSize, int size)
    {
        var sorted = new List<int>(indices);
        sorted.Sort();
        if (size <= 1 || sorted.Count <= 1) return sorted[0];
        var targetSet = new HashSet<int>(sorted);
        for (int i = 0; i < sorted.Count; i++)
        {
            int candidate = sorted[i];
            bool matches = true;
            for (int k = 0; k < size; k++)
            {
                if (!targetSet.Contains((candidate + k) % gridSize)) { matches = false; break; }
            }
            if (matches) return candidate;
        }
        return sorted[0];
    }

    /// <summary>JS: recomputeAnchors() — rebuilds every enemy's anchor by
    /// scanning cell ref arrays. Includes the stretched-rolly repair branch:
    /// if a shift broke its contiguous line, it collapses back to 1x1.</summary>
    public static void RecomputeAnchors()
    {
        var seen = new Dictionary<int, List<Vector2Int>>();
        for (int r = 0; r < S.Rows; r++)
        {
            for (int c = 0; c < S.Cols; c++)
            {
                foreach (var occ in S.Grid[r, c])
                {
                    if (occ.Kind != OccupantKind.Enemy) continue;
                    if (!seen.TryGetValue(occ.Id, out var list))
                        seen[occ.Id] = list = new List<Vector2Int>();
                    list.Add(new Vector2Int(r, c));
                }
            }
        }

        foreach (var kv in seen)
        {
            int id = kv.Key;
            if (!S.Enemies.TryGetValue(id, out var en)) continue;
            var cells = kv.Value;

            if (IsRolly(en) && en.StretchAxis != null)
            {
                string axis = en.StretchAxis;
                var fixedIdx = new HashSet<int>();
                var varyingIdxs = new List<int>();
                foreach (var p in cells)
                {
                    fixedIdx.Add(axis == "row" ? p.x : p.y);
                    varyingIdxs.Add(axis == "row" ? p.y : p.x);
                }
                int expectedLen = en.StretchBefore + en.StretchAfter + 1;
                bool formsContiguousLine = fixedIdx.Count == 1
                    && cells.Count == expectedLen
                    && new HashSet<int>(varyingIdxs).Count == expectedLen;

                if (!formsContiguousLine)
                {
                    foreach (var p in cells) RemoveEnemyRefAt(p.x, p.y, id);
                    en.StretchAxis = null; en.StretchBefore = 0; en.StretchAfter = 0;
                    en.Anchor = cells[0];
                    AddEnemyRefAt(cells[0].x, cells[0].y, id);
                }
                else
                {
                    int gridSize = axis == "row" ? S.Cols : S.Rows;
                    int startVarying = FindAxisStart(varyingIdxs, gridSize, expectedLen);
                    int fixedOne = FirstOf(fixedIdx);
                    en.Anchor = axis == "row"
                        ? new Vector2Int(fixedOne, (startVarying + en.StretchBefore) % gridSize)
                        : new Vector2Int((startVarying + en.StretchBefore) % gridSize, fixedOne);
                }
                continue;
            }

            if (en.Size.x == 1 && en.Size.y == 1) { en.Anchor = cells[0]; continue; }

            var rowIndices = new HashSet<int>();
            var colIndices = new HashSet<int>();
            foreach (var p in cells) { rowIndices.Add(p.x); colIndices.Add(p.y); }
            en.Anchor = new Vector2Int(
                FindAxisStart(rowIndices, S.Rows, en.Size.x),
                FindAxisStart(colIndices, S.Cols, en.Size.y));
        }
    }

    private static int FirstOf(HashSet<int> set)
    {
        foreach (int v in set) return v;
        return 0;
    }

    // ============================================================
    // Linked lines (multi-cell enemies drag neighboring rows/cols)
    // ============================================================

    /// <summary>JS: linkedLinesForAxis(axisType, seedIdx). axisType: 'r'/'c'.
    /// Returns the extra linked line indices, EXCLUDING the seed (JS
    /// deletes it before returning).</summary>
    public static HashSet<int> LinkedLinesForAxis(char axisType, int seedIdx)
    {
        var result = new HashSet<int> { seedIdx };
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var en in S.Enemies.Values)
            {
                List<int> fullRange = null;

                if (en.Size.x >= 2 || en.Size.y >= 2)
                {
                    int ar = en.Anchor.x, ac = en.Anchor.y;
                    int axisSize = axisType == 'r' ? en.Size.x : en.Size.y;
                    fullRange = new List<int>();
                    for (int i = 0; i < axisSize; i++)
                        fullRange.Add(axisType == 'r' ? (ar + i) % S.Rows : (ac + i) % S.Cols);
                }
                else if (IsRolly(en) && en.StretchAxis != null)
                {
                    var cells = RollyFootprintCells(en);
                    bool spansThisAxis = en.StretchAxis == "row" ? (axisType == 'c') : (axisType == 'r');
                    if (spansThisAxis)
                    {
                        var idxSet = new HashSet<int>();
                        foreach (var p in cells) idxSet.Add(axisType == 'r' ? p.x : p.y);
                        if (idxSet.Count > 1) fullRange = new List<int>(idxSet);
                    }
                }

                if (fullRange == null) continue;

                bool touches = false;
                foreach (int f in fullRange)
                    if (result.Contains(f)) { touches = true; break; }
                if (!touches) continue;

                foreach (int f in fullRange)
                    if (result.Add(f)) changed = true;
            }
        }
        result.Remove(seedIdx);
        return result;
    }

    // ============================================================
    // Shift history
    // ============================================================

    /// <summary>JS: snapshotGrid() — new per-cell lists; OccupantRef structs
    /// copy by value, so this is exactly as deep as the JS copy.</summary>
    public static List<OccupantRef>[,] SnapshotGrid()
    {
        var copy = new List<OccupantRef>[S.Rows, S.Cols];
        for (int r = 0; r < S.Rows; r++)
            for (int c = 0; c < S.Cols; c++)
                copy[r, c] = new List<OccupantRef>(S.Grid[r, c]);
        return copy;
    }

    /// <summary>JS: pushShiftHistory(linesIdx, axisType). Only pushes when at
    /// least one line in the group is NEW this turn (free re-shifts of
    /// already-touched lines don't consume history/reverts).</summary>
    public static void PushShiftHistory(HashSet<int> linesIdx, char axisType)
    {
        var touchedSet = axisType == 'r' ? S.RowsTouchedThisTurn : S.ColsTouchedThisTurn;
        bool anyNew = false;
        foreach (int i in linesIdx)
            if (!touchedSet.Contains(i)) anyNew = true;
        if (!anyNew) return;

        var anchors = new Dictionary<int, Vector2Int>();
        foreach (var kv in S.Enemies) anchors[kv.Key] = kv.Value.Anchor;

        S.ShiftHistory.Add(new ShiftSnapshot
        {
            Grid = SnapshotGrid(),
            LinesShiftedThisTurn = new HashSet<int>(S.LinesShiftedThisTurn),
            RowsTouched = new HashSet<int>(S.RowsTouchedThisTurn),
            ColsTouched = new HashSet<int>(S.ColsTouchedThisTurn),
            DecayStepCounter = S.DecayStepCounter,
            EnemyAnchors = anchors,
            TouchedIndices = new HashSet<int>(linesIdx)
        });
    }

    // ============================================================
    // Shifts & revert (the player's core verbs)
    // ============================================================

    /// <summary>JS: shiftRow(r, dir). dir == 1 shifts right, -1 shifts left.</summary>
    public static void ShiftRow(int r, int dir)
    {
        if (S.EnemyPhaseActive) { GameEvents.Toast("It's the enemies' turn — wait for your turn!"); return; }
        if (!DebugFlags.InfiniteAttacks && S.AttacksRemainingThisTurn <= 0) { GameEvents.Toast("No attacks left — grid is locked for this turn!"); return; }

        var rowsToShift = new HashSet<int> { r };
        rowsToShift.UnionWith(LinkedLinesForAxis('r', r));
        if (AnyLineInSetDisabledHook('r', rowsToShift)) { GameEvents.Toast("A disabled row is part of this shift — it cannot move!"); return; }

        PushShiftHistory(rowsToShift, 'r');

        foreach (int rr in rowsToShift)
        {
            var old = new List<OccupantRef>[S.Cols];
            for (int c = 0; c < S.Cols; c++) old[c] = S.Grid[rr, c];
            // JS dir===1: [last, ...rest]  → new[c] = old[c-1 (wrapped)]
            // JS dir===-1: [...rest, first] → new[c] = old[c+1 (wrapped)]
            for (int c = 0; c < S.Cols; c++)
                S.Grid[rr, c] = dir == 1 ? old[(c - 1 + S.Cols) % S.Cols] : old[(c + 1) % S.Cols];
        }

        RecomputeAnchors();

        bool anyNewRow = false;
        foreach (int rr in rowsToShift)
            if (!S.RowsTouchedThisTurn.Contains(rr)) anyNewRow = true;
        if (anyNewRow) S.LinesShiftedThisTurn.Add(S.DecayStepCounter++);
        foreach (int rr in rowsToShift) S.RowsTouchedThisTurn.Add(rr);

        GameEvents.RaiseHudMetersChanged();   // JS: updateBonusDisplay()
        GameEvents.RaiseStateChanged();       // JS: render()
    }

    /// <summary>JS: shiftCol(c, dir). dir == 1 shifts down, -1 shifts up.</summary>
    public static void ShiftCol(int c, int dir)
    {
        if (S.EnemyPhaseActive) { GameEvents.Toast("It's the enemies' turn — wait for your turn!"); return; }
        if (!DebugFlags.InfiniteAttacks && S.AttacksRemainingThisTurn <= 0) { GameEvents.Toast("No attacks left — grid is locked for this turn!"); return; }

        var colsToShift = new HashSet<int> { c };
        colsToShift.UnionWith(LinkedLinesForAxis('c', c));
        if (AnyLineInSetDisabledHook('c', colsToShift)) { GameEvents.Toast("A disabled column is part of this shift — it cannot move!"); return; }

        PushShiftHistory(colsToShift, 'c');

        foreach (int cc in colsToShift)
        {
            var old = new List<OccupantRef>[S.Rows];
            for (int r = 0; r < S.Rows; r++) old[r] = S.Grid[r, cc];
            for (int r = 0; r < S.Rows; r++)
                S.Grid[r, cc] = dir == 1 ? old[(r - 1 + S.Rows) % S.Rows] : old[(r + 1) % S.Rows];
        }

        RecomputeAnchors();

        bool anyNewCol = false;
        foreach (int cc in colsToShift)
            if (!S.ColsTouchedThisTurn.Contains(cc)) anyNewCol = true;
        if (anyNewCol) S.LinesShiftedThisTurn.Add(S.DecayStepCounter++);
        foreach (int cc in colsToShift) S.ColsTouchedThisTurn.Add(cc);

        GameEvents.RaiseHudMetersChanged();
        GameEvents.RaiseStateChanged();
    }

    /// <summary>JS: revertLastShift().</summary>
    public static void RevertLastShift()
    {
        if (!DebugFlags.InfiniteReverts && S.RevertsLeft <= 0) { GameEvents.Toast("No reverts left this turn!"); return; }
        if (S.ShiftHistory.Count == 0) { GameEvents.Toast("Nothing to revert!"); return; }

        var last = S.ShiftHistory[S.ShiftHistory.Count - 1];
        S.ShiftHistory.RemoveAt(S.ShiftHistory.Count - 1);

        S.Grid = last.Grid;
        S.LinesShiftedThisTurn = last.LinesShiftedThisTurn;
        S.RowsTouchedThisTurn = last.RowsTouched;
        S.ColsTouchedThisTurn = last.ColsTouched;
        S.DecayStepCounter = last.DecayStepCounter;
        foreach (var pair in last.EnemyAnchors)
        {
            if (S.Enemies.TryGetValue(pair.Key, out var en)) en.Anchor = pair.Value;
        }

        if (!DebugFlags.InfiniteReverts) S.RevertsLeft--;
        GameEvents.Toast("Reverted " + (last.TouchedIndices.Count > 1 ? "linked shift" : "shift")
            + "! (" + (DebugFlags.InfiniteReverts ? "∞" : S.RevertsLeft.ToString()) + " left)");
        GameEvents.RaiseHudMetersChanged();
        GameEvents.RaiseStateChanged();
    }

    // ============================================================
    // Misc queries
    // ============================================================

    /// <summary>JS: gridIsCompletelyOccupied() — every cell has ≥1 occupant.</summary>
    public static bool GridIsCompletelyOccupied()
    {
        for (int r = 0; r < S.Rows; r++)
            for (int c = 0; c < S.Cols; c++)
                if (S.Grid[r, c].Count == 0) return false;
        return true;
    }

    /// <summary>JS: getCycledOccupant(r, c) — shared click-cycling used by
    /// attack targeting, item targeting AND debug delete mode. Repeated taps
    /// on the same cell cycle through its stacked occupants.</summary>
    public static OccupantRef? GetCycledOccupant(int r, int c)
    {
        var refs = S.Grid[r, c];
        if (refs.Count == 0)
        {
            S.LastClickedCell = new Vector2Int(r, c);
            S.ClickCycleIndex = 0;
            return null;
        }
        bool sameCell = S.LastClickedCell.HasValue
            && S.LastClickedCell.Value.x == r && S.LastClickedCell.Value.y == c;
        if (!sameCell)
        {
            S.LastClickedCell = new Vector2Int(r, c);
            S.ClickCycleIndex = 0;
        }
        else
        {
            S.ClickCycleIndex = (S.ClickCycleIndex + 1) % refs.Count;
        }
        // JS quirk kept intentionally: if the stack shrank since the last
        // click, index modulo count still lands on a valid ref.
        if (S.ClickCycleIndex >= refs.Count) S.ClickCycleIndex = 0;
        return refs[S.ClickCycleIndex];
    }
}
