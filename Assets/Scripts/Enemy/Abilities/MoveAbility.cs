using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

namespace SlidingSiege
{
    /// Ported from the HTML game's generic movement:
    ///  - Steps, maxSteps == 1: shuffled 8-direction one-step move.
    ///  - Steps, maxSteps >= 2: straight-line move up to maxSteps in the
    ///    direction whose landing spot has the most open cardinal room.
    ///  - Anywhere: a uniformly random valid anchor cell (the old
    ///    Teleport Self behavior), sliding there like any other move.
    /// Landing rules are per-asset: allowOverlap skips the occupancy check
    /// (transparent enemies may stack), wrapAroundGrid lets steps/strides
    /// cross edges and lets footprints hang past them.
    /// On a sliding move, the wrap-split DOTween slide and the Move
    /// animation preset run IN PARALLEL over the same moveDuration: the
    /// preset's speed is scaled by referenceClipLength / moveDuration so
    /// both finish together. The ability waits for both before its
    /// post-delay applies.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Move")]
    public class MoveAbility : EnemyAbility
    {
        public enum RangeMode { Steps, Anywhere }

        [Header("Movement")]
        [Tooltip("Chance to move at all this phase (JS generic move: 0.45, sprite-style: 0.55).")]
        [SerializeField, Range(0f, 1f)] private float moveChance = 0.45f;
        [Tooltip("Steps: walk up to Max Steps in one direction. Anywhere: jump to any valid cell on the grid.")]
        [SerializeField] private RangeMode range = RangeMode.Steps;
        [Tooltip("Steps only. Maximum cells moved in one direction. 1 = classic random step; 2+ = straight-line strider.")]
        [SerializeField, Min(1)] private int maxSteps = 1;
        [Tooltip("On: may land on (stack with) occupied cells. Off: only fully empty footprints.")]
        [SerializeField] private bool allowOverlap = false;
        [Tooltip("On: steps/strides wrap around the grid edges and footprints may hang past them. Off: the whole footprint stays in bounds.")]
        [SerializeField] private bool wrapAroundGrid = true;

        [Header("Timing")]
        [Tooltip("Seconds the DOTween slide AND the Move animation both take, per move.")]
        [SerializeField, Min(0.01f)] private float moveDuration = 0.25f;
        [SerializeField] private Ease shiftEase = Ease.OutCubic;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset on the enemy piece, played alongside the slide. The state must bind its layer's speed parameter for the time-fitting to work.")]
        [SerializeField] private string moveAnimationPreset = "Move";
        [Tooltip("Authored length (seconds) of the Move clip at speed 1; the preset is played at referenceClipLength / moveDuration so it fits the slide.")]
        [SerializeField, Min(0.01f)] private float referenceClipLength = 1f;

        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(-1,-1), new Vector2Int(-1,0), new Vector2Int(-1,1),
            new Vector2Int(0,-1),                        new Vector2Int(0,1),
            new Vector2Int(1,-1),  new Vector2Int(1,0),  new Vector2Int(1,1),
        };

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            if (ctx.Owner == null || Random.value >= moveChance) yield break;

            Vector2Int destination;
            bool found = range == RangeMode.Anywhere ? TryPickAnywhere(ctx, out destination)
                : maxSteps <= 1 ? TryPickStep(ctx, out destination)
                : TryPickStride(ctx, out destination);
            if (!found) yield break;

            result.Success = true;

            // Slide and animation in parallel, both moveDuration long.
            bool slideDone = false;
            ctx.Views.MoveEnemySlide(ctx.Owner, destination, moveDuration, shiftEase, () => slideDone = true, wrapAroundGrid);

            bool animDone = string.IsNullOrEmpty(moveAnimationPreset);
            if (!animDone)
                ctx.Host.StartCoroutine(PlayAnim(ctx, () => animDone = true));

            while (!slideDone || !animDone) yield return null;
        }

        private IEnumerator PlayAnim(EnemyAbilityContext ctx, System.Action onDone)
        {
            float speedScale = 1 / referenceClipLength;
            yield return ctx.PlayOwnerPresetAndWait(moveAnimationPreset, speedScale);
            onDone();
        }

        /// True if the enemy's footprint may land with its anchor at (r, c),
        /// honoring the wrap and overlap options.
        private bool CanLand(GridState s, Enemy en, int r, int c)
        {
            if (!wrapAroundGrid)
            {
                var min = en.BodyMin;
                bool inBounds = r + min.x >= 0 && r + min.x + en.SizeRows <= s.Rows &&
                                c + min.y >= 0 && c + min.y + en.SizeCols <= s.Cols;
                if (!inBounds) return false;
            }
            return allowOverlap || s.CanPlaceBodyAtIgnoring(r, c, en.BodyCells, en.Id);
        }

        // ---- One random step (any of 8 directions) ----
        private bool TryPickStep(EnemyAbilityContext ctx, out Vector2Int destination)
        {
            var s = ctx.State;
            var en = ctx.Owner;
            foreach (var dir in Directions.OrderBy(_ => Random.value))
            {
                int nr = en.Anchor.x + dir.x;
                int nc = en.Anchor.y + dir.y;
                if (wrapAroundGrid) { nr = s.Wrap(nr, s.Rows); nc = s.Wrap(nc, s.Cols); }
                if (!CanLand(s, en, nr, nc)) continue;
                destination = new Vector2Int(nr, nc);
                return true;
            }
            destination = default;
            return false;
        }

        // ---- Straight line up to maxSteps, most-open landing ----
        private bool TryPickStride(EnemyAbilityContext ctx, out Vector2Int destination)
        {
            var s = ctx.State;
            var en = ctx.Owner;
            var candidates = new List<(Vector2Int dest, int room)>();

            foreach (var dir in Directions)
            {
                Vector2Int best = en.Anchor;
                bool found = false;
                for (int dist = 1; dist <= maxSteps; dist++)
                {
                    int r = en.Anchor.x + dir.x * dist;
                    int c = en.Anchor.y + dir.y * dist;
                    if (wrapAroundGrid) { r = s.Wrap(r, s.Rows); c = s.Wrap(c, s.Cols); }
                    if (!CanLand(s, en, r, c)) break;
                    best = new Vector2Int(r, c);
                    found = true;
                }
                if (found) candidates.Add((best, CountOpenCardinals(s, best, en)));
            }
            if (candidates.Count == 0) { destination = default; return false; }

            int maxRoom = candidates.Max(c => c.room);
            var top = candidates.Where(c => c.room == maxRoom).ToList();
            destination = top[Random.Range(0, top.Count)].dest;
            return true;
        }

        // ---- Any valid anchor on the grid (old Teleport Self) ----
        private bool TryPickAnywhere(EnemyAbilityContext ctx, out Vector2Int destination)
        {
            var s = ctx.State;
            var en = ctx.Owner;
            var candidates = new List<Vector2Int>();
            for (int r = 0; r < s.Rows; r++)
                for (int c = 0; c < s.Cols; c++)
                {
                    var cell = new Vector2Int(r, c);
                    if (cell == en.Anchor) continue; // staying put isn't a move
                    if (CanLand(s, en, r, c)) candidates.Add(cell);
                }
            if (candidates.Count == 0) { destination = default; return false; }
            destination = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        private static int CountOpenCardinals(GridState s, Vector2Int anchor, Enemy en)
        {
            var dirs = new[] { new Vector2Int(-1,0), new Vector2Int(1,0), new Vector2Int(0,-1), new Vector2Int(0,1) };
            int count = 0;
            foreach (var d in dirs)
            {
                int r = anchor.x + d.x, c = anchor.y + d.y;
                if (r < 0 || r >= s.Rows || c < 0 || c >= s.Cols) continue;
                bool blocked = false;
                foreach (var e2 in s.EnemiesAt(r, c))
                    if (e2.Id != en.Id) { blocked = true; break; }
                if (!blocked) count++;
            }
            return count;
        }
    }
}
