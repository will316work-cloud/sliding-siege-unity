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

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float shiftDuration = 0.18f;
        [SerializeField] private Ease shiftEase = Ease.OutCubic;

        [Header("Test spawns (optional)")]
        [SerializeField] private EnemyDefinition[] testSpawns;
        [SerializeField] private Vector2Int[] testSpawnCells;

        public GridState State { get; private set; }

        private IMoveAnimator _animator;

        private void Start()
        {
            State = new GridState();
            _animator = new DOTweenSlideAnimator(shiftDuration, shiftEase);

            uiBuilder.OnShiftButtonPressed += HandleShiftPressed;

            // Builds the board
            State.Initialize(rows, cols);
            uiBuilder.Build(rows, cols);
            enemyViewManager.Initialize(State, uiBuilder.Metrics, _animator);

            // Optional inspector-driven test spawns.
            for (int i = 0; i < testSpawns.Length && i < testSpawnCells.Length; i++)
            {
                var cell = testSpawnCells[i];
                if (State.CanPlaceAt(cell.y, cell.x, testSpawns[i]))
                    State.SpawnEnemy(testSpawns[i], cell.y, cell.x);
            }
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
            enemyViewManager.AnimateShift(result, () => uiBuilder.SetButtonsInteractable(true));
        }

        private void OnDestroy()
        {
            if (uiBuilder != null) uiBuilder.OnShiftButtonPressed -= HandleShiftPressed;
            _animator?.Kill();
        }
    }
}
