using System.Collections.Generic;

namespace SlidingSiege
{
    /// Maps ItemKind to its effect (the Unity analogue of the HTML game's
    /// ITEM_EFFECT_RESOLVERS registry).
    public static class ItemEffectFactory
    {
        private static readonly Dictionary<ItemKind, IItemEffect> Effects =
            new Dictionary<ItemKind, IItemEffect>
            {
                { ItemKind.ExtraSwing,       new ExtraSwingEffect() },
                { ItemKind.GravityOrb,       new GravityOrbEffect() },
                { ItemKind.ExpandedSoul,     new ExpandedSoulEffect() },
                { ItemKind.Teleport,         new TeleportEffect() },
                { ItemKind.DamageMultiplier, new VulnerabilityEffect() },
            };

        public static IItemEffect Get(ItemKind kind) => Effects[kind];
    }
}
