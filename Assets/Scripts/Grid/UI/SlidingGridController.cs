using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

namespace SlidingSiege
{
    /// Entry point. Initializes the backend, builds the UI for a runtime-
    /// configurable grid size, wires shift buttons, and enforces the flow:
    /// backend shifts first, then visuals tween to catch up; input is locked
    /// until the tween completes.
    ///
    /// Boot sequencing runs as a chain of UnityEvent phase boundaries
    /// (OnBoardBuilt -> OnInputWired -> OnCombatWired -> RunEnemyPhase)
    /// instead of one flat method: Awake wires each phase's default next
    /// step via AddListener so the game boots correctly out of the box,
    /// but every phase event is also public/serialized, so extra listeners
    /// (SFX, a loading-screen fade, whatever) can be layered on in the
    /// Inspector without touching this file. Don't remove the default
    /// AddListener wiring — nothing else re-establishes it if it's missing.
    public class SlidingGridController : MonoBehaviour
    {
        [Header("Grid size (runtime-configurable)")]
        [SerializeField, Min(2)] private int rows = 5;
        [SerializeField, Min(2)] private int cols = 5;

        [Header("Wiring (required)")]
        [SerializeField] private GridUIBuilder uiBuilder;
        [SerializeField] private EnemyViewManager enemyViewManager;
        [SerializeField] private GridDragInput dragInput;
        [SerializeField] private TargetingController targetingController;
        [SerializeField] private EnemyPhaseRunner enemyPhaseRunner;

        [Header("Wiring (optional)")]
        [Tooltip("Optional: damage bonus that depletes as rows/columns are shifted.")]
        [SerializeField] private DamageBonusSystem damageBonusSystem;
        [Tooltip("Optional: link-line display (Golem/Siren/Mage threads).")]
        [SerializeField] private LinkOverlay linkOverlay;
        [Tooltip("Optional: tint for slide-disabled rows/columns (Ghost/Phantom curses).")]
        [SerializeField] private DisabledLineOverlay disabledLineOverlay;
        [SerializeField] private ShiftPreviewOverlay shiftPreviewOverlay;
        [SerializeField] private AbilityHighlightOverlay abilityHighlightOverlay;
        [SerializeField] private DamageTextSpawner damageTextSpawner;

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float shiftDuration = 0.18f;
        [SerializeField] private Ease shiftEase = Ease.OutCubic;

        [Header("Boot sequence (phase events)")]
        [Tooltip("Board/GridState/EnemyViewManager ready. Default listener: WireInput.")]
        public UnityEvent OnBoardBuilt = new UnityEvent();
        [Tooltip("Drag input, targeting, and phase runner wired. Default listener: WireOverlaysAndCombat.")]
        public UnityEvent OnInputWired = new UnityEvent();
        [Tooltip("Overlays, damage bonus, undo button, and the trigger dispatcher wired. Default listener: StartGame.")]
        public UnityEvent OnCombatWired = new UnityEvent();

        public GridState State { get; private set; }

        private IMoveAnimator _animator;
        private AbilityTriggerDispatcher _triggerDispatcher;
        private bool _rebuildQueued;

        // Obtained via ShiftTracker.OnReady (Inspector-wired to
        // ReceiveShiftTracker below), not a SerializeField reference.
        private ShiftTracker _shiftTracker;

        private void Awake()
        {
            // Default boot chain — see the class doc for why this stays.
            OnBoardBuilt.AddListener(WireInput);
            OnInputWired.AddListener(WireOverlaysAndCombat);
            OnCombatWired.AddListener(StartGame);
        }

        private void Start()
        {
            State = new GridState();
            _animator = new DOTweenSlideAnimator(shiftDuration, shiftEase);

            // Shift-track buttons are no longer hooked up (drag input is the
            // active method); the track code/UI remains for later use.
            uiBuilder.OnLayoutChangeRequested += HandleLayoutChangeRequested;

            // Builds the board
            State.Initialize(rows, cols);
            uiBuilder.Build(rows, cols);
            enemyViewManager.Initialize(State, uiBuilder.Metrics, _animator);

            OnBoardBuilt.Invoke();
        }

        private void WireInput()
        {
            // Drag/swipe + tap input over the grid. Dragging is locked while
            // a tween runs OR while an attack/item is selected.
            dragInput.Initialize(State, uiBuilder.Metrics,
                isInputLocked: () => enemyViewManager.IsAnimating || targetingController.IsTargeting || enemyPhaseRunner.IsRunning,
                requestShift: HandleShiftPressed);
            dragInput.OnCellTapped.AddListener(targetingController.HandleCellTapped);
            targetingController.Initialize(State, enemyPhaseRunner);
            enemyPhaseRunner.Initialize(State, enemyViewManager);

            OnInputWired.Invoke();
        }

        private void WireOverlaysAndCombat()
        {
            if (shiftPreviewOverlay != null) shiftPreviewOverlay.Initialize(State, uiBuilder.Metrics, enemyViewManager);

            // Combat: attacks, items, targeting, hitbox highlight overlay.
            if (abilityHighlightOverlay != null) abilityHighlightOverlay.Initialize(uiBuilder.Metrics);
            if (damageTextSpawner != null) damageTextSpawner.Initialize(State, enemyViewManager);
            if (linkOverlay != null) linkOverlay.Initialize(State, targetingController.Combat, enemyViewManager);
            if (disabledLineOverlay != null) disabledLineOverlay.Initialize(State, uiBuilder.Metrics);
            if (damageBonusSystem != null)
                targetingController.Combat.DamageMultiplier = () => damageBonusSystem.CurrentMultiplier;

            OnCombatWired.Invoke();
        }

        private void StartGame()
        {
            // Event-triggered abilities (OnSpawn/OnDamaged/OnCritical/OnDeath)
            // run outside the phase runner; the dispatcher pumps on this host.
            _triggerDispatcher = new AbilityTriggerDispatcher(
                State, enemyViewManager, enemyPhaseRunner, targetingController.Combat, this);
            enemyPhaseRunner.AttachTriggerDispatcher(_triggerDispatcher);

            // Opening enemy phase: the runner's spawn abilities populate the
            // board (and newcomers run their not-yet-passed abilities, e.g.
            // outlines) before the player's first turn.
            enemyPhaseRunner.RunEnemyPhase();
        }

        /// Slot this into ShiftTracker.OnReady (Inspector) so this script
        /// can call RegisterShift/PopLastLine/CanUndo on it without ever
        /// holding a SerializeField reference to it.
        public void ReceiveShiftTracker(ShiftTracker tracker) => _shiftTracker = tracker;

        private void HandleShiftPressed(bool isRowShift, int index, int direction)
        {
            if (enemyViewManager.IsAnimating) return; // input locked during tween

            // Cursed lines can't slide — including indirectly via the
            // linked-line expansion (a multi-line enemy would drag them).
            foreach (var line in State.LinkedLinesForAxis(isRowShift, index))
                if (State.IsLineDisabled(isRowShift, line)) return;

            // 1) Backend updates first...
            ShiftResult result = isRowShift
                ? State.ShiftRow(index, direction)
                : State.ShiftCol(index, direction);

            if (_shiftTracker != null) _shiftTracker.RegisterShift(result);

            // 2) ...then visuals catch up from the anchor diff.
            uiBuilder.SetButtonsInteractable(false);
            enemyViewManager.AnimateShift(result, () =>
            {
                uiBuilder.SetButtonsInteractable(true);
                // Layout values changed mid-tween: rebuild now that it's done.
                if (_rebuildQueued) RebuildLayout();
            });
        }

        /// Reverses every shift of the most recently touched line in one
        /// press (ShiftTracker.PopLastLine — see its doc for why it stops
        /// at a cross-axis barrier), same input gating as a forward shift.
        /// No-op if there's nothing to undo. Public: wire a Button's
        /// onClick to this directly in the Inspector instead of a
        /// SerializeField Button reference here.
        public void HandleUndoShiftPressed()
        {
            if (enemyViewManager.IsAnimating || targetingController.IsTargeting || enemyPhaseRunner.IsRunning) return;
            if (_shiftTracker == null || !_shiftTracker.CanUndo) return;

            var popped = _shiftTracker.PopLastLine();
            if (popped.Count == 0) return;

            uiBuilder.SetButtonsInteractable(false);
            UndoNext(popped, 0);
        }

        /// Applies and animates popped[index], then recurses to the next
        /// entry — each entry is only safe to reverse once the one after it
        /// (chronologically) has already been undone, and popped is
        /// ordered most-recent-first.
        private void UndoNext(List<ShiftResult> popped, int index)
        {
            if (index >= popped.Count)
            {
                uiBuilder.SetButtonsInteractable(true);
                if (_rebuildQueued) RebuildLayout();
                return;
            }

            ShiftResult undoResult = State.UnshiftResult(popped[index]);
            enemyViewManager.AnimateShift(undoResult, () => UndoNext(popped, index + 1));
        }

        private void HandleLayoutChangeRequested()
        {
            if (enemyViewManager.IsAnimating) { _rebuildQueued = true; return; }
            RebuildLayout();
        }

        /// Re-lays-out the board UI and enemy visuals with the latest Layout
        /// values. GridState (enemy positions, ids) is untouched.
        private void RebuildLayout()
        {
            _rebuildQueued = false;
            uiBuilder.Rebuild();                 // resizes root, cells, buttons, updates metrics
            enemyViewManager.RebuildAll();       // re-snaps every enemy's pieces to new metrics
        }

        private void OnDestroy()
        {
            if (uiBuilder != null)
                uiBuilder.OnLayoutChangeRequested -= HandleLayoutChangeRequested;
            _animator?.Kill();
        }
    }
}
