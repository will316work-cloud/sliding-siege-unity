// ============================================================
// GAME CONSTANTS
// Translated from: global-state.js (timing constants section)
//
// JS used milliseconds (450 / 350); Unity coroutines use seconds.
// ============================================================

public static class GameConstants
{
    /// <summary>JS: STEP_DELAY = 450 (ms). Delay between major enemy-phase steps.</summary>
    public const float StepDelay = 0.45f;

    /// <summary>JS: SUBSTEP_DELAY = 350 (ms). Delay between sub-steps inside one enemy's action.</summary>
    public const float SubstepDelay = 0.35f;

    /// <summary>Toast on-screen lifetime. JS: setTimeout(..., 1600) in toast().</summary>
    public const float ToastDuration = 1.6f;

    /// <summary>Max entries kept in the scrolling log. JS: while (el.children.length > 30).</summary>
    public const int MaxLogEntries = 30;
}
