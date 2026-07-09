using System.Collections;
using UnityEngine;

namespace SlidingSiege
{
    /// Removes the owner from the board (the standard hurt + death removal
    /// sequence plays via the view manager).
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Kill Self")]
    public class KillSelfAbility : EnemyAbility
    {
        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner before it is removed.")]
        [SerializeField] private string animationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            yield return ctx.PlayOwnerPresetAndWait(animationPreset);
            ctx.State.RemoveEnemy(ctx.Owner.Id);
            result.Success = true;
        }
    }
}
