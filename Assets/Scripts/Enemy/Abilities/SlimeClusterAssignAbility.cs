using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Slime cluster assignment (JS chooseSlimeClusterAssignment), meant to
    /// run OnSpawn: each existing cluster of the owner's definition rolls a
    /// join chance based on its size (75% under 3 members, 40% at exactly 3,
    /// never at 4+); one passing cluster is picked at random, otherwise the
    /// owner starts a new cluster (id = its own enemy id, globally unique).
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Slime Cluster Assign")]
    public class SlimeClusterAssignAbility : EnemyAbility
    {
        [Header("Join rules")]
        [Tooltip("Join chance for clusters smaller than Full Roll Size.")]
        [SerializeField, Range(0f, 1f)] private float joinChanceSmall = 0.75f;
        [Tooltip("Join chance for clusters at exactly Full Roll Size members; larger clusters never accept.")]
        [SerializeField, Range(0f, 1f)] private float joinChanceAtCap = 0.4f;
        [Tooltip("Cluster size at which the lower join chance applies (beyond it, no joining).")]
        [SerializeField, Min(1)] private int fullRollSize = 3;

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var owner = ctx.Owner;
            if (owner == null || owner.ClusterId >= 0) yield break;

            var clusterSizes = ctx.State.ClusterSizes(owner.Definition, owner.Id);

            var winners = new List<int>();
            foreach (var kv in clusterSizes)
            {
                float chance = kv.Value < fullRollSize ? joinChanceSmall
                    : kv.Value == fullRollSize ? joinChanceAtCap : 0f;
                if (chance > 0f && Random.value < chance) winners.Add(kv.Key);
            }

            owner.AssignCluster(winners.Count > 0 ? winners[Random.Range(0, winners.Count)] : owner.Id);
            result.Success = true;
            yield break;
        }
    }
}
