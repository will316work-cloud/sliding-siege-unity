using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SlidingSiege
{
    /// Pointer input surface for the grid. Attach to a full-grid-area
    /// raycastable Graphic (e.g. a transparent Image overlaying the Enemy
    /// Layer). Works with mouse and touch alike via the EventSystem — use
    /// InputSystemUIInputModule on your EventSystem for the new Input System.
    ///
    /// Behavior:
    ///  - A drag locks to the dominant axis of its FIRST movement and, on
    ///    release, commits exactly ONE shift of the row/column of the cell
    ///    the drag STARTED on (horizontal -> row, vertical -> column).
    ///    Direction follows the drag: right/down = +1, left/up = -1.
    ///  - A press-and-release that never exceeds the EventSystem's drag
    ///    threshold is a TAP: it raises OnEnemyTapped for the topmost enemy
    ///    on the tapped cell (nothing if the cell is empty).
    public class GridDragInput : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [Header("Wiring")]
        [Tooltip("Rect whose area maps 1:1 onto the grid cells (the Enemy Layer / Grid Panel rect).")]
        [SerializeField] private RectTransform gridArea;

        [Header("Events")]
        public EnemyTappedEvent OnEnemyTapped = new EnemyTappedEvent();

        private GridState _state;
        private GridLayoutMetrics _metrics;
        private Func<bool> _isInputLocked;
        private Action<bool, int, int> _requestShift; // (isRowShift, index, dir)

        private bool _dragActive;
        private bool _dragIsHorizontal;
        private Vector2Int _dragStartCell;

        public void Initialize(GridState state, GridLayoutMetrics metrics,
            Func<bool> isInputLocked, Action<bool, int, int> requestShift)
        {
            _state = state;
            _metrics = metrics;
            _isInputLocked = isInputLocked;
            _requestShift = requestShift;
        }

        // ---------------- Drag = shift ----------------

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragActive = false;
            if (_state == null || (_isInputLocked?.Invoke() ?? false)) return;
            if (!TryGetCell(eventData.pressPosition, eventData.pressEventCamera, out _dragStartCell)) return;

            // Lock axis from the dominant direction of the first movement.
            Vector2 initial = eventData.position - eventData.pressPosition;
            if (initial == Vector2.zero) initial = eventData.delta;
            if (initial == Vector2.zero) return;
            _dragIsHorizontal = Mathf.Abs(initial.x) >= Mathf.Abs(initial.y);
            _dragActive = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Axis stays locked; nothing to do until release.
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragActive) return;
            _dragActive = false;
            if (_isInputLocked?.Invoke() ?? false) return;

            Vector2 total = eventData.position - eventData.pressPosition;
            if (_dragIsHorizontal)
            {
                if (Mathf.Approximately(total.x, 0f)) return;
                int dir = total.x > 0f ? +1 : -1;              // drag right = row shifts right
                _requestShift?.Invoke(true, _dragStartCell.x, dir);
            }
            else
            {
                if (Mathf.Approximately(total.y, 0f)) return;
                int dir = total.y < 0f ? +1 : -1;              // drag down (screen y-) = column shifts down
                _requestShift?.Invoke(false, _dragStartCell.y, dir);
            }
        }

        // ---------------- Tap = select ----------------

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return; // committed drags never select
            if (_state == null) return;
            if (!TryGetCell(eventData.position, eventData.pressEventCamera, out var cell)) return;

            foreach (var enemy in _state.EnemiesAt(cell.x, cell.y))
            {
                OnEnemyTapped?.Invoke(enemy);
                return; // topmost/first only
            }
        }

        // ---------------- Screen point -> cell ----------------

        /// Maps a screen point to a (row, col) cell using the shared metrics
        /// (same ContentOffset/Stride the visuals use). False if outside the
        /// cell area.
        private bool TryGetCell(Vector2 screenPos, Camera cam, out Vector2Int cell)
        {
            cell = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridArea, screenPos, cam, out var local))
                return false;

            Rect rect = gridArea.rect;
            // Convert to top-left-origin pixel space (+x right, +y down).
            float x = local.x - rect.xMin - _metrics.ContentOffset.x;
            float y = (rect.yMax - local.y) - _metrics.ContentOffset.y;
            if (x < 0f || y < 0f) return false;

            int col = (int)(x / _metrics.Stride);
            int row = (int)(y / _metrics.Stride);
            if (row < 0 || row >= _state.Rows || col < 0 || col >= _state.Cols) return false;

            cell = new Vector2Int(row, col);
            return true;
        }
    }
}
