using UnityEngine;

namespace SlidingSiege
{
    /// Authoring data for an attack (mirrors the HTML game's ATTACK_DEFS
    /// entries). Shape/variant logic lives in the matching
    /// IAttackShapeResolver (see AttackShapeResolverFactory).
    [CreateAssetMenu(menuName = "SlidingSiege/Attack Definition")]
    public class AttackDefinition : ScriptableObject
    {
        public AttackKind Kind;
        public string DisplayName;
        public Sprite Icon;
        [Min(0)] public int BaseDamage = 10;
        [Min(0)] public int StartingCharges = 2;
        [TextArea] public string Description;
    }
}
