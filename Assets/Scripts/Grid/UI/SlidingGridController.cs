using UnityEngine;
using DG.Tweening;

namespace SlidingSiege
{
    /// Entry point. Initializes the backend, builds the UI for a runtime-
    /// configurable grid size, wires shift buttons, and enforces the flow:
    /// backend shifts first, then visuals tween to catch up; input is locked
    /// until the tween completes.
    public class SlidingGridController : MonoBehaviour
    {
        [Header("Grid size (runtime-configurable)")]
        [SerializeField, Min(2)] private int rows = 5;
        [SerializeField, Min(2)] private int cols = 5;

        [Header("Wiring")]
        [SerializeField] private GridUIBuilder uiBuilder;
        [SerializeField] private EnemyViewManager enemyViewManager;
        [SerializeField] private GridDragInput dragInput;
        [SerializeField] private ShiftPreviewOverlay shiftPreviewOverlay;
        [SerializeField] private TargetingController targetingController;
        [SerializeField] private AbilityHighlightOverlay abilityHighlightOverlay;
        [SerializeField] private DamageTextSpawner damageTextSpawner;
        [SerializeField] private EnemyPhaseRunner enemyPhaseRunner;

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float shiftDuration = 0.18f;
        [SerializeField] private Ease shiftEase = Ease.OutCubic;

        public GridState State { get; private set; }

        private IMoveAnimator _animator;
        private bool _rebuildQueued;

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

            // Drag/swipe + tap input over the grid. Dragging is locked while
            // a tween runs OR while an attack/item is selected.
            dragInput.Initialize(State, uiBuilder.Metrics,
                isInputLocked: () => enemyViewManager.IsAnimating || targetingController.IsTargeting || enemyPhaseRunner.IsRunning,
                requestShift: HandleShiftPressed);
            dragInput.OnCellTapped.AddListener(targetingController.HandleCellTapped);
            shiftPreviewOverlay.Initialize(State, uiBuilder.Metrics, enemyViewManager);

            // Combat: attacks, items, targeting, hitbox highlight overlay.
            abilityHighlightOverlay.Initialize(uiBuilder.Metrics);
            targetingController.Initialize(State, enemyPhaseRunner);
            damageTextSpawner.Initialize(State, enemyViewManager);
            enemyPhaseRunner.Initialize(State, enemyViewManager);

            // Opening enemy phase: the runner's spawn abilities populate the
            // board (and newcomers run their not-yet-passed abilities, e.g.
            // outlines) before the player's first turn.
            enemyPhaseRunner.RunEnemyPhase();
        }

        private void HandleShiftPressed(bool isRowShift, int index, int direction)
        {
            if (enemyViewManager.IsAnimating) return; // input locked during tween

            // 1) Backend updates first...
            ShiftResult result = isRowShift
                ? State.ShiftRow(index, direction)
                : State.ShiftCol(index, direction);

            // 2) ...then visuals catch up from the anchor diff.
            uiBuilder.SetButtonsInteractable(false);
            enemyViewManager.AnimateShift(result, () =>
            {
                uiBuilder.SetButtonsInteractable(true);
                // Layout values changed mid-tween: rebuild now that it's done.
                if (_rebuildQueued) RebuildLayout();
            });
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
