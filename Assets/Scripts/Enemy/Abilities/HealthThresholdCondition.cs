using UnityEngine;

namespace SlidingSiege
{
    /// Health-threshold gate, as fractions of the subject's Max HP. Subject
    /// is either the enemies inside the owner's stored hitbox (see
    /// SetHitboxAbility; false when the hitbox is missing or holds no
    /// enemies) or the owner itself (e.g. Golem's critical check).
    [CreateAssetMenu(menuName = "SlidingSiege/Conditions/Health Threshold")]
    public class HealthThresholdCondition : AbilityCondition
    {
        public enum SubjectKind { AnyHitboxTarget, AllHitboxTargets, Owner, AnyClusterMember, AllClusterMembers }
        public enum ComparisonKind { Below, AtOrBelow, Above, AtOrAbove, RoughlyEqual }

        [SerializeField] private SubjectKind subject = SubjectKind.AnyHitboxTarget;
        [SerializeField] private ComparisonKind comparison = ComparisonKind.Below;
        [Tooltip("Threshold as a fraction of each subject's Max HP (1 = full health).")]
        [SerializeField, Range(0f, 1f)] private float healthFraction = 1f;
        [Tooltip("RoughlyEqual only: how far the health ratio may sit from the threshold and still pass.")]
        [SerializeField, Min(0f)] private float equalityMargin = 0.001f;

        public override bool Evaluate(EnemyAbilityContext ctx)
        {
            if (subject == SubjectKind.Owner)
                return ctx.Owner != null && Passes(ctx.Owner);

            if (subject == SubjectKind.AnyClusterMember || subject == SubjectKind.AllClusterMembers)
            {
                if (ctx.Owner == null) return false;
                bool anyMember = subject == SubjectKind.AnyClusterMember;
                foreach (var member in ctx.Owner.ClusterMembers(ctx.State))
                {
                    bool memberPass = Passes(member);
                    if (anyMember && memberPass) return true;
                    if (!anyMember && !memberPass) return false;
                }
                return !anyMember;
            }

            var targets = ctx.QueuedHitboxTargets();
            if (targets.Count == 0) return false;

            bool any = subject == SubjectKind.AnyHitboxTarget;
            foreach (var en in targets)
            {
                bool pass = Passes(en);
                if (any && pass) return true;
                if (!any && !pass) return false;
            }
            return !any;
        }

        private bool Passes(Enemy en)
        {
            float ratio = en.MaxHP > 0 ? (float)en.HP / en.MaxHP : 0f;
            return comparison switch
            {
                ComparisonKind.Below => ratio < healthFraction,
                ComparisonKind.Above => ratio > healthFraction,
                ComparisonKind.AtOrAbove => ratio >= healthFraction,
                ComparisonKind.AtOrBelow => ratio <= healthFraction,
                _ => Mathf.Abs(ratio - healthFraction) <= equalityMargin,
            };
        }
    }
}
