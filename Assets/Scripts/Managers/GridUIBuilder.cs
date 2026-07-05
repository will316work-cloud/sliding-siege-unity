using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Builds/rebuilds the Grid Panel cells and the four shift-button tracks
    /// at runtime for any grid size, RESPECTING the existing stretch-anchored
    /// RectTransforms: it never resizes or repositions Grid Panel, Enemy
    /// Layer, or the tracks along their stretched axes. Instead, the cell
    /// size is COMPUTED from the Grid Panel's current stretched rect so the
    /// board always fits inside it (content is centered), and each track is
    /// only given a thickness on its non-stretched cross axis.
    ///
    /// Expected hierarchy (assign in inspector):
    ///   Grid
    ///     Left Shift Track    (GridLayoutGroup, stretched vertically)
    ///     Top Shift Track     (GridLayoutGroup, stretched horizontally)
    ///     Right Shift Track   (GridLayoutGroup, stretched vertically)
    ///     Bottom Shift Track  (GridLayoutGroup, stretched horizontally)
    ///     Grid Panel          (GridLayoutGroup, stretched both axes)
    ///     Enemy Layer         (Image + Mask, stretched both axes, same rect as Grid Panel)
    public class GridUIBuilder : MonoBehaviour
    {
        [Header("Hierarchy")]
        [SerializeField] private RectTransform gridRoot;      // the "Grid" object (center-anchored, non-stretch)
        [SerializeField] private GridLayoutGroup gridPanel;
        [SerializeField] private GridLayoutGroup leftTrack;
        [SerializeField] private GridLayoutGroup topTrack;
        [SerializeField] private GridLayoutGroup rightTrack;
        [SerializeField] private GridLayoutGroup bottomTrack;
        [SerializeField] private RectTransform enemyLayer;

        [Header("Prefabs")]
        [SerializeField] private RectTransform cellPrefab;   // plain cell background
        [SerializeField] private Button shiftButtonPrefab;   // Button with a TextMeshProUGUI child

        [Header("Layout")]
        [SerializeField] private GridLayoutMetrics metrics = new GridLayoutMetrics();
        [SerializeField] private RectOffset padding = new RectOffset();
        [SerializeField] private float buttonThickness = 30f;

        public GridLayoutMetrics Metrics => metrics;
        public RectTransform EnemyLayer => enemyLayer;

        /// (isRowShift, lineIndex, direction)
        public event Action<bool, int, int> OnShiftButtonPressed;

        private ObjectPool<RectTransform> _cellPool;
        private ObjectPool<Button> _buttonPool;
        private readonly List<RectTransform> _activeCells = new List<RectTransform>();
        private readonly List<Button> _activeButtons = new List<Button>();

        private void EnsurePools()
        {
            _cellPool ??= new ObjectPool<RectTransform>(
                createFunc: () => Instantiate(cellPrefab),
                actionOnGet: rt => rt.gameObject.SetActive(true),
                actionOnRelease: rt => { rt.gameObject.SetActive(false); rt.SetParent(transform, false); },
                actionOnDestroy: rt => Destroy(rt.gameObject),
                defaultCapacity: 64);

            _buttonPool ??= new ObjectPool<Button>(
                createFunc: () => Instantiate(shiftButtonPrefab),
                actionOnGet: b => b.gameObject.SetActive(true),
                actionOnRelease: b => { b.onClick.RemoveAllListeners(); b.gameObject.SetActive(false); b.transform.SetParent(transform, false); },
                actionOnDestroy: b => Destroy(b.gameObject),
                defaultCapacity: 32);
        }

        public void Build(int rows, int cols)
        {
            EnsurePools();
            Clear();

            // ---- Size the Grid root from grid size, cell size, spacing, and
            //      padding. Grid is center-anchored/non-stretch; its stretch-
            //      anchored children (panel, enemy layer, tracks) follow it.
            //      Button tracks live on the Grid's outer rims, so thickness
            //      is not added here.
            Vector2 contentSize = metrics.GridPixelSize(rows, cols);
            gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                contentSize.x + padding.left + padding.right);
            gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                contentSize.y + padding.top + padding.bottom);

            // Make sure stretched child rects reflect the new root size
            // before we measure them.
            Canvas.ForceUpdateCanvases();

            var panelRT = (RectTransform)gridPanel.transform;
            Rect panelRect = panelRT.rect;

            // ---- Grid Panel: configure layout only, never resize it ----
            ConfigureGridGroup(gridPanel, metrics.CellSize, metrics.CellSize, cols, TextAnchor.MiddleCenter, padding);

            for (int i = 0; i < rows * cols; i++)
            {
                var cell = _cellPool.Get();
                cell.SetParent(gridPanel.transform, false);
                _activeCells.Add(cell);
            }

            // ---- Enemy Layer: never resized/moved; compute where the
            //      centered cell content sits inside its rect so enemy
            //      pieces line up with the cells. Assumes Enemy Layer and
            //      Grid Panel share the same rect (both fully stretched
            //      within the same parent).
            Rect layerRect = enemyLayer.rect;
            metrics.ContentOffset = new Vector2(
                padding.left + (layerRect.width  - padding.left - padding.right  - contentSize.x) * 0.5f,
                padding.top  + (layerRect.height - padding.top  - padding.bottom - contentSize.y) * 0.5f);

            // ---- Tracks: only the cross axis gets a thickness; the
            //      stretched axis is left entirely alone. Content is
            //      centered so buttons align with the centered cells.

            RectOffset horizontalPadding = new RectOffset(padding.left, padding.right, 0, 0);

            BuildTrack(leftTrack, rows, 1, 
                        new Vector2(buttonThickness, metrics.CellSize), horizontalPadding,
                        i => ("◀", (Action)(() => OnShiftButtonPressed?.Invoke(true, i, -1))));
            BuildTrack(rightTrack, rows, 1, 
                        new Vector2(buttonThickness, metrics.CellSize), horizontalPadding,
                        i => ("▶", (Action)(() => OnShiftButtonPressed?.Invoke(true, i, +1))));

            RectOffset verticalPadding = new RectOffset(0, 0, padding.top, padding.bottom);

            BuildTrack(topTrack, 1, cols, 
                        new Vector2(metrics.CellSize, buttonThickness), verticalPadding,
                        i => ("▲", (Action)(() => OnShiftButtonPressed?.Invoke(false, i, -1))));
            BuildTrack(bottomTrack, 1, cols, 
                        new Vector2(metrics.CellSize, buttonThickness), verticalPadding,
                        i => ("▼", (Action)(() => OnShiftButtonPressed?.Invoke(false, i, +1))));

            SetTrackCrossAxisThickness((RectTransform)leftTrack.transform,   RectTransform.Axis.Horizontal);
            SetTrackCrossAxisThickness((RectTransform)rightTrack.transform,  RectTransform.Axis.Horizontal);
            SetTrackCrossAxisThickness((RectTransform)topTrack.transform,    RectTransform.Axis.Vertical);
            SetTrackCrossAxisThickness((RectTransform)bottomTrack.transform, RectTransform.Axis.Vertical);
        }

        /// Sets the size of ONLY the non-stretched axis. For a stretch-
        /// anchored axis Unity stores size as an offset, so we must never
        /// write sizeDelta wholesale — SetSizeWithCurrentAnchors on the
        /// single cross axis leaves the stretched axis untouched.
        private void SetTrackCrossAxisThickness(RectTransform rt, RectTransform.Axis crossAxis)
        {
            rt.SetSizeWithCurrentAnchors(crossAxis, buttonThickness);
        }

        private void BuildTrack(GridLayoutGroup track, int rows, int cols, Vector2 buttonSize, RectOffset padding,
            Func<int, (string label, Action onClick)> configure)
        {
            ConfigureGridGroup(track, buttonSize.x, buttonSize.y, cols, TextAnchor.MiddleCenter, padding);
            int count = rows * cols;
            for (int i = 0; i < count; i++)
            {
                var btn = _buttonPool.Get();
                btn.transform.SetParent(track.transform, false);
                var (label, onClick) = configure(i);
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null) tmp.text = label;
                btn.onClick.AddListener(() => onClick());
                _activeButtons.Add(btn);
            }
        }

        private void ConfigureGridGroup(GridLayoutGroup group, float cellW, float cellH,
            int colsConstraint, TextAnchor alignment, RectOffset padding)
        {
            group.cellSize = new Vector2(cellW, cellH);
            group.spacing = new Vector2(metrics.Spacing, metrics.Spacing);
            group.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            group.constraintCount = colsConstraint;
            group.startCorner = GridLayoutGroup.Corner.UpperLeft;
            group.startAxis = GridLayoutGroup.Axis.Horizontal;
            group.childAlignment = alignment;
            group.padding = padding;
        }

        public void Clear()
        {
            foreach (var cell in _activeCells) _cellPool.Release(cell);
            _activeCells.Clear();
            foreach (var btn in _activeButtons) _buttonPool.Release(btn);
            _activeButtons.Clear();
        }

        public void SetButtonsInteractable(bool interactable)
        {
            foreach (var btn in _activeButtons) btn.interactable = interactable;
        }
    }
}
