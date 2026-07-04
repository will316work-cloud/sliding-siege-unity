// ============================================================
// CELL CLICK ROUTER  (NEW in Bunch 2)
// Translated from: the cell.onclick dispatch chain in
// grid-rendering.js (lines 294–301):
//
//   if (debugDeleteModeActive) { debugDeleteModeOnCellClick(r,c); return; }
//   if (debugMoveModeActive)   { debugMoveModeOnCellClick(r,c);   return; }
//   if (otherDebugInteractionsLocked()) return;
//   if (debugSelectedType)     { debugOnCellClick(r,c);           return; }
//   if (itemTargetingMode)     { itemOnCellClick(r,c);            return; }
//   onCellClick(r,c);   // always runs last — handles the tooltip branch too
//
// Hook owners: Bunch 12 (debug modes), Bunch 7 (ItemCellClick),
// Bunch 6 (AttackCellClick = cell-click-logic.js onCellClick).
// Defaults reproduce a run where those systems are inert.
// ============================================================

using System;
using UnityEngine;

public static class CellClickRouter
{
    // ---- Bunch 12 hooks ----
    public static Func<bool> DebugDeleteModeActive = () => false;
    public static Action<int, int> DebugDeleteModeOnCellClick = null;
    public static Func<bool> DebugMoveModeActive = () => false;
    public static Action<int, int> DebugMoveModeOnCellClick = null;
    public static Func<bool> DebugSpawnTypeSelected = () => false;
    public static Action<int, int> DebugOnCellClick = null;

    // ---- Bunch 7 hook ----
    public static Action<int, int> ItemOnCellClick = null;

    // ---- Bunch 6 hook (cell-click-logic.js onCellClick) ----
    public static Action<int, int> OnCellClick = null;

    public static void Route(int r, int c)
    {
        if (DebugDeleteModeActive()) { DebugDeleteModeOnCellClick?.Invoke(r, c); return; }
        if (DebugMoveModeActive()) { DebugMoveModeOnCellClick?.Invoke(r, c); return; }
        if (DebugFlags.OtherDebugInteractionsLocked()) return;
        if (DebugSpawnTypeSelected()) { DebugOnCellClick?.Invoke(r, c); return; }
        if (Session.ItemTargetingMode != null) { ItemOnCellClick?.Invoke(r, c); return; }

        if (OnCellClick != null) { OnCellClick(r, c); return; }

        // Pre-Bunch-6 fallback so the grid is inspectable right now:
        // exercise the shared click-cycling and log what was hit.
        var occ = GridLogic.GetCycledOccupant(r, c);
        Debug.Log($"[Cell {r},{c}] " + (occ.HasValue ? $"cycled to {occ.Value}" : "empty"));
        GameEvents.RaiseStateChanged();
    }
}
