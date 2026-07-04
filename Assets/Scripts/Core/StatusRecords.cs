// ============================================================
// STATUS RECORDS (stubs)
// *** PARTIAL — Bunch 1 stubs so GameState compiles. ***
// Bunch 8 (Status Effects) fills these in from
// spore-cloud-logic.js and row-col-disabling-logic.js.
// ============================================================

using UnityEngine;

/// <summary>JS: entries of state.sporeClouds (keyed by cloud id).</summary>
public class SporeCloud
{
    public int Id;
    public Vector2Int Cell;          // grid cell the cloud sits on (x = row, y = col)
    // Bunch 8: source mushy id, disabled attack key, remaining turns, etc.
}

/// <summary>JS: entries of state.disabledLines (phantom/ghost curses etc.).</summary>
public class DisabledLine
{
    public LineAxis Axis;
    public int Index;                // row index or col index depending on Axis
    public int SourceEnemyId;        // killing the source re-enables the line
    // Bunch 8: duration/expiry fields as defined in row-col-disabling-logic.js.
}

/// <summary>'r' / 'c' axis strings from the JS, made type-safe.</summary>
public enum LineAxis { Row, Col }
