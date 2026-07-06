using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Moves a chosen enemy to a chosen destination anchor (footprint must
    /// fit; wraps allowed, matching the JS preview's wrapped footprint).
    public class TeleportEffect : IItemEffect
    {
        public ItemTargeting Targeting => ItemTargeting.EnemyThenCell;

        public List<Vector2Int> PreviewCells(GridState s, Vector2Int? first, Vector2Int? second)
        {
            var cells = new List<Vector2Int>();
            if (first == null) return cells;
            var en = s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault();
            if (en == null) return cells;

            void AddFootprint(Vector2Int anchor)
            {
                for (int i = 0; i < en.SizeRows; i++)
                    for (int j = 0; j < en.SizeCols; j++)
                        cells.Add(new Vector2Int(s.Wrap(anchor.x + i, s.Rows), s.Wrap(anchor.y + j, s.Cols)));
            }

            AddFootprint(en.Anchor);
            if (second != null) AddFootprint(second.Value);
            return cells;
        }

        public bool CanApply(GridState s, CombatSystem combat, Vector2Int? first, Vector2Int? second)
        {
            if (first == null || second == null) return false;
            var en = s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault();
            return en != null && s.CanPlaceAtIgnoring(second.Value.x, second.Value.y, en.SizeRows, en.SizeCols, en.Id);
        }

        public bool Apply(GridState s, CombatSystem combat, Vector2Int? first, Vector2Int? second, out string message)
        {
            var en = first != null ? s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault() : null;
            if (en == null) { message = "No enemy on that tile."; return false; }
            if (second == null) { message = "Pick a destination."; return false; }
            if (!s.CanPlaceAtIgnoring(second.Value.x, second.Value.y, en.SizeRows, en.SizeCols, en.Id))
            { message = "It doesn't fit there."; return false; }
            s.MoveEnemy(en.Id, second.Value);
            message = "Teleported!";
            return true;
        }
    }
}
