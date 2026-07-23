using UnityEngine;
using UnityEngine.Events;

namespace SlidingSiege
{
    [System.Serializable]
    public class FillChangedEvent : UnityEvent<float> { }

    /// Scales CombatSystem.DamageMultiplier between 1x and MaxMultiplier
    /// from a shifted-line count it doesn't own or reference directly.
    /// Purely event-driven, both ways:
    ///  - IN: slot HandleShiftedCountChanged into whatever tracks shifts
    ///    (e.g. ShiftTracker.OnShiftedCountChanged) via the Inspector.
    ///  - OUT: OnFillChanged forwards fill (0..1) to any display's
    ///    float-parameter method (fill image, slider, text, ...).
    ///  - OUT: OnResetRequested is raised by ResetBonus() for whatever
    ///    owns the shift count to clear itself (e.g. slot
    ///    ShiftTracker.ResetTracking here) — the fill then updates once
    ///    that comes back through HandleShiftedCountChanged(0).
    public class DamageBonusSystem : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Damage multiplier at full bonus (fill = 1). Fill = 0 always multiplies by 1x.")]
        [SerializeField] private float maxDamageMultiplier = 2f;
        [Tooltip("Fraction of the fill (0..1) lost per distinct row/column shifted.")]
        [SerializeField, Range(0f, 1f)] private float depletionPerShift = 0.125f;

        [Header("Events (out)")]
        [Tooltip("Raised with the new fill (0..1) whenever it changes, including on Start. Slot a display's float-parameter method here.")]
        public FillChangedEvent OnFillChanged = new FillChangedEvent();
        [Tooltip("Raised when ResetBonus() is called. Slot whatever owns shift tracking's reset method here (e.g. ShiftTracker.ResetTracking).")]
        public UnityEvent OnResetRequested = new UnityEvent();

        public float Fill { get; private set; } = 1f;
        public float CurrentMultiplier => Mathf.Lerp(1f, maxDamageMultiplier, Fill);

        private void Start() => OnFillChanged.Invoke(Fill);

        /// Slot this into a shift tracker's count-changed event (Inspector).
        public void HandleShiftedCountChanged(int count) => Recalculate(count, notify: true);

        /// Never called automatically — callers decide what should refill
        /// the bonus (e.g. a successful attack). Raises OnResetRequested;
        /// doesn't touch Fill directly, since that comes back through
        /// HandleShiftedCountChanged once the reset propagates.
        public void ResetBonus() => OnResetRequested.Invoke();

        private void Recalculate(int shiftedCount, bool notify)
        {
            Fill = Mathf.Clamp01(1f - shiftedCount * depletionPerShift);
            if (notify) OnFillChanged.Invoke(Fill);
        }
    }
}
