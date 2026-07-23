using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SlidingSiege
{
    [Serializable] public class ShiftResultEvent : UnityEvent<ShiftResult> { }
    [Serializable] public class ShiftedCountEvent : UnityEvent<int> { }
    [Serializable] public class ShiftTrackerEvent : UnityEvent<ShiftTracker> { }

    /// Tracks every shift performed since the last reset: a full undo
    /// history (oldest-first list, walk back one line at a time) plus a
    /// deduped count of distinct (axis, index) lines shifted. The count is
    /// fully recomputed from whatever's left in the history after each
    /// undo, so undoing a repeat shift of an already-tracked line correctly
    /// leaves it counted if an earlier, still-undone entry also covers it.
    ///
    /// This only does bookkeeping — it does NOT touch GridState. Callers
    /// still call GridState.UnshiftResult(popped) themselves to actually
    /// reverse the board and animate it (see SlidingGridController), since
    /// that's a strictly-sequenced, awaited operation UnityEvents can't
    /// express (they fire every listener synchronously, with no notion of
    /// "wait for this animation to finish"). Everything else — reactive
    /// broadcasts other scripts can slot a method into instead of holding
    /// a direct reference — is a UnityEvent.
    public class ShiftTracker : MonoBehaviour
    {
        [Header("Events")]
        [Tooltip("Raised once (Awake) with this ShiftTracker so other scripts can pick it up without a direct SerializeField reference — slot a receiver method (e.g. SlidingGridController.ReceiveShiftTracker) here.")]
        public ShiftTrackerEvent OnReady = new ShiftTrackerEvent();
        [Tooltip("Raised with the new distinct-line count whenever it changes (shift, undo, or reset). Slot a consumer's int-parameter method here (e.g. DamageBonusSystem.HandleShiftedCountChanged).")]
        public ShiftedCountEvent OnShiftedCountChanged = new ShiftedCountEvent();
        [Tooltip("Raised with each shift right after it's recorded.")]
        public ShiftResultEvent OnShiftRegistered = new ShiftResultEvent();
        [Tooltip("Raised once at the START of an undo press, before anything is reversed.")]
        public UnityEvent OnUndoStarted = new UnityEvent();
        [Tooltip("Raised once at the END of an undo press, after every matched entry has been popped. The popped entries themselves are PopLastLine's return value, not this event's payload — applying them requires strict sequential animation the caller must still drive itself.")]
        public UnityEvent OnUndoFinished = new UnityEvent();
        [Tooltip("Raised when tracking is fully cleared (ResetTracking).")]
        public UnityEvent OnReset = new UnityEvent();

        // Oldest-first; the back of the list is the most recent shift.
        private readonly List<ShiftResult> _history = new List<ShiftResult>();
        private readonly HashSet<(bool IsRow, int Index)> _shiftedLines = new HashSet<(bool, int)>();

        public int ShiftedCount => _shiftedLines.Count;
        public bool CanUndo => _history.Count > 0;

        private void Awake() => OnReady.Invoke(this);

        private void Start() => OnShiftedCountChanged.Invoke(ShiftedCount);

        /// Records a shift for undo and line-dedup tracking.
        public void RegisterShift(ShiftResult result)
        {
            _history.Add(result);
            OnShiftRegistered.Invoke(result);
            bool changed = false;
            foreach (var line in result.ShiftedLines)
                if (_shiftedLines.Add((result.IsRowShift, line))) changed = true;
            if (changed) OnShiftedCountChanged.Invoke(ShiftedCount);
        }

        /// Pops every history entry that shifted the SAME line(s) as the
        /// most recent shift — so shifting one row 3 times in a row is one
        /// undo press, no matter how many individual shifts it took.
        ///
        /// Walks back from the most recent entry, skipping OVER (leaving
        /// untouched) same-axis shifts of a DIFFERENT line — those are
        /// physically independent (RotateRow/RotateCol only touch their own
        /// line's cells) so it's safe to reach past them. The walk stops
        /// the instant it hits a shift on the OTHER axis: a column shift
        /// touches every row (and vice versa), so anything before that
        /// barrier can't be reversed in isolation without corrupting the
        /// board — it stays in history for a later undo press once that
        /// barrier itself has been undone.
        ///
        /// Returns the matched entries MOST-RECENT-FIRST — apply them to
        /// GridState.UnshiftResult in that exact order (last mutation
        /// undone first) — or empty if there's nothing to undo.
        public List<ShiftResult> PopLastLine()
        {
            var matched = new List<ShiftResult>();
            if (_history.Count == 0) return matched;

            OnUndoStarted.Invoke();

            var top = _history[_history.Count - 1];
            bool axis = top.IsRowShift;
            var targetLines = new HashSet<int>(top.ShiftedLines);

            for (int i = _history.Count - 1; i >= 0; i--)
            {
                var entry = _history[i];
                if (entry.IsRowShift != axis) break; // cross-axis barrier

                bool matches = false;
                foreach (var line in entry.ShiftedLines)
                    if (targetLines.Contains(line)) { matches = true; break; }
                if (!matches) continue; // independent line on the same axis — leave it, keep walking back

                matched.Add(entry);
                foreach (var line in entry.ShiftedLines) targetLines.Add(line);
                _history.RemoveAt(i);
            }

            if (matched.Count > 0) Recompute();
            OnUndoFinished.Invoke();
            return matched;
        }

        /// Clears all history and tracked lines. Never called
        /// automatically — callers decide what should reset it (e.g. slot
        /// this into DamageBonusSystem.OnResetRequested).
        public void ResetTracking()
        {
            if (_history.Count == 0 && _shiftedLines.Count == 0) return;
            _history.Clear();
            _shiftedLines.Clear();
            OnReset.Invoke();
            OnShiftedCountChanged.Invoke(0);
        }

        private void Recompute()
        {
            _shiftedLines.Clear();
            foreach (var entry in _history)
                foreach (var line in entry.ShiftedLines)
                    _shiftedLines.Add((entry.IsRowShift, line));
            OnShiftedCountChanged.Invoke(ShiftedCount);
        }
    }
}
