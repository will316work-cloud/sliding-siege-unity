using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Siren's song (JS resolveSirenSongsAndShrieks): each enemy phase this
    /// first lifts the owner's previous curses (so a shriek lasts exactly
    /// one player turn), then — only while the owner has living links —
    /// advances its charge counter; at full charge it shrieks, cursing a
    /// batch of random available attacks/items (one combined pool), and
    /// the counter resets. Curses also lift when the owner dies or is
    /// stunned (StunSelfAbility).
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Charge Curse")]
    public class ChargeCurseAbility : EnemyAbility
    {
        [Header("Charge")]
        [Tooltip("Linked enemy phases needed before the shriek fires (charges on top of this value, JS songCounter >= 3).")]
        [SerializeField, Min(1)] private int chargeTurns = 3;

        [Header("Curse")]
        [Tooltip("How many cards get cursed, drawn from one combined pool of available attacks and items.")]
        [SerializeField, Min(1)] private int curseCount = 4;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner when it shrieks.")]
        [SerializeField] private string shriekAnimationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var owner = ctx.Owner;
            var combat = ctx.Combat;
            if (owner == null || combat == null) yield break;

            // Last phase's curses expire now (1-turn duration).
            combat.ClearDisablesFrom(owner.Id);

            if (!owner.LivingLinkTargets(ctx.State).Any()) yield break;

            if (owner.ChargeCounter < chargeTurns)
            {
                owner.AdvanceCharge();
                yield break;
            }

            // Full charge: shriek. One combined pool, curseCount picks.
            var pool = combat.AvailableAbilities().ToList();
            if (pool.Count == 0) yield break;

            int picks = Mathf.Min(curseCount, pool.Count);
            var names = new List<string>();
            for (int i = 0; i < picks; i++)
            {
                int idx = Random.Range(0, pool.Count);
                var pick = pool[idx];
                pool.RemoveAt(idx);
                combat.Disable(owner.Id, pick);
                names.Add(pick.DisplayName);
            }
            owner.ResetCharge();
            Debug.Log($"[SlidingSiege] {owner.Definition.name} shrieks! Cursed: {string.Join(", ", names)}");
            result.Success = true;

            yield return ctx.PlayOwnerPresetAndWait(shriekAnimationPreset);
        }
    }
}
