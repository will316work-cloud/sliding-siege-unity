using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Moves a chosen enemy to a chosen destination anchor (footprint must
    /// fit; wraps allowed, matching the JS preview's wrapped footprint).
    /// The preview is procedural (source + destination footprints); the item
    /// hitbox's first part supplies the highlight appearance.
    public class TeleportEffect : IItemEffect
    {
        public ItemTargeting Targeting => ItemTargeting.EnemyThenCell;

        public List<HitCell> PreviewCells(GridState s, ItemDefinition def, Vector2Int? first, Vector2Int? second)
        {
            var cells = new List<HitCell>();
            if (first == null) return cells;
            var en = s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault();
            if (en == null) return cells;
            var part = def.Hitbox?.FirstPart;

            void AddFootprint(Vector2Int anchor)
            {
                foreach (var off in en.BodyCells)
                    cells.Add(new HitCell(new Vector2Int(s.Wrap(anchor.x + off.x, s.Rows), s.Wrap(anchor.y + off.y, s.Cols)), part));
            }

            AddFootprint(en.Anchor);
            if (second != null) AddFootprint(second.Value);
            return cells;
        }

        public bool CanApply(GridState s, ItemDefinition def, CombatSystem combat, Vector2Int? first, Vector2Int? second)
        {
            if (first == null || second == null) return false;
            var en = s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault();
            return en != null && s.CanPlaceBodyAtIgnoring(second.Value.x, second.Value.y, en.BodyCells, en.Id);
        }

        public bool Apply(GridState s, ItemDefinition def, CombatSystem combat, Vector2Int? first, Vector2Int? second, out string message)
        {
            var en = first != null ? s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault() : null;
            if (en == null) { message = "No enemy on that tile."; return false; }
            if (second == null) { message = "Pick a destination."; return false; }
            if (!s.CanPlaceBodyAtIgnoring(second.Value.x, second.Value.y, en.BodyCells, en.Id))
            { message = "It doesn't fit there."; return false; }
            s.MoveEnemy(en.Id, second.Value);
            message = "Teleported!";
            return true;
        }
    }
}
