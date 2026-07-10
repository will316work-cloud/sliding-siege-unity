using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Grants one additional attack use this turn. No targeting.
    public class ExtraSwingEffect : IItemEffect
    {
        public ItemTargeting Targeting => ItemTargeting.None;

        public List<HitCell> PreviewCells(GridState s, ItemDefinition def, Vector2Int? a, Vector2Int? b) => new List<HitCell>();

        public bool CanApply(GridState s, ItemDefinition def, CombatSystem combat, Vector2Int? a, Vector2Int? b) => true;

        public bool Apply(GridState s, ItemDefinition def, CombatSystem combat, Vector2Int? a, Vector2Int? b, out string message)
        {
            combat.GrantExtraAttack();
            message = "Extra Swing: +1 attack use this turn!";
            return true;
        }
    }
}
