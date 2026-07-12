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
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0)] private int baseDamage = 10;
        [SerializeField, Min(0)] private int startingCharges = 2;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Hitbox[] hitboxes = new Hitbox[0];

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int BaseDamage => baseDamage;
        public int StartingCharges => startingCharges;
        public string Description => description;

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
    }
}
