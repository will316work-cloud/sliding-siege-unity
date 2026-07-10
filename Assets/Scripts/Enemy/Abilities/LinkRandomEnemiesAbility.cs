using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Golem/Siren linking (JS rerollGolemLinks / linkSiren): clears the
    /// owner's links and picks a random batch of eligible enemies — never
    /// itself, never its own definition (golems don't link golems), never a
    /// definition with CanBeLinkTarget off (bombs). Critical (pending
    /// detonation) owners skip entirely; LinkOverlay draws the results.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Link Random Enemies")]
    public class LinkRandomEnemiesAbility : EnemyAbility
    {
        [Header("Linking")]
        [SerializeField, Min(0)] private int minLinks = 1;
        [SerializeField, Min(1)] private int maxLinks = 2;
        [Tooltip("Skip while the owner still has at least one LIVING link (Siren keeps links until broken); off = reroll every phase (Golem).")]
        [SerializeField] private bool onlyWhenUnlinked = false;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner while linking.")]
        [SerializeField] private string castAnimationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var owner = ctx.Owner;
            if (owner == null || owner.PendingDetonation) yield break;
            if (onlyWhenUnlinked && owner.LivingLinkTargets(ctx.State).Any()) yield break;

            owner.LinkedIds.Clear();
            var pool = ctx.State.Enemies.Values
                .Where(en => en.Id != owner.Id
                             && en.Definition != owner.Definition
                             && en.Definition.CanBeLinkTarget)
                .ToList();
            if (pool.Count == 0) yield break;

            int want = Random.Range(minLinks, maxLinks + 1);
            int count = Mathf.Min(want, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, pool.Count);
                owner.LinkedIds.Add(pool[idx].Id);
                pool.RemoveAt(idx);
            }
            if (owner.LinkedIds.Count == 0) yield break;

            Debug.Log($"[SlidingSiege] {owner.Definition.name} links itself to {owner.LinkedIds.Count} enem{(owner.LinkedIds.Count == 1 ? "y" : "ies")}.");
            result.Success = true;
            yield return ctx.PlayOwnerPresetAndWait(castAnimationPreset);
        }
    }
}
