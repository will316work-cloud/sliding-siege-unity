using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using DG.Tweening;

namespace SlidingSiege
{
    /// Drag shift preview: translucent highlight bars over every linked line
    /// that will move, tiled arrows (one per cell) scrolling continuously in
    /// the shift direction, and a small nudge of the affected enemies toward
    /// the drag direction. Fades in/out with DOTween. Rendered OVER the
    /// enemies: overlayRoot must be the LAST child of the Enemy Layer (so it
    /// is masked and draws on top of enemy pieces).
    public class ShiftPreviewOverlay : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Empty RectTransform, last child of the Enemy Layer, stretched to fill it.")]
        [SerializeField] private RectTransform overlayRoot;

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
                actionOnRelease: img => img.gameObject.SetActive(false),
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

        private void Update()
        {
            if (!_visible || _arrows.Count == 0) return;
            // scrollSpeed is read every frame, so Inspector changes apply live.
            _scrollT += Time.deltaTime * scrollSpeed;
            float s = Mathf.Repeat(_scrollT, _metrics.Stride) * _dir;
            Vector2 offset = _isRow ? new Vector2(s, 0f) : new Vector2(0f, -s);
            for (int i = 0; i < _arrows.Count; i++)
                _arrows[i].rectTransform.anchoredPosition = _arrowBasePos[i] + offset;
        }

        // ---------------- Visual construction ----------------

        private void BuildVisuals(bool isRow, HashSet<int> lines)
        {
            Vector2 gridPx = _metrics.GridPixelSize(_state.Rows, _state.Cols);
            foreach (int line in lines)
            {
                // Highlight bar covering the whole line.
                var bar = _imagePool.Get();
                bar.sprite = null;
                bar.color = highlightColor;
                var barRT = bar.rectTransform;
                SetTopLeft(barRT);
                if (isRow)
                {
                    barRT.sizeDelta = new Vector2(gridPx.x, _metrics.CellSize);
                    barRT.anchoredPosition = CellTopLeft(line, 0);
                }
                else
                {
                    barRT.sizeDelta = new Vector2(_metrics.CellSize, gridPx.y);
                    barRT.anchoredPosition = CellTopLeft(0, line);
                }
                barRT.SetAsFirstSibling(); // bars under arrows
                _bars.Add(bar);

                // One arrow per cell along the line.
                int count = isRow ? _state.Cols : _state.Rows;
                for (int i = 0; i < count; i++)
                {
                    var arrow = _imagePool.Get();
                    arrow.sprite = arrowSprite;
                    arrow.color = arrowColor;
                    var rt = arrow.rectTransform;
                    SetCentered(rt);
                    rt.sizeDelta = new Vector2(arrowSize, arrowSize);
                    Vector2 basePos = isRow ? CellCenter(line, i) : CellCenter(i, line);
                    rt.anchoredPosition = basePos;
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

        private Vector2 CellTopLeft(int r, int c) => new Vector2(
            _metrics.ContentOffset.x + c * _metrics.Stride,
            -(_metrics.ContentOffset.y + r * _metrics.Stride));

        private Vector2 CellCenter(int r, int c) => new Vector2(
            _metrics.ContentOffset.x + c * _metrics.Stride + _metrics.CellSize * 0.5f,
            -(_metrics.ContentOffset.y + r * _metrics.Stride + _metrics.CellSize * 0.5f));
    }
}
