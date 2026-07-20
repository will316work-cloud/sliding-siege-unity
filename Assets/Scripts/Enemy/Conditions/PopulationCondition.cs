using UnityEngine;

namespace SlidingSiege
{
    /// Gates on the current living-enemy population: board total, or only
    /// enemies of a specific definition when a filter is set. Compares the
    /// current count against the thresholds (pending spawns are not counted).
    [CreateAssetMenu(menuName = "SlidingSiege/Conditions/Population")]
    public class PopulationCondition : AbilityCondition
    {
        [Tooltip("Only count living enemies of this definition; empty = count every living enemy.")]
        [SerializeField] private EnemyDefinition definitionFilter;
        [Tooltip("Fails while the count is below this. 0 = no minimum.")]
        [SerializeField, Min(0)] private int minCount = 0;
        [Tooltip("Fails while the count is at or above this. 0 = no maximum.")]
        [SerializeField, Min(0)] private int maxCount = 0;

        public override bool Evaluate(EnemyAbilityContext ctx)
        {
            int count;
            if (definitionFilter == null)
            {
                count = ctx.State.EnemyCount;
            }
            else
            {
                count = 0;
                foreach (var en in ctx.State.AllEnemies)
                    if (en.Definition == definitionFilter) count++;
            }

            if (count < minCount) return false;
            if (maxCount > 0 && count >= maxCount) return false;
            return true;
        }
    }
}
