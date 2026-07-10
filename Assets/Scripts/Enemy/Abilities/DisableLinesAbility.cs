using System.Collections;
using UnityEngine;

namespace SlidingSiege
{
    /// Ghost/Phantom curse: disables rows and/or columns for sliding. The
    /// lines come from an array of coordinates — each yields its row, its
    /// column, or both, per the axis enum. Coordinates are either offsets
    /// from the owner's anchor or absolute grid positions, and in both
    /// modes the whole array can be shifted by an extra offset vector
    /// (mirroring SetHitboxAbility / hitbox part layouts). The ability
    /// clears its own previous curses first, so with Turns Disabled = 1 a
    /// curse lasts exactly one player turn; the source's death lifts its
    /// curses instantly (GridState.RemoveEnemy).
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Disable Lines")]
    public class DisableLinesAbility : EnemyAbility
    {
        public enum AxisMode { Row, Column, Both }
        public enum CoordinateSpace { RelativeToAnchor, GridAbsolute }

        [Header("Lines")]
        [Tooltip("Which line(s) each coordinate disables: its row, its column, or both.")]
        [SerializeField] private AxisMode axis = AxisMode.Row;
        [Tooltip("RelativeToAnchor: coordinates are offsets from the owner's anchor. GridAbsolute: coordinates are grid cells (x = row, y = col).")]
        [SerializeField] private CoordinateSpace space = CoordinateSpace.RelativeToAnchor;
        [Tooltip("Cells whose rows/columns get disabled (x = row, y = col). Wrapped into bounds.")]
        [SerializeField] private Vector2Int[] coordinates = { Vector2Int.zero };
        [Tooltip("Extra shift applied to every coordinate in both modes (like a hitbox part's Offset).")]
        [SerializeField] private Vector2Int offset;

        [Header("Duration")]
        [Tooltip("Player turns the lines stay disabled (ticked down at the start of each enemy phase).")]
        [SerializeField, Min(1)] private int turnsDisabled = 1;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner while cursing.")]
        [SerializeField] private string castAnimationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var owner = ctx.Owner;
            var s = ctx.State;
            if (owner == null || coordinates == null || coordinates.Length == 0) yield break;

            // Reapplying replaces this owner's previous curses.
            s.ClearDisabledLinesFrom(owner.Id);

            foreach (var coord in coordinates)
            {
                var cell = (space == CoordinateSpace.RelativeToAnchor ? owner.Anchor + coord : coord) + offset;
                if (axis != AxisMode.Column) s.DisableLine(true, cell.x, owner.Id, turnsDisabled);
                if (axis != AxisMode.Row) s.DisableLine(false, cell.y, owner.Id, turnsDisabled);
            }

            string what = axis == AxisMode.Row ? "row" : axis == AxisMode.Column ? "column" : "rows and columns";
            Debug.Log($"[SlidingSiege] {owner.Definition.name} curses its {what}!");
            result.Success = true;
            yield return ctx.PlayOwnerPresetAndWait(castAnimationPreset);
        }
    }
}
