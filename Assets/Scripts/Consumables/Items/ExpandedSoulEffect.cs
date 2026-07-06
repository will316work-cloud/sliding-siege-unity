using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Surrounds a chosen enemy with a soul cloud for one turn: attacks that
    /// hit any of its 8-neighbor halo cells also count as hitting it.
    public class ExpandedSoulEffect : IItemEffect
    {
        public ItemTargeting Targeting => ItemTargeting.Enemy;

        public List<Vector2Int> PreviewCells(GridState s, Vector2Int? first, Vector2Int? b)
        {
            if (first == null) return new List<Vector2Int>();
            var en = s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault();
            if (en == null) return new List<Vector2Int> { first.Value };
            return CombatSystem.FootprintAndHaloCells(s, en);
        }

        public bool CanApply(GridState s, CombatSystem combat, Vector2Int? first, Vector2Int? b) =>
            first != null && s.EnemiesAt(first.Value.x, first.Value.y).Any();

        public bool Apply(GridState s, CombatSystem combat, Vector2Int? first, Vector2Int? b, out string message)
        {
            var en = first != null ? s.EnemiesAt(first.Value.x, first.Value.y).FirstOrDefault() : null;
            if (en == null) { message = "No enemy on that tile."; return false; }
            en.Statuses.Add(new SoulCloudStatus { TurnsRemaining = 1 });
            message = "Expanded Soul applied!";
            return true;
        }
    }
}
