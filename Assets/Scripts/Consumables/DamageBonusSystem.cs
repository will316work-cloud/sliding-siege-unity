using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SlidingSiege
{
    [System.Serializable]
    public class FillChangedEvent : UnityEvent<float> { }

    /// Tracks a damage bonus that depletes as the player shifts rows/columns
    /// and scales CombatSystem.DamageMultiplier between 1x and MaxMultiplier.
    /// Each DISTINCT (axis, index) line shifted since the last reset costs
    /// DepletionPerShift off the fill (0..1); re-shifting an already-tracked
    /// line costs nothing. Nothing in here resets the fill automatically —
    /// callers reset it explicitly (e.g. on a successful attack) via
    /// ResetBonus(). Wire OnFillChanged to any display's float-parameter
    /// method (fill image, slider, text, ...).
    public class DamageBonusSystem : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Damage multiplier at full bonus (fill = 1). Fill = 0 always multiplies by 1x.")]
        [SerializeField] private float maxDamageMultiplier = 2f;
        [Tooltip("Fraction of the fill (0..1) lost per distinct row/column shifted.")]
        [SerializeField, Range(0f, 1f)] private float depletionPerShift = 0.125f;

        [Header("Events")]
        [Tooltip("Raised with the new fill (0..1) whenever it changes, including on Initialize.")]
        public FillChangedEvent OnFillChanged = new FillChangedEvent();

        private readonly HashSet<(bool IsRow, int Index)> _shiftedLines = new HashSet<(bool, int)>();

        public float Fill { get; private set; } = 1f;
        public float CurrentMultiplier => Mathf.Lerp(1f, maxDamageMultiplier, Fill);

        private void Awake() => Recalculate(notify: false);

        private void Start() => OnFillChanged.Invoke(Fill);

        /// Marks every line in a shift result as shifted (dedup: lines
        /// already tracked since the last reset cost nothing further).
        public void RegisterShift(ShiftResult result)
        {
            bool changed = false;
            foreach (var line in result.ShiftedLines)
                if (_shiftedLines.Add((result.IsRowShift, line))) changed = true;
            if (changed) Recalculate(notify: true);
        }

        public void RegisterShift(bool isRow, int index)
        {
            if (_shiftedLines.Add((isRow, index))) Recalculate(notify: true);
        }

        /// Un-counts every line in a previously-registered shift (pair with
        /// GridState.UnshiftResult, which slides the enemies back) so
        /// reversing a shift also gives its bonus cost back.
        public void RegisterUnshift(ShiftResult result)
        {
            bool changed = false;
            foreach (var line in result.ShiftedLines)
                if (_shiftedLines.Remove((result.IsRowShift, line))) changed = true;
            if (changed) Recalculate(notify: true);
        }

        public void RegisterUnshift(bool isRow, int index)
        {
            if (_shiftedLines.Remove((isRow, index))) Recalculate(notify: true);
        }

        /// Clears every tracked line, refilling the bonus to 1.0. Never
        /// called automatically — callers decide what should refill it
        /// (e.g. a successful attack).
        public void ResetBonus()
        {
            if (_shiftedLines.Count == 0) return;
            _shiftedLines.Clear();
            Recalculate(notify: true);
        }

        private void Recalculate(bool notify)
        {
            Fill = Mathf.Clamp01(1f - _shiftedLines.Count * depletionPerShift);
            if (notify) OnFillChanged.Invoke(Fill);
        }
    }
}
