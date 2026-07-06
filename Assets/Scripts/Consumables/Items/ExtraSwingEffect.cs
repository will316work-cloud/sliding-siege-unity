using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Grants one additional attack use this turn. No targeting.
    public class ExtraSwingEffect : IItemEffect
    {
        public ItemTargeting Targeting => ItemTargeting.None;

        public List<Vector2Int> PreviewCells(GridState s, Vector2Int? a, Vector2Int? b) => new List<Vector2Int>();

        public bool CanApply(GridState s, CombatSystem combat, Vector2Int? a, Vector2Int? b) => true;

        public bool Apply(GridState s, CombatSystem combat, Vector2Int? a, Vector2Int? b, out string message)
        {
            combat.AttacksRemaining++;
            message = "Extra Swing: +1 attack use this turn!";
            return true;
        }
    }
}
