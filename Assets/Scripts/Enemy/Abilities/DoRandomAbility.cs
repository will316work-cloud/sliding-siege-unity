using System;
using System.Collections;
using UnityEngine;

namespace SlidingSiege
{
    /// Composite: rolls once among the listed abilities using their weights
    /// (0 = never picked) and executes the winner. The inner ability's
    /// result flows through, so its success gates this asset's post-delay.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Do Random")]
    public class DoRandomAbility : EnemyAbility
    {
        [Serializable]
        public class WeightedAbility
        {
            public EnemyAbility Ability;
            [Min(0f)] public float Weight = 1f;
        }

        [SerializeField] private WeightedAbility[] options = new WeightedAbility[0];

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            float total = 0f;
            foreach (var option in options)
                if (option.Ability != null && option.Ability != this)
                    total += option.Weight;
            if (total <= 0f) yield break;

            float roll = UnityEngine.Random.value * total;
            foreach (var option in options)
            {
                if (option.Ability == null || option.Ability == this || option.Weight <= 0f) continue;
                roll -= option.Weight;
                if (roll <= 0f)
                {
                    yield return option.Ability.Execute(ctx, result);
                    yield break;
                }
            }
        }
    }
}
