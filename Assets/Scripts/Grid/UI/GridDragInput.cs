using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace SlidingSiege
{
    [Serializable] public class ShiftPreviewShowEvent : UnityEvent<bool, int, int> { }
    [Serializable] public class DisabledLineEvent : UnityEvent<bool, int> { }
    [Serializable] public class DisabledLinesEvent : UnityEvent<IEnumerable<(bool IsRow, int Index)>> { }

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
    ///    threshold is a TAP: it raises OnCellTapped with the tapped cell
    ///    (TargetingController decides what a tap means).
    ///
    /// No longer holds direct references to ShiftPreviewOverlay/
    /// DisabledLineOverlay — every moment that used to call into them
    /// directly is a UnityEvent instead; slot the overlays' methods into
    /// them via the Inspector (see each event's tooltip for which method).
    public class GridDragInput : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [Header("Wiring (required)")]
        [Tooltip("Rect whose area maps 1:1 onto the grid cells (the Enemy Layer / Grid Panel rect).")]
        [SerializeField] private RectTransform gridArea;

        [Header("Events")]
        [Tooltip("Raised for every non-drag tap on a grid cell. Wire TargetingController.HandleCellTapped here (or in code).")]
        public CellTappedEvent OnCellTapped = new CellTappedEvent();
        [Tooltip("Raised to show the shift preview (isRow, seed line, direction). Slot ShiftPreviewOverlay.Show here.")]
        public ShiftPreviewShowEvent OnPreviewShowRequested = new ShiftPreviewShowEvent();
        [Tooltip("Raised to hide the shift preview — mid-drag when inactive/blocked, and always at drag end. Slot ShiftPreviewOverlay.Hide here.")]
        public UnityEvent OnPreviewHideRequested = new UnityEvent();
        [Tooltip("Raised with the currently-blocking cursed lines (possibly empty) as the drag preview updates. Slot DisabledLineOverlay.SetHighlight here.")]
        public DisabledLinesEvent OnDisabledLinesChanged = new DisabledLinesEvent();
        [Tooltip("Raised once at drag end to clear any cursed-line highlight. Slot DisabledLineOverlay.ClearHighlight here.")]
        public UnityEvent OnDisabledHighlightCleared = new UnityEvent();
        [Tooltip("Raised once per blocking line when a drag release is rejected by a cursed line. Slot DisabledLineOverlay.Shake here.")]
        public DisabledLineEvent OnDisabledLineShakeRequested = new DisabledLineEvent();

        private GridState _state;
        private GridLayoutMetrics _metrics;
        private Func<bool> _isInputLocked;
        private Action<bool, int, int> _requestShift; // (isRowShift, index, dir)

        private bool _dragActive;
        private bool _dragIsHorizontal;
        private Vector2Int _dragStartCell;
        private bool _inCancelZone;
        private int _currentDir;

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
            _inCancelZone = true;
            _currentDir = 0;
            UpdatePreview(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragActive) return;
            UpdatePreview(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragActive) return;
            _dragActive = false;
            UpdatePreview(eventData);
            OnPreviewHideRequested.Invoke();
            OnDisabledHighlightCleared.Invoke();

            if (_isInputLocked?.Invoke() ?? false) return;
            // Cancel: pointer never left (or returned to) the start cell.
            if (_inCancelZone || _currentDir == 0) return;

            int index = _dragIsHorizontal ? _dragStartCell.x : _dragStartCell.y;
            var blocking = BlockingDisabledLines(_dragIsHorizontal, index);
            if (blocking.Count > 0)
            {
                // Cursed: no shift — the blocking line(s) shake instead.
                foreach (var line in blocking)
                    OnDisabledLineShakeRequested.Invoke(line.IsRow, line.Index);
                return;
            }

            _requestShift?.Invoke(_dragIsHorizontal, index, _currentDir);
        }

        /// Recomputes cancel-zone state and current direction from the live
        /// pointer position, and shows/hides/updates the preview overlay.
        /// A shift blocked by cursed lines gets no preview; the blocking
        /// lines highlight instead. Direction can flip mid-drag; the axis
        /// never does.
        private void UpdatePreview(PointerEventData eventData)
        {
            // Cancel zone: pointer still within the start cell's bounds.
            _inCancelZone =
                TryGetCell(eventData.position, eventData.pressEventCamera, out var cell)
                && cell == _dragStartCell;

            Vector2 total = eventData.position - eventData.pressPosition;
            float along = _dragIsHorizontal ? total.x : -total.y; // +1 = right/down
            if (!Mathf.Approximately(along, 0f))
                _currentDir = along > 0f ? +1 : -1;

            bool inactive = _inCancelZone || _currentDir == 0;
            int index = _dragIsHorizontal ? _dragStartCell.x : _dragStartCell.y;
            var blocking = inactive
                ? null
                : BlockingDisabledLines(_dragIsHorizontal, index);
            bool blocked = blocking != null && blocking.Count > 0;

            OnDisabledLinesChanged.Invoke(blocked ? blocking : null);
            if (inactive || blocked)
                OnPreviewHideRequested.Invoke();
            else
                OnPreviewShowRequested.Invoke(_dragIsHorizontal, index, _currentDir);
        }

        /// Cursed lines that block shifting the given line — the line itself
        /// and anything dragged along via linked-line expansion.
        private List<(bool IsRow, int Index)> BlockingDisabledLines(bool isRowShift, int index)
        {
            var blocking = new List<(bool, int)>();
            foreach (var line in _state.LinkedLinesForAxis(isRowShift, index))
                if (_state.IsLineDisabled(isRowShift, line))
                    blocking.Add((isRowShift, line));
            return blocking;
        }

        // ---------------- Tap = select ----------------

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return; // committed drags never select
            if (_state == null) return;
            if (!TryGetCell(eventData.position, eventData.pressEventCamera, out var cell)) return;
            OnCellTapped?.Invoke(cell);
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
