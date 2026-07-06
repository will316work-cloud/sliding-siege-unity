using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using DG.Tweening;

namespace SlidingSiege
{
    /// Drag shift preview. The overlay root itself is never resized or moved
    /// by this script — only the bars and arrows extend beyond it:
    ///  - highlight bars span the line's full length EXPANDED per side by the
    ///    stretch offsets (fixed to CellSize on the other axis),
    ///  - arrows tile one per stride along the line, scrolling in the shift
    ///    direction; each arrow stays visible until it travels past the grid
    ///    edge by the stretch offset (then despawns), while new arrows spawn
    ///    in from beyond the opposite boundary.
    /// Enemies on all linked lines get a fixed nudge toward the direction.
    public class ShiftPreviewOverlay : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Overlay RectTransform: sibling AFTER the Enemy Layer, same rect. Not modified by this script.")]
        [SerializeField] private RectTransform overlayRoot;

        [Header("Stretch offsets (px beyond the grid edge, per side)")]
        [Tooltip("How far bars extend and arrows travel beyond the grid edge before despawning. Live-updates in Play mode.")]
        [SerializeField] private RectOffset stretchOffsets = new RectOffset();

        [Header("Appearance")]
        [Tooltip("Color overlay of the highlighted lines (alpha = translucency).")]
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.25f);
        [Tooltip("Arrow sprite, authored POINTING RIGHT; rotated for other directions.")]
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private Color arrowColor = Color.white;
        [SerializeField, Min(1f)] private float arrowSize = 24f;

        [Header("Motion")]
        [Tooltip("Arrow scroll speed in pixels/second. Live-updates in Play mode.")]
        [SerializeField, Min(0f)] private float scrollSpeed = 60f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.15f;
        [Tooltip("Enemy nudge as a fraction of a cell.")]
        [SerializeField, Range(0f, 0.5f)] private float nudgeFraction = 0.15f;

        private GridState _state;
        private GridLayoutMetrics _metrics;
        private EnemyViewManager _viewManager;

        private CanvasGroup _group;
        private ObjectPool<Image> _imagePool;
        private readonly List<Image> _bars = new List<Image>();
        private readonly List<Image> _arrows = new List<Image>();
        private readonly List<Vector2> _arrowBasePos = new List<Vector2>();

        private bool _visible;
        private bool _isRow;
        private int _dir;
        private HashSet<int> _lines = new HashSet<int>();
        private float _scrollT;
        private bool _pendingValidate;

        // Visible travel bounds along the scroll axis (top-left-down space),
        // recomputed per build: grid edge expanded by the stretch offsets.
        private float _travelMin, _travelMax;

        public void Initialize(GridState state, GridLayoutMetrics metrics, EnemyViewManager viewManager)
        {
            _state = state;
            _metrics = metrics;
            _viewManager = viewManager;

            _group = overlayRoot.GetComponent<CanvasGroup>();
            if (_group == null) _group = overlayRoot.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _imagePool ??= new ObjectPool<Image>(
                createFunc: () =>
                {
                    var go = new GameObject("PreviewImage", typeof(RectTransform), typeof(Image));
                    var img = go.GetComponent<Image>();
                    img.raycastTarget = false;
                    go.transform.SetParent(overlayRoot, false);
                    return img;
                },
                actionOnGet: img => { img.gameObject.SetActive(true); img.transform.SetParent(overlayRoot, false); },
                actionOnRelease: img => { img.enabled = true; img.gameObject.SetActive(false); },
                actionOnDestroy: img => Destroy(img.gameObject),
                defaultCapacity: 24);
        }

        /// Show/update the preview for a drag on `seed` (row index if isRow,
        /// else column index) toward `dir` (+1/-1). Safe to call every frame;
        /// rebuilds visuals only when the affected lines or direction change.
        public void Show(bool isRow, int seed, int dir)
        {
            var lines = _state.LinkedLinesForAxis(isRow, seed);
            bool layoutChanged = !_visible || isRow != _isRow || !lines.SetEquals(_lines);
            bool dirChanged = dir != _dir;

            if (layoutChanged)
            {
                ReleaseVisuals();
                BuildVisuals(isRow, lines);
            }
            if (layoutChanged || dirChanged)
                ApplyArrowDirection(isRow, dir);

            _isRow = isRow;
            _dir = dir;
            _lines = lines;

            if (!_visible)
            {
                _visible = true;
                _group.DOKill();
                _group.DOFade(1f, fadeDuration);
            }

            // Nudge the enemies that will actually move; settle the rest.
            Vector2 nudge = NudgeOffset(isRow, dir);
            var moving = _state.EnemiesOnLines(isRow, lines);
            _viewManager.ClearNudges(fadeDuration);
            _viewManager.NudgeEnemies(moving, nudge, fadeDuration);
        }

        /// Fade everything out and settle the enemies back (also used when the
        /// pointer re-enters the cancel zone).
        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            _group.DOKill();
            _group.DOFade(0f, fadeDuration).OnComplete(ReleaseVisuals);
            _viewManager.ClearNudges(fadeDuration);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || _state == null) return;
            _pendingValidate = true; // hierarchy work is deferred to Update
        }

        private void Update()
        {
            if (_pendingValidate)
            {
                _pendingValidate = false;
                if (_visible)
                {
                    ReleaseVisuals();
                    BuildVisuals(_isRow, _lines);
                    ApplyArrowDirection(_isRow, _dir);
                }
            }

            if (!_visible || _arrows.Count == 0) return;
            // scrollSpeed is read every frame, so Inspector changes apply live.
            _scrollT += Time.deltaTime * scrollSpeed;
            float s = Mathf.Repeat(_scrollT, _metrics.Stride) * _dir;
            Vector2 offset = _isRow ? new Vector2(s, 0f) : new Vector2(0f, -s);

            for (int i = 0; i < _arrows.Count; i++)
            {
                var img = _arrows[i];
                Vector2 pos = _arrowBasePos[i] + offset;
                img.rectTransform.anchoredPosition = pos;
                // Despawn/spawn at the stretch boundaries: an arrow is only
                // drawn while its center is within the extended travel range.
                float axisCoord = _isRow ? pos.x : -pos.y;
                bool inRange = axisCoord >= _travelMin && axisCoord <= _travelMax;
                if (img.enabled != inRange) img.enabled = inRange;
            }
        }

        // ---------------- Visual construction ----------------

        private void BuildVisuals(bool isRow, HashSet<int> lines)
        {
            Vector2 gridPx = _metrics.GridPixelSize(_state.Rows, _state.Cols);
            float stride = _metrics.Stride;

            // Extended travel bounds along the scroll axis (top-left-down space).
            if (isRow)
            {
                _travelMin = _metrics.ContentOffset.x - stretchOffsets.left;
                _travelMax = _metrics.ContentOffset.x + gridPx.x + stretchOffsets.right;
            }
            else
            {
                _travelMin = _metrics.ContentOffset.y - stretchOffsets.top;
                _travelMax = _metrics.ContentOffset.y + gridPx.y + stretchOffsets.bottom;
            }

            foreach (int line in lines)
            {
                // Highlight bar: line length expanded per side, CellSize across.
                var bar = _imagePool.Get();
                bar.sprite = null;
                bar.color = highlightColor;
                var barRT = bar.rectTransform;
                SetTopLeft(barRT);
                if (isRow)
                {
                    barRT.sizeDelta = new Vector2(gridPx.x + stretchOffsets.left + stretchOffsets.right, _metrics.CellSize);
                    barRT.anchoredPosition = new Vector2(_metrics.ContentOffset.x - stretchOffsets.left, CellTopLeft(line, 0).y);
                }
                else
                {
                    barRT.sizeDelta = new Vector2(_metrics.CellSize, gridPx.y + stretchOffsets.top + stretchOffsets.bottom);
                    barRT.anchoredPosition = new Vector2(CellTopLeft(0, line).x, -(_metrics.ContentOffset.y - stretchOffsets.top));
                }
                barRT.SetAsFirstSibling(); // bars under arrows
                _bars.Add(bar);

                // Arrows: tiled one per stride from cell centers, extended one
                // stride beyond each travel boundary so entering arrows are
                // already positioned when they cross into the visible range.
                float firstCenter = isRow
                    ? CellCenter(line, 0).x
                    : -CellCenter(0, line).y; // top-left-down space, positive
                int iMin = Mathf.FloorToInt((_travelMin - stride - firstCenter) / stride);
                int iMax = Mathf.CeilToInt((_travelMax + stride - firstCenter) / stride);

                for (int i = iMin; i <= iMax; i++)
                {
                    var arrow = _imagePool.Get();
                    arrow.sprite = arrowSprite;
                    arrow.color = arrowColor;
                    var rt = arrow.rectTransform;
                    SetCentered(rt);
                    rt.sizeDelta = new Vector2(arrowSize, arrowSize);
                    float axisCoord = firstCenter + i * stride;
                    Vector2 basePos = isRow
                        ? new Vector2(axisCoord, CellCenter(line, 0).y)
                        : new Vector2(CellCenter(0, line).x, -axisCoord);
                    rt.anchoredPosition = basePos;
                    arrow.enabled = axisCoord >= _travelMin && axisCoord <= _travelMax;
                    _arrows.Add(arrow);
                    _arrowBasePos.Add(basePos);
                }
            }
        }

        private void ApplyArrowDirection(bool isRow, int dir)
        {
            float z = isRow ? (dir > 0 ? 0f : 180f) : (dir > 0 ? -90f : 90f);
            foreach (var arrow in _arrows)
                arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, z);
        }

        private Vector2 NudgeOffset(bool isRow, int dir)
        {
            float amount = nudgeFraction * _metrics.CellSize * dir;
            return isRow ? new Vector2(amount, 0f) : new Vector2(0f, -amount);
        }

        private void ReleaseVisuals()
        {
            foreach (var img in _bars) _imagePool.Release(img);
            _bars.Clear();
            foreach (var img in _arrows) _imagePool.Release(img);
            _arrows.Clear();
            _arrowBasePos.Clear();
        }

        // ---------------- Layout helpers ----------------

        private static void SetTopLeft(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.localEulerAngles = Vector3.zero;
        }

        private static void SetCentered(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// Cell coords in overlay-local top-left space (overlay rect assumed
        /// to match the Enemy Layer rect; the root is never modified).
        private Vector2 CellTopLeft(int r, int c) => new Vector2(
            _metrics.ContentOffset.x + c * _metrics.Stride,
            -(_metrics.ContentOffset.y + r * _metrics.Stride));

        private Vector2 CellCenter(int r, int c) => new Vector2(
            _metrics.ContentOffset.x + c * _metrics.Stride + _metrics.CellSize * 0.5f,
            -(_metrics.ContentOffset.y + r * _metrics.Stride + _metrics.CellSize * 0.5f));
    }
}
