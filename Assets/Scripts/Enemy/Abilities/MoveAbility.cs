using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Ported from the HTML game's generic movement:
    ///  - maxSteps == 1: shuffled 8-direction one-step move with wrap-aware
    ///    footprint checks (moveEnemiesOneStepSequenced's fallback branch).
    ///  - maxSteps >= 2: straight-line move up to maxSteps in the direction
    ///    whose landing spot has the most open cardinal room, in-bounds only
    ///    (moveSpriteUpToDistance).
    /// The move happens only when the chance roll passes.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Move")]
    public class MoveAbility : EnemyAbility
    {
        [Header("Movement")]
        [Tooltip("Chance to move at all this phase (JS generic move: 0.45, sprite-style: 0.55).")]
        [SerializeField, Range(0f, 1f)] private float moveChance = 0.45f;
        [Tooltip("Maximum cells moved in one direction. 1 = classic random step; 2+ = straight-line strider.")]
        [SerializeField, Min(1)] private int maxSteps = 1;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset on the enemy piece, played (and awaited) after moving.")]
        [SerializeField] private string moveAnimationPreset = "";

        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(-1,-1), new Vector2Int(-1,0), new Vector2Int(-1,1),
            new Vector2Int(0,-1),                        new Vector2Int(0,1),
            new Vector2Int(1,-1),  new Vector2Int(1,0),  new Vector2Int(1,1),
        };

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            if (Random.value >= moveChance) yield break;

            bool moved = maxSteps <= 1 ? TryStepOnce(ctx) : TryStride(ctx);
            if (!moved) yield break;

            result.Success = true;
            yield return ctx.PlayOwnerPresetAndWait(moveAnimationPreset);
        }

        // ---- One random wrap-aware step (any of 8 directions) ----
        private bool TryStepOnce(EnemyAbilityContext ctx)
        {
            var s = ctx.State;
            var en = ctx.Owner;
            foreach (var dir in Directions.OrderBy(_ => Random.value))
            {
                int nr = s.Wrap(en.Anchor.x + dir.x, s.Rows);
                int nc = s.Wrap(en.Anchor.y + dir.y, s.Cols);
                if (!s.CanPlaceAtIgnoring(nr, nc, en.SizeRows, en.SizeCols, en.Id)) continue;
                s.MoveEnemy(en.Id, new Vector2Int(nr, nc));
                return true;
            }
            return false;
        }

        // ---- Straight line up to maxSteps, in-bounds, most-open landing ----
        private bool TryStride(EnemyAbilityContext ctx)
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
                    bool inBounds = r >= 0 && r + en.SizeRows <= s.Rows &&
                                    c >= 0 && c + en.SizeCols <= s.Cols;
                    if (!inBounds || !s.CanPlaceAtIgnoring(r, c, en.SizeRows, en.SizeCols, en.Id)) break;
                    best = new Vector2Int(r, c);
                    found = true;
                }
                if (found) candidates.Add((best, CountOpenCardinals(s, best, en)));
            }
            if (candidates.Count == 0) return false;

            int maxRoom = candidates.Max(c => c.room);
            var top = candidates.Where(c => c.room == maxRoom).ToList();
            var choice = top[Random.Range(0, top.Count)];
            s.MoveEnemy(en.Id, choice.dest);
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
