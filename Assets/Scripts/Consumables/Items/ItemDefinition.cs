using UnityEngine;

namespace SlidingSiege
{
    /// Authoring data for an item (mirrors the HTML game's ITEM_DEFS).
    /// Effect/targeting logic lives in the matching IItemEffect
    /// (see ItemEffectFactory).
    [CreateAssetMenu(menuName = "SlidingSiege/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        public ItemKind Kind;
        public string DisplayName;
        public Sprite Icon;
        [Min(0)] public int StartingCount = 2;
        [TextArea] public string Description;

        [Tooltip("Shape-based effects (e.g. Gravity Orb) resolve this at the target cell; procedural previews use the FIRST part's highlight appearance.")]
        public Hitbox Hitbox = new Hitbox();
    }
}
