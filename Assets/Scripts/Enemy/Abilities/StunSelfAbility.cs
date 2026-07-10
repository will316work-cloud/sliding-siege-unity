using System.Collections;
using UnityEngine;

namespace SlidingSiege
{
    /// Siren's broken-link collapse (JS checkSirenStunOnDeath), meant for
    /// the OnLinkBroken trigger: stuns the owner for a number of enemy
    /// phases and tears down its ongoing effects — links, its damage
    /// resistance, its card curses, and its charge counter.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Stun Self")]
    public class StunSelfAbility : EnemyAbility
    {
        [Header("Stun")]
        [SerializeField, Min(1)] private int stunTurns = 1;
        [Tooltip("Also remove the owner's DamageResistanceStatus (the Siren's while-linked shield).")]
        [SerializeField] private bool removeDamageResistance = true;
        [Tooltip("Also lift the owner's attack/item disables (Siren curses).")]
        [SerializeField] private bool clearOwnDisables = true;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner when stunned.")]
        [SerializeField] private string stunAnimationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var owner = ctx.Owner;
            if (owner == null || owner.HasStatus<StunStatus>()) yield break;

            owner.ClearLinks();
            owner.ResetCharge();
            if (removeDamageResistance) owner.RemoveStatuses(st => st is DamageResistanceStatus);
            if (clearOwnDisables) ctx.Combat?.ClearDisablesFrom(owner.Id);
            owner.AddStatus(new StunStatus(stunTurns));
            Debug.Log($"[SlidingSiege] {owner.Definition.name}'s last link is destroyed — it is stunned and its curse breaks!");
            result.Success = true;

            yield return ctx.PlayOwnerPresetAndWait(stunAnimationPreset);
        }
    }
}
