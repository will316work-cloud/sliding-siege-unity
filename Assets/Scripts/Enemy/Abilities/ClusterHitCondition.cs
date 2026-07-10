using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Pending-hit gate over the owner's cluster (Slime resolution): checks
    /// whether the owner itself, ANY member, or ALL members of its cluster
    /// carry the PendingHit mark. Invert for "not all struck" style gates.
    [CreateAssetMenu(menuName = "SlidingSiege/Conditions/Cluster Hit")]
    public class ClusterHitCondition : AbilityCondition
    {
        public enum SubjectKind { Owner, AnyClusterMember, AllClusterMembers }

        [SerializeField] private SubjectKind subject = SubjectKind.Owner;
        [Tooltip("On = passes when the check FAILS (e.g. 'not every member was struck').")]
        [SerializeField] private bool invert;

        public override bool Evaluate(EnemyAbilityContext ctx)
        {
            var owner = ctx.Owner;
            if (owner == null) return false;

            bool hit = subject switch
            {
                SubjectKind.Owner => owner.PendingHit,
                SubjectKind.AnyClusterMember => owner.ClusterMembers(ctx.State).Any(m => m.PendingHit),
                _ => owner.ClusterMembers(ctx.State).All(m => m.PendingHit),
            };
            return hit != invert;
        }
    }
}
