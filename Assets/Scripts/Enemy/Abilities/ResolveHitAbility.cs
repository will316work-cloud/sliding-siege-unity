using System.Collections;
using UnityEngine;

namespace SlidingSiege
{
    /// Consumes the owner's "was hit" state: clears its PendingHit mark and,
    /// while it has health again, its critical (0-HP limbo) flag. Sequenced
    /// after a Slime's self-heal, or alone as the fall-through resolve.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Resolve Hit")]
    public class ResolveHitAbility : EnemyAbility
    {
        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var owner = ctx.Owner;
            if (owner == null) yield break;

            result.Success = owner.PendingHit;
            owner.ClearPendingHit();
            if (owner.HP > 0) owner.ResetCritical();
        }
    }
}
