using UnityEngine;

namespace SlidingSiege
{
    /// Authoring data for an item (mirrors the HTML game's ITEM_DEFS).
    /// Effect/targeting logic lives in the matching IItemEffect
    /// (see ItemEffectFactory).
    [CreateAssetMenu(menuName = "SlidingSiege/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] private ItemKind kind;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0)] private int startingCount = 2;
        [SerializeField, TextArea] private string description;

        [Tooltip("Shape-based effects (e.g. Gravity Orb) resolve this at the target cell; procedural previews use the FIRST part's highlight appearance.")]
        [SerializeField] private Hitbox hitbox = new Hitbox();

        public ItemKind Kind => kind;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int StartingCount => startingCount;
        public string Description => description;
        public Hitbox Hitbox => hitbox;
    }
}
