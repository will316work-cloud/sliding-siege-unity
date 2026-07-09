using System;
using UnityEngine;

namespace SlidingSiege
{
    /// One serializable gate for DoUnderConditionAbility. Health thresholds
    /// are checked against the enemies inside the owner's stored hitbox
    /// (see SetHitboxAbility), as fractions of each target's Max HP.
    [Serializable]
    public class AbilityCondition
    {
        public enum SubjectKind { AnyHitboxTarget, AllHitboxTargets }
        public enum ComparisonKind { Below, AtOrAbove }

        public SubjectKind Subject = SubjectKind.AnyHitboxTarget;
        public ComparisonKind Comparison = ComparisonKind.Below;
        [Tooltip("Threshold as a fraction of each target's Max HP (1 = full health).")]
        [Range(0f, 1f)] public float HealthFraction = 1f;

        /// False when the owner has no stored hitbox or it holds no enemies.
        public bool Evaluate(EnemyAbilityContext ctx)
        {
            var targets = ctx.QueuedHitboxTargets();
            if (targets.Count == 0) return false;

            bool any = Subject == SubjectKind.AnyHitboxTarget;
            foreach (var en in targets)
            {
                bool pass = Comparison == ComparisonKind.Below
                    ? en.HP < en.MaxHP * HealthFraction
                    : en.HP >= en.MaxHP * HealthFraction;
                if (any && pass) return true;
                if (!any && !pass) return false;
            }
            return !any;
        }
    }
}
