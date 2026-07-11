using System;
using System.Collections;
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
        [Tooltip("Fallback fade-out duration for pieces WITHOUT an AnimationCaller when the enemy is removed.")]
        [SerializeField, Min(0f)] private float removalFadeDuration = 0.25f;

        [Header("Enemy move slide (MoveStyle.Slide default; MoveAbility can override per-asset)")]
        [SerializeField, Min(0.01f)] private float defaultMoveDuration = 0.25f;
        [SerializeField] private Ease defaultMoveEase = Ease.OutCubic;

        [Header("Animation preset labels (played on each piece's AnimationCaller)")]
        [SerializeField] private string spawnPresetLabel = "Spawn";
        [SerializeField] private string hurtPresetLabel = "Hurt";
        [SerializeField] private string deathPresetLabel = "Death";

        private GridState _state;
        private GridLayoutMetrics _metrics;
        private IMoveAnimator _animator;

        private readonly Dictionary<int, EnemyView> _views = new Dictionary<int, EnemyView>();
        private ObjectPool<EnemyPieceView> _piecePool;

        private bool _shiftAnimating;
        private int _pendingRemovals;
        private int _pendingMoves;

        /// True while a shift tween, an enemy move slide, or any death
        /// sequence is playing.
        public bool IsAnimating => _shiftAnimating || _pendingRemovals > 0 || _pendingMoves > 0;

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
            _state.OnEnemyResized += HandleResized;
            _state.OnRebuilt += HandleRebuilt;

            HandleRebuilt();
        }

        private void OnDestroy()
        {
            if (_state == null) return;
            _state.OnEnemySpawned -= HandleSpawned;
            _state.OnEnemyRemoved -= HandleRemoved;
            _state.OnEnemyMoved -= HandleMoved;
            _state.OnEnemyResized -= HandleResized;
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
        /// enemy at a given anchor. The visual bounding box starts at
        /// anchor + BodyMin (body cells may be negative), and wrap ghosts
        /// are added when the box hangs off either side of an axis.
        private List<Vector2Int> PieceOrigins(Enemy en, Vector2Int anchor)
        {
            var min = en.BodyMin;
            int baseR = anchor.x + min.x, baseC = anchor.y + min.y;

            var rowOpts = new List<int>(2) { baseR };
            if (baseR < 0) rowOpts.Add(baseR + _state.Rows);
            if (baseR + en.SizeRows > _state.Rows) rowOpts.Add(baseR - _state.Rows);
            var colOpts = new List<int>(2) { baseC };
            if (baseC < 0) colOpts.Add(baseC + _state.Cols);
            if (baseC + en.SizeCols > _state.Cols) colOpts.Add(baseC - _state.Cols);

            var seen = new HashSet<Vector2Int>();
            var origins = new List<Vector2Int>(4);
            foreach (var r in rowOpts)
                foreach (var c in colOpts)
                {
                    var o = new Vector2Int(r, c);
                    if (seen.Add(o)) origins.Add(o);
                }
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
            _shiftAnimating = true;

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
                _shiftAnimating = false;
                onComplete?.Invoke();
            });
        }

        /// The enemy's main piece (index 0), e.g. for playing ability
        /// animations. False if the enemy has no view or no pieces.
        public bool TryGetMainPiece(int enemyId, out EnemyPieceView piece)
        {
            if (_views.TryGetValue(enemyId, out var view) && view.Pieces.Count > 0)
            {
                piece = view.Pieces[0];
                return true;
            }
            piece = null;
            return false;
        }

        /// Current piece RectTransforms of an enemy (main + wrap ghosts),
        /// e.g. for anchoring indicators. False if the enemy has no view.
        public bool TryGetPieceRects(int enemyId, out List<RectTransform> rects)
        {
            if (_views.TryGetValue(enemyId, out var view))
            {
                rects = view.PieceRects();
                return rects.Count > 0;
            }
            rects = null;
            return false;
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

        private readonly Dictionary<int, Action<int>> _hurtHooks = new Dictionary<int, Action<int>>();

        private void HandleSpawned(Enemy en)
        {
            var view = new EnemyView();
            view.Bind(en.Id, () => _piecePool.Get(), img => _piecePool.Release(img));
            _views[en.Id] = view;
            RebuildEnemyPieces(en);

            // Hurt animation on every non-lethal hit (death handles the rest).
            Action<int> onLost = _ => { if (!en.IsDead) PlayPresetFireAndForget(view, hurtPresetLabel); };
            en.OnHealthLost += onLost;
            _hurtHooks[en.Id] = onLost;

            PlayPresetFireAndForget(view, spawnPresetLabel);
        }

        private void UnhookHurt(Enemy en)
        {
            if (_hurtHooks.TryGetValue(en.Id, out var hook))
            {
                en.OnHealthLost -= hook;
                _hurtHooks.Remove(en.Id);
            }
        }

        /// Removal plays Hurt then Death on every piece's AnimationCaller
        /// (pieces without a caller fall back to the DOTween fade during the
        /// death step), and only releases the pieces once ALL are done. The
        /// view leaves the dictionary immediately so game logic never sees
        /// a half-removed enemy; IsAnimating stays true (and gameplay waits)
        /// until every pending death sequence finishes.
        private void HandleRemoved(Enemy en)
        {
            UnhookHurt(en);
            if (!_views.TryGetValue(en.Id, out var view)) return;
            _views.Remove(en.Id);

            _pendingRemovals++;
            StartCoroutine(RemovalSequence(view));
        }

        private IEnumerator RemovalSequence(EnemyView view)
        {
            yield return PlayPresetOnAllPieces(view, hurtPresetLabel, fadeFallback: false);
            yield return PlayPresetOnAllPieces(view, deathPresetLabel, fadeFallback: true);
            view.ReleaseAll();
            _pendingRemovals--;
        }

        /// Plays a preset on every piece that has an AnimationCaller and
        /// waits for all of them to complete. Pieces without a caller do
        /// nothing — unless fadeFallback is set, in which case they fade
        /// out over removalFadeDuration instead.
        private IEnumerator PlayPresetOnAllPieces(EnemyView view, string label, bool fadeFallback)
        {
            int pending = 0;
            foreach (var piece in view.Pieces)
            {
                var caller = piece.AnimationCaller;
                if (caller != null && !string.IsNullOrEmpty(label))
                {
                    pending++;
                    caller.PlayPreset(label, () => pending--);
                }
                else if (fadeFallback && removalFadeDuration > 0f)
                {
                    pending++;
                    piece.RectTransform.DOKill();
                    piece.CanvasGroup.DOKill();
                    piece.CanvasGroup.DOFade(0f, removalFadeDuration).OnComplete(() => pending--);
                }
            }
            while (pending > 0) yield return null;
        }

        private void PlayPresetFireAndForget(EnemyView view, string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            foreach (var piece in view.Pieces)
                if (piece.AnimationCaller != null)
                    piece.AnimationCaller.PlayPreset(label);
        }

        /// Footprint size changed: re-lay the pieces at the new metrics.
        private void HandleResized(Enemy en)
        {
            if (_suppressNextResizeVisual) return;
            RebuildEnemyPieces(en);
        }

        private bool _suppressNextResizeVisual;

        /// Reshapes the enemy on the backend AND tweens each existing
        /// piece's rect from its current size/position to the new resting
        /// layout (fallback visual for shape changes without an animation
        /// preset). Newly created wrap-ghost pieces snap into place.
        public void ReshapeEnemyTweened(Enemy en, Vector2Int newAnchor, EnemyShape shapeOverride,
            float duration, Ease ease, Action onComplete)
        {
            if (!_views.TryGetValue(en.Id, out var view))
            {
                _state.ReshapeEnemy(en.Id, newAnchor, shapeOverride);
                onComplete?.Invoke();
                return;
            }

            var oldStates = new List<(Vector2 size, Vector2 pos)>(view.Pieces.Count);
            foreach (var piece in view.Pieces)
                oldStates.Add((piece.RectTransform.sizeDelta, piece.RectTransform.anchoredPosition));

            _suppressNextResizeVisual = true;
            _state.ReshapeEnemy(en.Id, newAnchor, shapeOverride);
            _suppressNextResizeVisual = false;

            var origins = PieceOrigins(en, en.Anchor);
            origins.RemoveAll(o => !OverlapsGrid(en, o));
            Vector2 fp = FootprintSizePx(en);
            view.EnsurePieceCount(origins.Count, en, fp);

            Vector2 targetSize = en.VisualSize(fp);
            Vector2 visualOffset = en.VisualAnchorOffset(fp);

            _pendingMoves++;
            int pending = 0;

            void Finish()
            {
                if (_views.ContainsKey(en.Id)) RebuildEnemyPieces(en);
                _pendingMoves--;
                onComplete?.Invoke();
            }
            void Done() { if (--pending == 0) Finish(); }

            for (int i = 0; i < view.Pieces.Count && i < origins.Count; i++)
            {
                var rt = view.Pieces[i].RectTransform;
                Vector2 targetPos = PiecePos(origins[i].x, origins[i].y) + visualOffset;
                rt.DOKill();
                if (i < oldStates.Count)
                {
                    // Re-prime to the pre-reshape rect, then grow/slide in.
                    rt.sizeDelta = oldStates[i].size;
                    rt.anchoredPosition = oldStates[i].pos;
                    pending += 2;
                    rt.DOSizeDelta(targetSize, duration).SetEase(ease).OnComplete(Done);
                    rt.DOAnchorPos(targetPos, duration).SetEase(ease).OnComplete(Done);
                }
                else
                {
                    rt.anchoredPosition = targetPos;
                }
            }
            if (pending == 0) Finish();
        }

        private void HandleMoved(Enemy en, Vector2Int oldAnchor, MoveStyle style)
        {
            if (style == MoveStyle.Slide)
                AnimateEnemyMove(en, oldAnchor, defaultMoveDuration, defaultMoveEase, null);
            else
                RebuildEnemyPieces(en);
        }

        /// Moves an enemy on the backend AND slides its pieces with the
        /// full wrap-split treatment, using a caller-supplied duration/ease
        /// (MoveAbility). The internal flag suppresses the event-driven
        /// default slide so the move isn't animated twice.
        public void MoveEnemySlide(Enemy en, Vector2Int destination, float duration, Ease ease, Action onComplete, bool wrapPath = true)
        {
            Vector2Int oldAnchor = en.Anchor;
            _suppressNextMoveVisual = true;
            _state.MoveEnemy(en.Id, destination, MoveStyle.Slide);
            _suppressNextMoveVisual = false;
            AnimateEnemyMove(en, oldAnchor, duration, ease, onComplete, wrapPath);
        }

        private bool _suppressNextMoveVisual;

        /// Wrap-split slide of a single enemy from oldAnchor to its current
        /// anchor: pieces are laid out for the union of (start layout) and
        /// (end layout stepped back by the wrapped delta), all tween by the
        /// delta, then snap to the canonical resting layout. wrapPath false
        /// forces travel across the grid interior (never over an edge).
        public void AnimateEnemyMove(Enemy en, Vector2Int oldAnchor, float duration, Ease ease, Action onComplete, bool wrapPath = true)
        {
            if (_suppressNextMoveVisual) return;
            if (!_views.TryGetValue(en.Id, out var view))
            {
                onComplete?.Invoke();
                return;
            }

            // Per-axis delta: shortest wrapped, or direct when wrapPath is off.
            int dr = en.Anchor.x - oldAnchor.x;
            int dc = en.Anchor.y - oldAnchor.y;
            if (wrapPath)
            {
                dr = ShortestDelta(dr, _state.Rows);
                dc = ShortestDelta(dc, _state.Cols);
            }
            if (dr == 0 && dc == 0)
            {
                RebuildEnemyPieces(en);
                onComplete?.Invoke();
                return;
            }

            Vector2 pixelDelta = new Vector2(dc * _metrics.Stride, -dr * _metrics.Stride);
            Vector2Int stepBack = new Vector2Int(-dr, -dc);

            var originSet = new HashSet<Vector2Int>();
            foreach (var o in PieceOrigins(en, oldAnchor))
                if (OverlapsGrid(en, o)) originSet.Add(o);
            foreach (var o in PieceOrigins(en, en.Anchor))
                if (OverlapsGrid(en, o)) originSet.Add(o + stepBack);

            var origins = new List<Vector2Int>(originSet);
            view.EnsurePieceCount(origins.Count, en, FootprintSizePx(en));
            var positions = new List<Vector2>(origins.Count);
            foreach (var o in origins) positions.Add(PiecePos(o.x, o.y));
            view.SnapPieces(positions);

            _pendingMoves++;
            int pending = 0;
            foreach (var rt in view.PieceRects())
            {
                pending++;
                rt.DOKill();
                rt.DOAnchorPos(rt.anchoredPosition + pixelDelta, duration).SetEase(ease).OnComplete(() =>
                {
                    if (--pending > 0) return;
                    if (_views.ContainsKey(en.Id)) RebuildEnemyPieces(en);
                    _pendingMoves--;
                    onComplete?.Invoke();
                });
            }
            if (pending == 0) { _pendingMoves--; onComplete?.Invoke(); }
        }

        private static int ShortestDelta(int raw, int size)
        {
            int d = ((raw % size) + size) % size;
            if (d > size / 2) d -= size;
            return d;
        }

        private void HandleRebuilt()
        {
            foreach (var en in _state.Enemies.Values) UnhookHurt(en);
            _hurtHooks.Clear();
            foreach (var view in _views.Values) view.ReleaseAll();
            _views.Clear();
            foreach (var en in _state.Enemies.Values) HandleSpawned(en);
        }
    }
}
