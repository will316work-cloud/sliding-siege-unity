// ============================================================
// SESSION (targeting / selection / pending-visual state)
// Translated from: the loose module-level vars in global-state.js
// that deliberately did NOT live on `state` (they reset freely and
// are never snapshotted by shift history).
//
// Static class = the direct C# equivalent of those globals.
// Session.Clear() is called by GameManager.NewRun().
// ============================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>JS: previewTarget / pendingTarget object shape
/// { attack, r, c, variant }.</summary>
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
    /// <summary>Attack key currently in targeting mode, or null. JS: targetingMode.</summary>
    public static string TargetingMode = null;
    public static AttackTarget PreviewTarget = null;
    /// <summary>Set when Confirm is pressed; consumed by QTE resolution. JS: pendingTarget.</summary>
    public static AttackTarget PendingTarget = null;

    // ---------------- Item targeting ----------------
    public static string SelectedItem = null;                 // JS: selectedItem
    public static string ItemTargetingMode = null;            // JS: itemTargetingMode
    public static Vector2Int? ItemPreviewCell = null;         // JS: itemPreviewCell
    public static Vector2Int? ItemSecondTarget = null;        // JS: itemSecondTarget (teleport destination)
    public static List<Vector2Int> ItemSecondTargetFootprint = new List<Vector2Int>();

    // ---------------- Pending visual queues ----------------
    // Written by logic during resolution, flushed by the view layer on the
    // next refresh (JS: flushDamageTexts()/flushComboText() in grid-rendering.js).
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

public class PendingDamageText
{
    public int R;
    public int C;
    public string Text;
    public Color Color = Color.white;
}

public class PendingComboText
{
    public int R;
    public int C;
    public string Text;
}
