using UnityEngine;

namespace SlidingSiege
{
    /// Authoring data for an item (mirrors the HTML game's ITEM_DEFS).
    /// Effect/targeting logic lives in the matching IItemEffect
    /// (see ItemEffectFactory).
    [CreateAssetMenu(menuName = "SlidingSiege/Item Definition")]
    public class ItemDefinition : AbilityDefinition
    {
        [SerializeField] private ItemKind kind;

        [Tooltip("Shape-based effects (e.g. Gravity Orb) resolve this at the target cell; procedural previews use the FIRST part's highlight appearance.")]
        [SerializeField] private Hitbox hitbox = new Hitbox();

        public ItemKind Kind => kind;
        public Hitbox Hitbox => hitbox;

        public override bool IsInfinite(CombatSystem combat) => combat.InfiniteItems;
        public override bool CanUse(CombatSystem combat) => combat.CanUseItem(this);
    }
}
