using UnityEngine;

namespace SlidingSiege
{
    /// Base for condition ASSETS used by DoUnderConditionAbility. Subclasses
    /// (health thresholds, shape fits, ...) are ScriptableObjects so one
    /// condition can gate many abilities.
    public abstract class AbilityCondition : ScriptableObject
    {
        public abstract bool Evaluate(EnemyAbilityContext ctx);
    }
}
