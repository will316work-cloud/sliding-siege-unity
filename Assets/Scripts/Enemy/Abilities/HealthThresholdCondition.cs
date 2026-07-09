using UnityEngine;

namespace SlidingSiege
{
    /// Health-threshold gate checked against the enemies inside the owner's
    /// stored hitbox (see SetHitboxAbility), as fractions of each target's
    /// Max HP. False when the hitbox is missing or holds no enemies.
    [CreateAssetMenu(menuName = "SlidingSiege/Conditions/Health Threshold")]
    public class HealthThresholdCondition : AbilityCondition
    {
        public enum SubjectKind { AnyHitboxTarget, AllHitboxTargets }
        public enum ComparisonKind { Below, AtOrAbove }

        [SerializeField] private SubjectKind subject = SubjectKind.AnyHitboxTarget;
        [SerializeField] private ComparisonKind comparison = ComparisonKind.Below;
        [Tooltip("Threshold as a fraction of each target's Max HP (1 = full health).")]
        [SerializeField, Range(0f, 1f)] private float healthFraction = 1f;

        public override bool Evaluate(EnemyAbilityContext ctx)
        {
            var targets = ctx.QueuedHitboxTargets();
            if (targets.Count == 0) return false;

            bool any = subject == SubjectKind.AnyHitboxTarget;
            foreach (var en in targets)
            {
                bool pass = comparison == ComparisonKind.Below
                    ? en.HP < en.MaxHP * healthFraction
                    : en.HP >= en.MaxHP * healthFraction;
                if (any && pass) return true;
                if (!any && !pass) return false;
            }
            return !any;
        }
    }
}
