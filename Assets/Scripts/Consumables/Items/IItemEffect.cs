using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Per-item validate/preview/apply logic (the Unity analogue of the
    /// HTML game's ITEM_EFFECT_RESOLVERS + ITEM_PREVIEW_CELL_RESOLVERS).
    public interface IItemEffect
    {
        ItemTargeting Targeting { get; }

        /// Cells to highlight for the current targeting selection.
        List<Vector2Int> PreviewCells(GridState state, Vector2Int? firstCell, Vector2Int? secondCell);

        /// True if the current selection is complete and valid to confirm.
        bool CanApply(GridState state, CombatSystem combat, Vector2Int? firstCell, Vector2Int? secondCell);

        /// Applies the effect. Returns false (with a message) if it bailed.
        bool Apply(GridState state, CombatSystem combat, Vector2Int? firstCell, Vector2Int? secondCell, out string message);
    }
}
