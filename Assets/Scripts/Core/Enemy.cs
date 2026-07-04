// ============================================================
// ENEMY (runtime instance)
// (*** PATCHED in Bunch 2 — replaces the Bunch 1 file ***)
//
// CHANGE LOG vs Bunch 1:
//   - Runtime footprint is `Size` (JS: en.size — what placeEnemyAt,
//     recomputeAnchors, enemyWraps and linkedLinesForAxis all read).
//     The definition's BaseSize seeds it at spawn; `BaseSize` was
//     removed from this class (it lives on EnemyDefinition).
//   - Added the rolly stretch fields (en.stretchAxis / stretchBefore /
//     stretchAfter) and PendingSpawn / PendingDetonation flags —
//     grid logic and rendering read all of these.
//
// Still *** PARTIAL ***: Bunch 3 adds status-effect counters and
// the remaining per-type fields (songCounter, disabledAttackKey,
// queuedShape, ...).
// ============================================================

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Enemy
{
    public int Id;
    public string Type;      // registry key, e.g. "slime"
    public string Label;

    public int Hp;
    public int MaxHp;

    /// <summary>Anchor cell. Convention: Vector2Int(x = row, y = col).</summary>
    public Vector2Int Anchor;

    /// <summary>Runtime footprint (JS: en.size). x = rows, y = cols.</summary>
    public Vector2Int Size = new Vector2Int(1, 1);

    /// <summary>Per-type variant key (e.g. bomb blast pattern). JS: en.variant</summary>
    public string Variant;

    public List<int> LinkedIds = new List<int>();

    // ---------------- Rolly stretch state (rolly-enemy-logic.js) ----------------
    /// <summary>null = not stretched; "row" = stretched along its row
    /// (varying column); "col" = stretched along its column. Kept as the
    /// exact JS strings so translated comparisons read identically.</summary>
    public string StretchAxis = null;
    public int StretchBefore = 0;
    public int StretchAfter = 0;

    // ---------------- Flags read by grid rendering ----------------
    /// <summary>JS: en.pendingSpawn — mid-spawn-animation, skip normal draw.</summary>
    public bool PendingSpawn = false;
    /// <summary>JS: en.pendingDetonation — golem critical state.</summary>
    public bool PendingDetonation = false;

    // -------- Expanded in Bunch 3 / Bunch 8: status counters etc. --------
}
