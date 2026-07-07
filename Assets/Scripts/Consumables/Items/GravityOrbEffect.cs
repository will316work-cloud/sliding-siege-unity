using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Pulls every enemy within a 5x5 area toward the chosen center tile,
    /// nearest enemies settling first (greedy stepping; simplified from the
    /// JS settle logic — swap in richer movement/animation later).
    public class GravityOrbEffect : IItemEffect
    {
        public ItemTargeting Targeting => ItemTargeting.Cell;

        public List<Vector2Int> PreviewCells(GridState s, Vector2Int? first, Vector2Int? b)
        {
            var cells = new List<Vector2Int>();
            if (first == null) return cells;
            for (int dr = -2; dr <= 2; dr++)
                for (int dc = -2; dc <= 2; dc++)
                {
                    int r = first.Value.x + dr, c = first.Value.y + dc;
                    if (r >= 0 && r < s.Rows && c >= 0 && c < s.Cols) cells.Add(new Vector2Int(r, c));
                }
            return cells;
        }

        public bool CanApply(GridState s, CombatSystem combat, Vector2Int? first, Vector2Int? b) => first != null;

        public bool Apply(GridState s, CombatSystem combat, Vector2Int? first, Vector2Int? b, out string message)
        {
            if (first == null) { message = "Pick a tile to pull enemies toward."; return false; }
            Vector2Int center = first.Value;

            var affected = new List<Enemy>();
            foreach (var cell in PreviewCells(s, first, null))
                foreach (var en in s.EnemiesAt(cell.x, cell.y))
                    if (!affected.Contains(en)) affected.Add(en);

            affected = affected
                .OrderBy(en => Vector2Int.Distance(en.Anchor, center))
                .ToList();

            bool moved = true;
            int guard = 0;
            while (moved && guard++ < 64)
            {
                moved = false;
                foreach (var en in affected)
                    if (StepToward(s, en, center)) moved = true;
            }

            message = "Gravity Orb: enemies pulled in!";
            return true;
        }

        private static bool StepToward(GridState s, Enemy en, Vector2Int center)
        {
            int dr = Mathf.Clamp(center.x - en.Anchor.x, -1, 1);
            int dc = Mathf.Clamp(center.y - en.Anchor.y, -1, 1);
            if (dr == 0 && dc == 0) return false;

            // Try the dominant axis first, then the other, then the diagonal.
            var candidates = new List<Vector2Int>();
            bool rowDominant = Mathf.Abs(center.x - en.Anchor.x) >= Mathf.Abs(center.y - en.Anchor.y);
            if (rowDominant)
            {
                if (dr != 0) candidates.Add(en.Anchor + new Vector2Int(dr, 0));
                if (dc != 0) candidates.Add(en.Anchor + new Vector2Int(0, dc));
            }
            else
            {
                if (dc != 0) candidates.Add(en.Anchor + new Vector2Int(0, dc));
                if (dr != 0) candidates.Add(en.Anchor + new Vector2Int(dr, 0));
            }
            if (dr != 0 && dc != 0) candidates.Add(en.Anchor + new Vector2Int(dr, dc));

            foreach (var dest in candidates)
            {
                var wrapped = new Vector2Int(s.Wrap(dest.x, s.Rows), s.Wrap(dest.y, s.Cols));
                if (wrapped == en.Anchor) continue;
                if (s.CanPlaceAtIgnoring(wrapped.x, wrapped.y, en.SizeRows, en.SizeCols, en.Id))
                {
                    s.MoveEnemy(en.Id, wrapped, MoveStyle.Slide);
                    return true;
                }
            }
            return false;
        }
    }
}
