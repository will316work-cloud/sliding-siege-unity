// ============================================================
// GAME STATE  (*** PATCHED in Bunch 2 — replaces the Bunch 1 file ***)
//
// CHANGE LOG vs Bunch 1:
//   - Grid cells are List<OccupantRef> (the JS { kind, id } refs),
//     not List<int>. Ghost/phantom/spore-cloud stacking depends on
//     the kind tag, so bare ids were wrong.
//   - EnemiesAtCell() now filters enemy-kind refs (JS: enemiesAtCell
//     in grid-logic.js) and SporeCloudsAtCell() was added (JS:
//     sporeCloudsAtCellRef).
//
// Everything else is identical to Bunch 1 — see that header for
// the full translation notes (incl. the AttackBaseDmg rationale).
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public class GameState
{
    // ---------------- Reverts / shift history ----------------
    public int MaxRevertsPerTurn = 3;
    public int RevertsLeft = 3;
    public List<ShiftSnapshot> ShiftHistory = new List<ShiftSnapshot>();

    public bool EnemyPhaseActive = false;

    // ---------------- Siren curses ----------------
    public Dictionary<string, List<int>> SirenCursedAttacks = new Dictionary<string, List<int>>();
    public Dictionary<string, List<int>> SirenCursedItems = new Dictionary<string, List<int>>();

    // ---------------- Spore clouds ----------------
    public Dictionary<int, SporeCloud> SporeClouds = new Dictionary<int, SporeCloud>();
    public int NextSporeCloudId = 1;
    public Dictionary<string, List<int>> SporeDisabledAttacks = new Dictionary<string, List<int>>();

    // ---------------- Row/col disabling ----------------
    public List<DisabledLine> DisabledLines = new List<DisabledLine>();

    // ---------------- Click cycling (grid-logic.js) ----------------
    public Vector2Int? LastClickedCell = null;
    public int ClickCycleIndex = 0;

    // ---------------- Score / floor / damage bonus ----------------
    public int Score = 0;
    public int Floor = 1;
    public int BonusMax = 100;
    public int BonusStep = 20;

    // ---------------- Per-turn shift decay tracking ----------------
    public HashSet<int> LinesShiftedThisTurn = new HashSet<int>();
    public HashSet<int> RowsTouchedThisTurn = new HashSet<int>();
    public HashSet<int> ColsTouchedThisTurn = new HashSet<int>();
    public int DecayStepCounter = 0;

    // ---------------- Charges & items ----------------
    public Dictionary<string, int> Charges = new Dictionary<string, int>();
    public Dictionary<string, int> Items = new Dictionary<string, int>();
    public Dictionary<string, int> AttackBaseDmg = new Dictionary<string, int>();

    // ---------------- Turn / action flags ----------------
    public string SelectedAttack = null;
    public bool SelectedItemUsedThisTurn = false;
    public int AttacksRemainingThisTurn = 1;
    public bool AttackUsedThisTurn = false;

    // ---------------- Combo ----------------
    public string ComboLastKilledType = null;
    public int ComboCount = 0;

    // ---------------- Charge-bar meta progression ----------------
    public int ChargePointsProgress = 0;
    public int ChargePointThreshold = 100;

    // ---------------- Misc run flags ----------------
    public bool FreezeEnemiesNextTurn = false;
    public bool GameOver = false;
    public string GameOverReason = null;

    // ---------------- Board ----------------
    public int Rows = 5;
    public int Cols = 5;

    /// <summary>Grid[r, c] = list of occupant refs ({kind, id}), matching
    /// grid-logic.js exactly. Empty cell = empty list, never null.</summary>
    public List<OccupantRef>[,] Grid;

    public Dictionary<int, Enemy> Enemies = new Dictionary<int, Enemy>();
    public int NextId = 1;

    public GameState()
    {
        BuildEmptyGrid();
    }

    /// <summary>JS: emptyGrid(rows, cols) in grid-logic.js.</summary>
    public void BuildEmptyGrid()
    {
        Grid = new List<OccupantRef>[Rows, Cols];
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                Grid[r, c] = new List<OccupantRef>();
    }

    public bool InBounds(int r, int c) => r >= 0 && r < Rows && c >= 0 && c < Cols;

    /// <summary>JS: enemiesAtCell(r, c) — enemy-kind refs resolved to live
    /// enemies (dead/missing ids and spore refs filtered out).</summary>
    public List<Enemy> EnemiesAtCell(int r, int c)
    {
        var result = new List<Enemy>();
        if (!InBounds(r, c)) return result;
        foreach (var occ in Grid[r, c])
            if (occ.Kind == OccupantKind.Enemy && Enemies.TryGetValue(occ.Id, out var en))
                result.Add(en);
        return result;
    }

    /// <summary>JS: sporeCloudsAtCellRef(r, c).</summary>
    public List<SporeCloud> SporeCloudsAtCell(int r, int c)
    {
        var result = new List<SporeCloud>();
        if (!InBounds(r, c)) return result;
        foreach (var occ in Grid[r, c])
            if (occ.Kind == OccupantKind.SporeCloud && SporeClouds.TryGetValue(occ.Id, out var sc))
                result.Add(sc);
        return result;
    }
}
