using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Pulls every enemy within the item's hitbox toward the chosen center
    /// tile, nearest enemies settling first (greedy stepping; simplified
    /// from the JS settle logic — swap in richer movement/animation later).
    public class GravityOrbEffect : IItemEffect
    {
        public ItemTargeting Targeting => ItemTargeting.Cell;

        public List<HitCell> PreviewCells(GridState s, ItemDefinition def, Vector2Int? first, Vector2Int? b)
        {
            if (first == null || def.Hitbox == null) return new List<HitCell>();
            return def.Hitbox.Resolve(s, first.Value);
        }

        public bool CanApply(GridState s, ItemDefinition def, CombatSystem combat, Vector2Int? first, Vector2Int? b) => first != null;

        public bool Apply(GridState s, ItemDefinition def, CombatSystem combat, Vector2Int? first, Vector2Int? b, out string message)
        {
            if (first == null) { message = "Pick a tile to pull enemies toward."; return false; }
            Vector2Int center = first.Value;

            var affected = new List<Enemy>();
            foreach (var hit in PreviewCells(s, def, first, null))
                foreach (var en in s.EnemiesAt(hit.Cell.x, hit.Cell.y))
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
                if (s.CanPlaceBodyAtIgnoring(wrapped.x, wrapped.y, en.BodyCells, en.Id))
                {
                    s.MoveEnemy(en.Id, wrapped, MoveStyle.Slide);
                    return true;
                }
            }
            return false;
        }
    }
}
