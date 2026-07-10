using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Surrounds a chosen enemy with a soul cloud for one turn: attacks that
    /// hit any of its 8-neighbor halo cells also count as hitting it. The
    /// preview is procedural (footprint + halo); the item hitbox's first
    /// part supplies the highlight appearance.
    public class ExpandedSoulEffect : IItemEffect
    {
        public ItemTargeting Targeting => ItemTargeting.Enemy;

        public List<HitCell> PreviewCells(GridState s, ItemDefinition def, Vector2Int? first, Vector2Int? b)
        {
            var cells = new List<HitCell>();
            if (first == null) return cells;
            var part = def.Hitbox?.FirstPart;
            var en = s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault();
            if (en == null) { cells.Add(new HitCell(first.Value, part)); return cells; }
            foreach (var cell in CombatSystem.FootprintAndHaloCells(s, en))
                cells.Add(new HitCell(cell, part));
            return cells;
        }

        public bool CanApply(GridState s, ItemDefinition def, CombatSystem combat, Vector2Int? first, Vector2Int? b) =>
            first != null && s.EnemiesAt(first.Value.x, first.Value.y).Any();

        public bool Apply(GridState s, ItemDefinition def, CombatSystem combat, Vector2Int? first, Vector2Int? b, out string message)
        {
            var en = first != null ? s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault() : null;
            if (en == null) { message = "No enemy on that tile."; return false; }
            en.AddStatus(new SoulCloudStatus());
            message = "Expanded Soul applied!";
            return true;
        }
    }
}
