using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Composite: runs its inner ability only when EVERY condition passes.
    /// The inner ability's result flows through, so its success gates this
    /// asset's post-delay.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Do Under Condition")]
    public class DoUnderConditionAbility : EnemyAbility
    {
        [SerializeField] private List<AbilityCondition> conditions = new List<AbilityCondition>();
        [SerializeField] private EnemyAbility ability;

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            if (ability == null || ability == this) yield break;
            foreach (var condition in conditions)
                if (condition == null || !condition.Evaluate(ctx)) yield break;
            yield return ability.Execute(ctx, result);
        }
    }
}
