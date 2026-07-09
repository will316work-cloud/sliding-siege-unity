using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Grants a temporary damage-reduction ward to every FULL-health enemy
    /// inside the owner's stored hitbox (see SetHitboxAbility). Existing
    /// wards are refreshed rather than stacked.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Resistance Cast")]
    public class ResistanceCastAbility : EnemyAbility
    {
        [Header("Ward")]
        [Tooltip("Damage reduction factor: 0.2 = warded targets take 20% less damage.")]
        [SerializeField, Range(0f, 1f)] private float reductionFactor = 0.2f;
        [Tooltip("Enemy phases the ward lasts.")]
        [SerializeField, Min(1)] private int wardTurns = 1;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner before the ward lands.")]
        [SerializeField] private string castAnimationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var healthy = new List<Enemy>();
            foreach (var target in ctx.QueuedHitboxTargets())
                if (target.HP >= target.MaxHP) healthy.Add(target);
            if (healthy.Count == 0) yield break;

            yield return ctx.PlayOwnerPresetAndWait(castAnimationPreset);

            foreach (var target in healthy)
            {
                target.Statuses.RemoveAll(st => st is DamageReductionStatus);
                target.Statuses.Add(new DamageReductionStatus(reductionFactor, wardTurns));
            }
            result.Success = true;
        }
    }
}
