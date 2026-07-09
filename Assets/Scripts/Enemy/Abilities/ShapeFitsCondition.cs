using UnityEngine;

namespace SlidingSiege
{
    /// True when a candidate shape fits at the owner's anchor plus an
    /// offset (wrapped), treating the owner's own cells as free. Reference
    /// a ChangeShapeAbility to test ITS shape/offset (single source of
    /// truth; revert-mode abilities always pass); the local cells/offset
    /// below are used only when no ability is referenced.
    [CreateAssetMenu(menuName = "SlidingSiege/Conditions/Shape Fits")]
    public class ShapeFitsCondition : AbilityCondition
    {
        [Tooltip("When set, this ability's shape and anchor offset are checked and the manual fields below are ignored.")]
        [SerializeField] private ChangeShapeAbility changeShape;

        [Tooltip("On: cells past the board edge wrap around (torus) and only occupancy is checked. Off: any cell that would leave the board fails the check.")]
        [SerializeField] private bool allowWrapping = true;

        [Header("Manual shape (used when no ability is referenced)")]
        [Tooltip("Anchor shift the change would apply (x = row, y = col).")]
        [SerializeField] private Vector2Int anchorOffset;
        [Tooltip("Body cells of the candidate shape, offsets from its bounding box top-left.")]
        [SerializeField] private Vector2Int[] bodyCells = { Vector2Int.zero };

        public override bool Evaluate(EnemyAbilityContext ctx)
        {
            var en = ctx.Owner;
            if (en == null) return false;

            Vector2Int offset;
            Vector2Int[] cells;
            if (changeShape != null)
            {
                if (changeShape.RevertToDefinition) return true; // reverts re-enter own cells
                offset = changeShape.AnchorOffset;
                cells = changeShape.Shape?.BodyCells;
            }
            else
            {
                offset = anchorOffset;
                cells = bodyCells;
            }

            if (cells == null || cells.Length == 0) return false;

            var s = ctx.State;
            int ar = en.Anchor.x + offset.x, ac = en.Anchor.y + offset.y;
            if (!allowWrapping)
                foreach (var cell in cells)
                {
                    int r = ar + cell.x, c = ac + cell.y;
                    if (r < 0 || r >= s.Rows || c < 0 || c >= s.Cols) return false;
                }
            return s.CanPlaceBodyAtIgnoring(ar, ac, cells, en.Id);
        }
    }
}
