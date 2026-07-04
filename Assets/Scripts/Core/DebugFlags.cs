// ============================================================
// DEBUG FLAGS  (NEW in Bunch 2)
// Translated from: the loose debug globals referenced throughout
// grid-logic.js / grid-rendering.js (debugInfiniteAttacks,
// debugInfiniteReverts, debugInfiniteItems) plus the
// otherDebugInteractionsLocked() gate.
//
// Bunch 12 (Debug Panel) provides the UI that toggles these and
// assigns OtherDebugInteractionsLocked. Until then everything
// defaults to "off", which makes every guard behave exactly like
// the JS with the debug panel untouched.
// ============================================================

using System;

public static class DebugFlags
{
    public static bool InfiniteAttacks = false;   // JS: debugInfiniteAttacks
    public static bool InfiniteReverts = false;   // JS: debugInfiniteReverts
    public static bool InfiniteItems = false;     // JS: debugInfiniteItems

    /// <summary>JS: otherDebugInteractionsLocked() — true while a debug
    /// mode (move/delete/spawn) owns grid clicks. Bunch 12 assigns.</summary>
    public static Func<bool> OtherDebugInteractionsLocked = () => false;
}
