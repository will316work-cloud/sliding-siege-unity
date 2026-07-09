using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Per-item validate/preview/apply logic (the Unity analogue of the
    /// HTML game's ITEM_EFFECT_RESOLVERS + ITEM_PREVIEW_CELL_RESOLVERS).
    /// The definition carries the item's Hitbox: shape-based effects resolve
    /// it directly; procedural previews use its parts as appearance carriers.
    public interface IItemEffect
    {
        ItemTargeting Targeting { get; }

        /// Cells to highlight for the current targeting selection, each with
        /// the hitbox part that styles it.
        List<HitCell> PreviewCells(GridState state, ItemDefinition def, Vector2Int? firstCell, Vector2Int? secondCell);

        /// True if the current selection is complete and valid to confirm.
        bool CanApply(GridState state, ItemDefinition def, CombatSystem combat, Vector2Int? firstCell, Vector2Int? secondCell);

        /// Applies the effect. Returns false (with a message) if it bailed.
        bool Apply(GridState state, ItemDefinition def, CombatSystem combat, Vector2Int? firstCell, Vector2Int? secondCell, out string message);
    }
}
