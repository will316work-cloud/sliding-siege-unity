using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Authoring data for an attack. Shape is fully data-driven: each
    /// Hitbox is one selectable arrangement (cycled by re-tapping the
    /// anchor), built from prioritized HitboxParts.
    [CreateAssetMenu(menuName = "SlidingSiege/Attack Definition")]
    public class AttackDefinition : AbilityDefinition
    {
        [SerializeField, Min(0)] private int baseDamage = 10;
        [SerializeField] private Hitbox[] hitboxes = new Hitbox[0];

        public int BaseDamage => baseDamage;

        public int VariantCount => hitboxes != null && hitboxes.Length > 0 ? hitboxes.Length : 1;

        public string VariantLabel(int variantIndex)
        {
            if (hitboxes == null || hitboxes.Length == 0) return "";
            var hb = hitboxes[Mathf.Clamp(variantIndex, 0, hitboxes.Length - 1)];
            return string.IsNullOrEmpty(hb.Label) ? "Variant " + (variantIndex + 1) : hb.Label;
        }

        /// Cells the given variant hits (in-bounds only, deduped), each with
        /// the damage percent of the part that claimed it.
        public List<HitCell> ResolveCells(GridState state, Vector2Int anchor, int variantIndex)
        {
            if (hitboxes == null || hitboxes.Length == 0) return new List<HitCell>();
            return hitboxes[Mathf.Clamp(variantIndex, 0, hitboxes.Length - 1)].Resolve(state, anchor);
        }

        public override bool IsInfinite(CombatSystem combat) => combat.InfiniteAttacks;
        public override bool CanUse(CombatSystem combat) => combat.CanAttack(this);
        public override string DamageLabel(CombatSystem combat) =>
            Mathf.RoundToInt(baseDamage * combat.DamageMultiplier()) + " dmg";
        public override string ConfirmLabel(int variantIndex) =>
            DisplayName + " (" + VariantLabel(variantIndex) + ")";
    }
}
