// ============================================================
// ACTION BUTTONS VIEW  (NEW in Bunch 2)
// Translated from: the .side-actions block of buildGridDOM()
// (grid-rendering.js lines 330–367): Confirm / Revert / Skip Turn /
// Restart, including their per-render disabled conditions.
//
// Hook owners:
//   ConfirmReadyHook + ConfirmPressedHook → Bunch 6
//     (confirmButtonReady / onConfirmPressed, confirm-button-logic.js)
//   SkipTurnHook                          → Bunch 9 (skipTurnConfirmed)
//   The showConfirm() modal wrapping Skip/Restart → Bunch 10; until
//   then those actions run immediately (noted below).
// ============================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActionButtonsView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button revertBtn;
    [SerializeField] private Button skipTurnBtn;
    [SerializeField] private Button restartBtn;
    [SerializeField] private TextMeshProUGUI revertLabel;

    public static Func<bool> ConfirmReadyHook = () => false;   // Bunch 6
    public static Action ConfirmPressedHook = null;            // Bunch 6
    public static Action SkipTurnHook = null;                  // Bunch 9
    /// <summary>Bunch 10 assigns this to route through the confirm modal
    /// (JS: showConfirm(title, msg, onConfirm)). Null = act immediately.</summary>
    public static Action<string, string, Action> ShowConfirmHook = null;

    private void OnEnable()
    {
        GameEvents.StateChanged += Refresh;
        confirmBtn.onClick.AddListener(OnConfirm);
        revertBtn.onClick.AddListener(GridLogic.RevertLastShift);
        skipTurnBtn.onClick.AddListener(OnSkip);
        restartBtn.onClick.AddListener(OnRestart);
        Refresh();
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= Refresh;
        confirmBtn.onClick.RemoveListener(OnConfirm);
        revertBtn.onClick.RemoveListener(GridLogic.RevertLastShift);
        skipTurnBtn.onClick.RemoveListener(OnSkip);
        restartBtn.onClick.RemoveListener(OnRestart);
    }

    private void OnConfirm()
    {
        // JS: onConfirmPressed() — full dispatch arrives in Bunch 6.
        if (ConfirmPressedHook != null) { ConfirmPressedHook(); return; }
        GameEvents.Toast("Select an attack or item first!");
    }

    private void OnSkip()
    {
        Action doSkip = () =>
        {
            if (SkipTurnHook != null) SkipTurnHook();
            else GameEvents.Toast("Turn cycle arrives in Bunch 9.");
        };
        if (ShowConfirmHook != null)
            ShowConfirmHook("⏭️ Skip Turn?",
                "This will end your turn immediately without attacking, using an item, or finishing your slides. Enemies will act normally. Continue?",
                doSkip);
        else doSkip();
    }

    private void OnRestart()
    {
        Action doRestart = () => GameManager.Instance.RestartRun();
        if (ShowConfirmHook != null)
            ShowConfirmHook("🔄 Restart Game?",
                "This will erase your current run — score, floor progress, charges, and items — and start a brand new game from Floor 1. This cannot be undone. Continue?",
                doRestart);
        else doRestart();   // Bunch 10 adds the modal guard
    }

    /// <summary>JS: the three .disabled assignments at buildGridDOM 365–367
    /// + the revert count baked into the button label at line 334.</summary>
    private void Refresh()
    {
        var s = GameManager.S;
        if (s == null) return;
        bool locked = DebugFlags.OtherDebugInteractionsLocked();

        confirmBtn.interactable = ConfirmReadyHook() && !s.EnemyPhaseActive && !locked;

        bool revertDisabled =
            (!DebugFlags.InfiniteReverts && (s.RevertsLeft <= 0 || s.ShiftHistory.Count == 0))
            || (DebugFlags.InfiniteReverts && s.ShiftHistory.Count == 0)
            || s.EnemyPhaseActive || locked;
        revertBtn.interactable = !revertDisabled;
        if (revertLabel != null)
            revertLabel.text = "↩️ Revert (" + (DebugFlags.InfiniteReverts ? "∞" : s.RevertsLeft.ToString()) + ")";

        skipTurnBtn.interactable = !s.EnemyPhaseActive && !locked;
    }
}
