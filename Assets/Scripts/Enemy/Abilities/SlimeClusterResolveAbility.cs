using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Slime cluster resolution (JS resolveSlimes), run each enemy phase:
    /// damage to slimes only sticks if EVERY member of the owner's cluster
    /// was hit since the last resolution — then members at 0 HP die.
    /// Otherwise the hit members regenerate to full and everyone's pending
    /// mark clears. The first cluster member to act resolves the whole
    /// cluster; later members see no pending hits and no-op, so one asset
    /// on the definition covers everything. Pair with DiesAtZeroHP off so
    /// slimes survive at 0 HP until this runs.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Slime Cluster Resolve")]
    public class SlimeClusterResolveAbility : EnemyAbility
    {
        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var owner = ctx.Owner;
            if (owner == null) yield break;

            var members = ctx.State.Enemies.Values
                .Where(en => en.Definition == owner.Definition
                             && (owner.ClusterId >= 0 ? en.ClusterId == owner.ClusterId : en.Id == owner.Id))
                .ToList();
            if (!members.Any(m => m.PendingHit)) yield break;

            result.Success = true;
            if (members.All(m => m.PendingHit))
            {
                // Whole cluster struck — damage holds, 0-HP members die.
                var killed = new List<int>();
                foreach (var m in members)
                {
                    m.PendingHit = false;
                    if (m.HP <= 0) killed.Add(m.Id);
                }
                foreach (var id in killed) ctx.State.RemoveEnemy(id);
                Debug.Log($"[SlidingSiege] An entire {owner.Definition.name} cluster was struck — damage holds! ({killed.Count} defeated)");
            }
            else
            {
                // Not everyone was hit — struck members regenerate to full.
                foreach (var m in members)
                {
                    if (m.PendingHit)
                    {
                        m.HP = m.MaxHP;
                        m.PendingDetonation = false; // back from the 0-HP limbo
                    }
                    m.PendingHit = false;
                }
                Debug.Log($"[SlidingSiege] A {owner.Definition.name} cluster regenerates! Not every member was struck.");
            }
        }
    }
}
