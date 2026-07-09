using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace SlidingSiege
{
    /// Changes the owner's body into the serialized shape (with its own
    /// sprite and visual rect), placed at anchor + anchorOffset — or reverts
    /// to the definition body at the remembered origin. Performs NO room
    /// check: gate with a Shape Fits condition (overlaps stack, edges wrap).
    /// Visuals: plays the AnimationCaller preset when the owner's piece has
    /// one matching the label; otherwise falls back to a DOTween size +
    /// position tween into the new rect.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Change Shape")]
    public class ChangeShapeAbility : EnemyAbility
    {
        [Header("Shape change")]
        [Tooltip("Revert to the definition body at the remembered pre-change anchor instead of applying the shape below.")]
        [SerializeField] private bool revertToDefinition;
        [Tooltip("Anchor shift applied with the new shape (x = row, y = col). Ignored on revert.")]
        [SerializeField] private Vector2Int anchorOffset;
        [SerializeField] private EnemyShape shape = new EnemyShape();

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner before reshaping. When the piece has no preset with this label, the tween fallback below is used instead.")]
        [SerializeField] private string animationPreset = "";
        [Tooltip("Fallback tween: seconds of the size/position change.")]
        [SerializeField, Min(0.01f)] private float tweenDuration = 0.25f;
        [SerializeField] private Ease tweenEase = Ease.OutCubic;

        // Read-only access for ShapeFitsCondition (single source of truth).
        public bool RevertToDefinition => revertToDefinition;
        public Vector2Int AnchorOffset => anchorOffset;
        public EnemyShape Shape => shape;

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var s = ctx.State;
            var en = ctx.Owner;
            if (en == null) yield break;

            Vector2Int targetAnchor;
            EnemyShape targetShape;
            if (revertToDefinition)
            {
                if (en.ShapeOverride == null) yield break;
                var origin = en.ResizeOriginOffset ?? Vector2Int.zero;
                targetAnchor = new Vector2Int(s.Wrap(en.Anchor.x + origin.x, s.Rows),
                                              s.Wrap(en.Anchor.y + origin.y, s.Cols));
                en.ResizeOriginOffset = null;
                targetShape = null;
            }
            else
            {
                if (shape == null || shape.BodyCells == null || shape.BodyCells.Length == 0) yield break;
                // Accumulate so a later revert lands on the original cell
                // even after several chained shape changes.
                en.ResizeOriginOffset = (en.ResizeOriginOffset ?? Vector2Int.zero) - anchorOffset;
                targetAnchor = en.Anchor + anchorOffset;
                targetShape = shape;
            }

            if (HasPreset(ctx))
            {
                yield return ctx.PlayOwnerPresetAndWait(animationPreset);
                s.ReshapeEnemy(en.Id, targetAnchor, targetShape);
            }
            else if (ctx.Views != null)
            {
                bool done = false;
                ctx.Views.ReshapeEnemyTweened(en, targetAnchor, targetShape, tweenDuration, tweenEase, () => done = true);
                while (!done) yield return null;
            }
            else
            {
                s.ReshapeEnemy(en.Id, targetAnchor, targetShape);
            }
            result.Success = true;
        }

        /// True when the owner's main piece has an AnimationCaller preset
        /// matching the serialized label.
        private bool HasPreset(EnemyAbilityContext ctx)
        {
            if (string.IsNullOrEmpty(animationPreset)) return false;
            if (ctx.Views == null || !ctx.Views.TryGetMainPiece(ctx.Owner.Id, out var piece)) return false;
            var caller = piece.AnimationCaller;
            if (caller == null) return false;
            foreach (var preset in caller.Presets)
                if (string.Equals(preset.Label, animationPreset, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
