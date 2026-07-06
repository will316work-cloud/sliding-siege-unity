using System.Collections.Generic;

namespace SlidingSiege
{
    /// Maps AttackKind to its shape resolver (the Unity analogue of the
    /// HTML game's ATTACK_CELL_RESOLVERS registry).
    public static class AttackShapeResolverFactory
    {
        private static readonly Dictionary<AttackKind, IAttackShapeResolver> Resolvers =
            new Dictionary<AttackKind, IAttackShapeResolver>
            {
                { AttackKind.Axe,     new AxeShapeResolver() },
                { AttackKind.Sword,   new SwordShapeResolver() },
                { AttackKind.Hammer,  new HammerShapeResolver() },
                { AttackKind.Ring,    new RingShapeResolver() },
                { AttackKind.Crystal, new CrystalShapeResolver() },
            };

        public static IAttackShapeResolver Get(AttackKind kind) => Resolvers[kind];
    }
}
