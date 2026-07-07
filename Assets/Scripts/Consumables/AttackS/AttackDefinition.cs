using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Authoring data for an attack. Shape is fully data-driven: each
    /// Hitbox is one selectable arrangement (cycled by re-tapping the
    /// anchor), built from prioritized HitboxParts.
    [CreateAssetMenu(menuName = "SlidingSiege/Attack Definition")]
    public class AttackDefinition : ScriptableObject
    {
        public string DisplayName;
        public Sprite Icon;
        [Min(0)] public int BaseDamage = 10;
        [Min(0)] public int StartingCharges = 2;
        [TextArea] public string Description;
        public Hitbox[] Hitboxes = new Hitbox[0];

        public int VariantCount => Hitboxes != null && Hitboxes.Length > 0 ? Hitboxes.Length : 1;

        public string VariantLabel(int variantIndex)
        {
            if (Hitboxes == null || Hitboxes.Length == 0) return "";
            var hb = Hitboxes[Mathf.Clamp(variantIndex, 0, Hitboxes.Length - 1)];
            return string.IsNullOrEmpty(hb.Label) ? "Variant " + (variantIndex + 1) : hb.Label;
        }

        /// Cells the given variant hits (in-bounds only, deduped), each with
        /// the damage percent of the part that claimed it.
        public List<HitCell> ResolveCells(GridState state, Vector2Int anchor, int variantIndex)
        {
            if (Hitboxes == null || Hitboxes.Length == 0) return new List<HitCell>();
            return Hitboxes[Mathf.Clamp(variantIndex, 0, Hitboxes.Length - 1)].Resolve(state, anchor);
        }
    }
}
