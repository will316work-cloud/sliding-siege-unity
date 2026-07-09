using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Composite if/else-if chain: branches are checked first to last, and
    /// the FIRST branch whose conditions all pass runs its ability — later
    /// branches are skipped. An empty condition list always passes (put one
    /// last as an "else"). The executed ability's result flows through, so
    /// its success gates this asset's post-delay.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Do Under Condition")]
    public class DoUnderConditionAbility : EnemyAbility
    {
        [Serializable]
        public class Branch
        {
            [Tooltip("ALL must pass for this branch to run. Empty = always passes (else branch).")]
            public List<AbilityCondition> Conditions = new List<AbilityCondition>();
            public EnemyAbility Ability;
        }

        [SerializeField] private List<Branch> branches = new List<Branch>();

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            foreach (var branch in branches)
            {
                if (branch == null) continue;
                if (!Passes(branch, ctx)) continue;

                // First passing branch consumes the chain, even when its
                // ability slot is empty (an explicit do-nothing else).
                if (branch.Ability != null && branch.Ability != this)
                    yield return branch.Ability.Execute(ctx, result);
                yield break;
            }
        }

        private static bool Passes(Branch branch, EnemyAbilityContext ctx)
        {
            if (branch.Conditions == null) return true;
            foreach (var condition in branch.Conditions)
                if (condition != null && !condition.Evaluate(ctx))
                    return false;
            return true;
        }
    }
}
