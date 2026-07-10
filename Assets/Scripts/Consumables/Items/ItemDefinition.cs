using UnityEngine;
using UnityEngine.Serialization;

namespace SlidingSiege
{
    /// Authoring data for an item (mirrors the HTML game's ITEM_DEFS).
    /// Effect/targeting logic lives in the matching IItemEffect
    /// (see ItemEffectFactory).
    [CreateAssetMenu(menuName = "SlidingSiege/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [FormerlySerializedAs("Kind")]
        [SerializeField] private ItemKind kind;
        [FormerlySerializedAs("DisplayName")]
        [SerializeField] private string displayName;
        [FormerlySerializedAs("Icon")]
        [SerializeField] private Sprite icon;
        [FormerlySerializedAs("StartingCount")]
        [SerializeField, Min(0)] private int startingCount = 2;
        [FormerlySerializedAs("Description")]
        [SerializeField, TextArea] private string description;

        [Tooltip("Shape-based effects (e.g. Gravity Orb) resolve this at the target cell; procedural previews use the FIRST part's highlight appearance.")]
        [FormerlySerializedAs("Hitbox")]
        [SerializeField] private Hitbox hitbox = new Hitbox();

        public ItemKind Kind => kind;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int StartingCount => startingCount;
        public string Description => description;
        public Hitbox Hitbox => hitbox;
    }
}
