using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using DG.Tweening;

namespace SlidingSiege
{
    /// Translates GridState into pieces under the masked Enemy Layer, and
    /// animates shifts by diffing old vs new anchors (backend updates first,
    /// visuals catch up). Uses Unity's built-in ObjectPool for piece Images.
    public class EnemyViewManager : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RectTransform enemyLayer;   // masked Image layer
        [SerializeField] private EnemyPieceView enemyPiecePrefab; // structured piece prefab (sprite + health bar)
        [Tooltip("Fade-out duration when an enemy is removed/killed. Swap HandleRemoved for richer death animations later.")]
        [SerializeField, Min(0f)] private float removalFadeDuration = 0.25f;

        private GridState _state;
        private GridLayoutMetrics _metrics;
        private IMoveAnimator _animator;

        private readonly Dictionary<int, EnemyView> _views = new Dictionary<int, EnemyView>();
        private ObjectPool<EnemyPieceView> _piecePool;

        public bool IsAnimating { get; private set; }

        public void Initialize(GridState state, GridLayoutMetrics metrics, IMoveAnimator animator)
        {
            _state = state;
            _metrics = metrics;
            _animator = animator;

            _piecePool ??= new ObjectPool<EnemyPieceView>(
                createFunc: () => Instantiate(enemyPiecePrefab, enemyLayer),
                actionOnGet: piece =>
                {
                    piece.gameObject.SetActive(true);
                    piece.transform.SetParent(enemyLayer, false);
                    piece.CanvasGroup.DOKill();
                    piece.CanvasGroup.alpha = 1f; // undo any removal fade
                },
                actionOnRelease: piece => piece.gameObject.SetActive(false),
                actionOnDestroy: piece => Destroy(piece.gameObject),
                defaultCapacity: 16);

            _state.OnEnemySpawned += HandleSpawned;
            _state.OnEnemyRemoved += HandleRemoved;
            _state.OnEnemyMoved += HandleMoved;
            _state.OnRebuilt += HandleRebuilt;

            HandleRebuilt();
        }

        private void OnDestroy()
        {
            if (_state == null) return;
            _state.OnEnemySpawned -= HandleSpawned;
            _state.OnEnemyRemoved -= HandleRemoved;
            _state.OnEnemyMoved -= HandleMoved;
            _state.OnRebuilt -= HandleRebuilt;
        }

        // ---------------- Piece placement math ----------------

        /// Anchored position (top-left pivot, top-left anchor) of a footprint
        /// whose top-left cell is at unwrapped (r, c). r/c may be negative.
        private Vector2 PiecePos(int r, int c) => new Vector2(
            _metrics.ContentOffset.x + c * _metrics.Stride,
            -(_metrics.ContentOffset.y + r * _metrics.Stride));

        private Vector2 FootprintSizePx(Enemy en) => new Vector2(
            en.SizeCols * _metrics.CellSize + (en.SizeCols - 1) * _metrics.Spacing,
            en.SizeRows * _metrics.CellSize + (en.SizeRows - 1) * _metrics.Spacing);

        /// Unwrapped top-left origins for every piece needed to display the
        /// enemy at a given anchor: candidates are anchor offset by 0/-Rows on
        /// the row axis and 0/-Cols on the column axis, kept if the piece's
        /// rect actually overlaps the grid viewport.
        private List<Vector2Int> PieceOrigins(Enemy en, Vector2Int anchor)
        {
            var origins = new List<Vector2Int>(4);
            int[] rowOpts = anchor.x + en.SizeRows > _state.Rows
                ? new[] { anchor.x, anchor.x - _state.Rows } : new[] { anchor.x };
            int[] colOpts = anchor.y + en.SizeCols > _state.Cols
                ? new[] { anchor.y, anchor.y - _state.Cols } : new[] { anchor.y };
            foreach (var r in rowOpts)
                foreach (var c in colOpts)
                    origins.Add(new Vector2Int(r, c));
            return origins;
        }

        private bool OverlapsGrid(Enemy en, Vector2Int origin)
        {
            // Rows: piece covers [origin.x, origin.x + SizeRows), grid covers [0, Rows)
            bool rows = origin.x < _state.Rows && origin.x + en.SizeRows > 0;
            bool cols = origin.y < _state.Cols && origin.y + en.SizeCols > 0;
            return rows && cols;
        }

        // ---------------- Static (resting) rendering ----------------

        private void RebuildEnemyPieces(Enemy en)
        {
            if (!_views.TryGetValue(en.Id, out var view)) return;
            var origins = PieceOrigins(en, en.Anchor);
            origins.RemoveAll(o => !OverlapsGrid(en, o));
            view.EnsurePieceCount(origins.Count, en, FootprintSizePx(en));
            var positions = new List<Vector2>(origins.Count);
            foreach (var o in origins) positions.Add(PiecePos(o.x, o.y));
            view.SnapPieces(positions);
        }

        public void RebuildAll()
        {
            foreach (var en in _state.Enemies.Values)
            {
                if (!_views.ContainsKey(en.Id)) HandleSpawned(en);
                RebuildEnemyPieces(en);
            }
        }

        // ---------------- Shift animation ----------------

        /// Call AFTER the backend shift. Old anchors come from the result;
        /// pieces are laid out for the union of (start pieces) and (end
        /// pieces pre-translated back by one step), then everything tweens by
        /// one stride and snaps to the final resting layout.
        public void AnimateShift(ShiftResult result, Action onComplete)
        {
            IsAnimating = true;

            Vector2 delta = result.IsRowShift
                ? new Vector2(result.Direction * _metrics.Stride, 0f)
                : new Vector2(0f, -result.Direction * _metrics.Stride);

            Vector2Int stepBack = result.IsRowShift
                ? new Vector2Int(0, -result.Direction)
                : new Vector2Int(-result.Direction, 0);

            var movingRects = new List<RectTransform>();

            foreach (var id in result.MovedEnemyIds)
            {
                if (!_state.Enemies.TryGetValue(id, out var en)) continue;
                if (!_views.TryGetValue(id, out var view)) continue;

                Vector2Int oldAnchor = result.OldAnchors[id];
                Vector2Int newAnchor = en.Anchor;

                // Union of piece origins: as displayed at the old anchor, plus
                // the new-anchor pieces translated one step back (so that after
                // moving by `delta` they land exactly on the new layout).
                var originSet = new HashSet<Vector2Int>();
                foreach (var o in PieceOrigins(en, oldAnchor))
                    if (OverlapsGrid(en, o)) originSet.Add(o);
                foreach (var o in PieceOrigins(en, newAnchor))
                {
                    var pre = o + stepBack;
                    // Include even if only visible at the END of the move.
                    if (OverlapsGrid(en, o) ) originSet.Add(pre);
                }

                var origins = new List<Vector2Int>(originSet);
                view.EnsurePieceCount(origins.Count, en, FootprintSizePx(en));
                var positions = new List<Vector2>(origins.Count);
                foreach (var o in origins) positions.Add(PiecePos(o.x, o.y));
                view.SnapPieces(positions);

                movingRects.AddRange(view.PieceRects());
            }

            _animator.AnimateShift(movingRects, delta, () =>
            {
                // Snap to canonical resting layout (drops transient ghosts,
                // keeps persistent wrap ghosts).
                foreach (var id in result.MovedEnemyIds)
                    if (_state.Enemies.TryGetValue(id, out var en))
                        RebuildEnemyPieces(en);
                IsAnimating = false;
                onComplete?.Invoke();
            });
        }

        // ---------------- Drag preview nudge ----------------

        /// Tweens the given enemies' pieces to a small offset from their
        /// resting positions (drag shift preview). Others are left alone.
        public void NudgeEnemies(ISet<int> enemyIds, Vector2 offset, float duration)
        {
            foreach (var kv in _views)
                if (enemyIds.Contains(kv.Key))
                    kv.Value.NudgeTo(offset, duration);
        }

        /// Tweens every enemy's pieces back to their resting positions.
        public void ClearNudges(float duration)
        {
            foreach (var view in _views.Values)
                view.NudgeTo(Vector2.zero, duration);
        }

        // ---------------- Spawn / despawn / rebuild ----------------

        private void HandleSpawned(Enemy en)
        {
            var view = new EnemyView();
            view.Bind(en.Id, () => _piecePool.Get(), img => _piecePool.Release(img));
            _views[en.Id] = view;
            RebuildEnemyPieces(en);
        }

        /// Death/removal is animated (simple DOTween fade for now) before
        /// the pieces are released — the extension point for custom death
        /// animations. The view leaves the dictionary immediately so game
        /// logic never sees a half-removed enemy.
        private void HandleRemoved(Enemy en)
        {
            if (!_views.TryGetValue(en.Id, out var view)) return;
            _views.Remove(en.Id);

            if (removalFadeDuration <= 0f) { view.ReleaseAll(); return; }

            int pending = 0;
            foreach (var piece in view.Pieces)
            {
                pending++;
                piece.RectTransform.DOKill();
                piece.CanvasGroup.DOKill();
                piece.CanvasGroup.DOFade(0f, removalFadeDuration).OnComplete(() =>
                {
                    if (--pending == 0) view.ReleaseAll();
                });
            }
            if (pending == 0) view.ReleaseAll();
        }

        /// Snap for now; item/enemy movement animations can hook here later.
        private void HandleMoved(Enemy en, Vector2Int oldAnchor) => RebuildEnemyPieces(en);

        private void HandleRebuilt()
        {
            foreach (var view in _views.Values) view.ReleaseAll();
            _views.Clear();
            foreach (var en in _state.Enemies.Values) HandleSpawned(en);
        }
    }
}
