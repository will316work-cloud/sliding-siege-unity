using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Composite if/else-if chain: branches are checked first to last, and
    /// the FIRST branch whose conditions all pass runs its abilities in
    /// order â€” later branches are skipped. An empty condition list always
    /// passes (put one last as an "else"). Each sub-ability's own postDelay
    /// applies after it succeeds; the branch reports success (gating this
    /// asset's postDelay) when ANY sub-ability succeeded.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Do Under Condition")]
    public class DoUnderConditionAbility : EnemyAbility
    {
        [Serializable]
        public class Branch
        {
            [Tooltip("ALL must pass for this branch to run. Empty = always passes (else branch).")]
            [SerializeField] private List<AbilityCondition> conditions = new List<AbilityCondition>();
            [Tooltip("Run in order; each one's own postDelay applies when it succeeds.")]
            [SerializeField] private List<EnemyAbility> abilities = new List<EnemyAbility>();

            public IReadOnlyList<AbilityCondition> Conditions => conditions;
            public IReadOnlyList<EnemyAbility> Abilities => abilities;
        }

        [SerializeField] private List<Branch> branches = new List<Branch>();

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            foreach (var branch in branches)
            {
                if (branch == null) continue;
                if (!Passes(branch, ctx)) continue;

                // First passing branch consumes the chain, even when its
                // ability list is empty (an explicit do-nothing else).
                if (branch.Abilities == null) yield break;
                foreach (var ability in branch.Abilities)
                {
                    if (ability == null || ability == this) continue;
                    var subResult = new AbilityResult();
                    yield return ability.Execute(ctx, subResult);
                    if (subResult.Success)
                    {
                        result.Success = true;
                        if (ability.PostDelay > 0f)
                            yield return new WaitForSeconds(ability.PostDelay);
                    }
                    // Stop if a sub-ability removed the owner (e.g. KillSelf).
                    if (ctx.Owner != null && !ctx.State.ContainsEnemy(ctx.Owner.Id))
                        break;
                }
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
