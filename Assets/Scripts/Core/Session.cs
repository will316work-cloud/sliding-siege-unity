// ============================================================
// SESSION  (*** PATCHED in Bunch 3 — replaces the Bunch 1 file ***)
// CHANGE LOG vs Bunch 1: PendingDamageText now mirrors the exact
// JS queue tuple from queueDamageTextAt() in animation-rendering.js:
// [r, c, amount, isHeal, enemyId]. PendingComboText unchanged.
// See Bunch 1 header for the loose-globals rationale.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public class AttackTarget
{
    public string Attack;
    public int R;
    public int C;
    public string Variant;
}

public static class Session
{
    // ---------------- Attack targeting ----------------
    public static string TargetingMode = null;
    public static AttackTarget PreviewTarget = null;
    public static AttackTarget PendingTarget = null;

    // ---------------- Item targeting ----------------
    public static string SelectedItem = null;
    public static string ItemTargetingMode = null;
    public static Vector2Int? ItemPreviewCell = null;
    public static Vector2Int? ItemSecondTarget = null;
    public static List<Vector2Int> ItemSecondTargetFootprint = new List<Vector2Int>();

    // ---------------- Pending visual queues ----------------
    public static List<PendingDamageText> PendingDamageTexts = new List<PendingDamageText>();
    public static PendingComboText PendingCombo = null;

    public static void Clear()
    {
        TargetingMode = null;
        PreviewTarget = null;
        PendingTarget = null;
        SelectedItem = null;
        ItemTargetingMode = null;
        ItemPreviewCell = null;
        ItemSecondTarget = null;
        ItemSecondTargetFootprint.Clear();
        PendingDamageTexts.Clear();
        PendingCombo = null;
    }
}

/// <summary>JS: pendingDamageTexts entries [r, c, amount, isHeal, enemyId].</summary>
public class PendingDamageText
{
    public int R;
    public int C;
    public int Amount;
    public bool IsHeal;
    /// <summary>Null = anchor to the cell; otherwise anchor to the element
    /// carrying that enemy's name/HP text (JS: findEnemyTextEl).</summary>
    public int? EnemyId;
}

public class PendingComboText
{
    public int R;
    public int C;
    public string Text;
}
