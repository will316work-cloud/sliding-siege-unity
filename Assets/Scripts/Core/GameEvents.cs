// ============================================================
// GAME EVENTS
// The Unity replacement for the JS "call render() after every
// mutation" pattern, plus toast()/log() from general-ui-logic.js.
//
// Logic classes call GameEvents.RaiseStateChanged() wherever the
// JS called render(). View components (grid view, HUD, attack
// list, ...) subscribe in OnEnable / unsubscribe in OnDisable and
// refresh themselves from GameManager.S. This keeps the JS's
// dead-simple "state is truth, UI redraws from it" mental model
// while letting Unity views pool instead of rebuild.
// ============================================================

using System;

public static class GameEvents
{
    /// <summary>Equivalent of render(). Fire after ANY state mutation the UI
    /// should reflect. Views must be cheap-idempotent on refresh.</summary>
    public static event Action StateChanged;

    /// <summary>JS: toast(msg) — transient floating message.</summary>
    public static event Action<string> ToastRequested;

    /// <summary>JS: log(msg) — prepend to the scrolling battle log.</summary>
    public static event Action<string> LogRequested;

    /// <summary>Fired by damage-bonus / charge-bar logic; the HUD listens.
    /// (JS: updateBonusDisplay() / updateChargeProgressDisplay() direct calls.)</summary>
    public static event Action HudMetersChanged;

    /// <summary>Fired when state.GameOver flips true. Payload = reason text.</summary>
    public static event Action<string> GameOverShown;

    public static void RaiseStateChanged() => StateChanged?.Invoke();
    public static void Toast(string msg) => ToastRequested?.Invoke(msg);
    public static void Log(string msg) => LogRequested?.Invoke(msg);
    public static void RaiseHudMetersChanged() => HudMetersChanged?.Invoke();
    public static void RaiseGameOver(string reason) => GameOverShown?.Invoke(reason);

    /// <summary>Call on run restart so stale subscribers from destroyed views
    /// can't linger if you ever hard-reload the scene additively.</summary>
    public static void ClearAll()
    {
        StateChanged = null;
        ToastRequested = null;
        LogRequested = null;
        HudMetersChanged = null;
        GameOverShown = null;
    }
}
