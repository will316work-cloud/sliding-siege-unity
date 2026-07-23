using System.Collections;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// The Mage's spell (JS rerollMageDisablesSequenced): each enemy phase
    /// the owner drops its previous disable and disables one random attack
    /// the player still has charges for. The disable lifts when the owner
    /// dies (CombatSystem clears per-source disables on enemy removal) or
    /// when this reroll picks a new target next phase.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Disable Attack")]
    public class DisableAttackAbility : EnemyAbility
    {
        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner while casting.")]
        [SerializeField] private string castAnimationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var combat = ctx.Combat;
            var owner = ctx.Owner;
            if (combat == null || owner == null) yield break;

            combat.ClearDisablesFrom(owner.Id);
            var pool = combat.AvailableAbilities().OfType<AttackDefinition>().ToList();
            if (pool.Count == 0) yield break;

            var pick = pool[Random.Range(0, pool.Count)];
            combat.Disable(owner.Id, pick);
            Debug.Log($"[SlidingSiege] {owner.Definition.name} casts a disabling spell on {pick.DisplayName}!");
            result.Success = true;

            yield return ctx.PlayOwnerPresetAndWait(castAnimationPreset);
        }
    }
}
