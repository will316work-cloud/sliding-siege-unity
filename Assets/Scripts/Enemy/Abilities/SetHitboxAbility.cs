using System.Collections;
using UnityEngine;

namespace SlidingSiege
{
    /// Stores a hitbox on the owner; it persists until another Set Hitbox
    /// overwrites it. Cast abilities and conditions resolve it at the
    /// owner's current anchor. Parts support rows, columns, diagonals, and
    /// grids exactly like attack hitboxes (damage factors are ignored).
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Set Hitbox")]
    public class SetHitboxAbility : EnemyAbility
    {
        [SerializeField] private Hitbox hitbox = new Hitbox();

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            ctx.Owner.QueueHitbox(hitbox);
            ctx.State.NotifyEnemyHitboxChanged(ctx.Owner);
            result.Success = true;
            yield break;
        }
    }
}
