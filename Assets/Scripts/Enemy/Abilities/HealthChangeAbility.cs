using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Changes the HP of every enemy inside the owner's stored hitbox (see
    /// SetHitboxAbility): positive amounts heal (capped at max HP), negative
    /// amounts damage (killed enemies are removed). The claiming cell's
    /// damage factor scales both directions; the target's damage-taken
    /// multiplier applies to damage only. Gate with DoUnderConditionAbility
    /// for checks like "only heal when someone is hurt".
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Health Change")]
    public class HealthChangeAbility : EnemyAbility
    {
        public enum ChangeMode { FlatAmount, MaxHealthFactor, CurrentHealthFactor }

        [Header("Health change")]
        [SerializeField] private ChangeMode mode = ChangeMode.MaxHealthFactor;
        [Tooltip("Positive heals, negative damages. FlatAmount: HP; factor modes: fraction of the target's max/current HP.")]
        [SerializeField] private float amount = 0.5f;
        [Tooltip("Also affect the owner when its footprint is inside the hitbox.")]
        [SerializeField] private bool includeOwner = true;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner before the change lands.")]
        [SerializeField] private string castAnimationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var s = ctx.State;
            var owner = ctx.Owner;
            var hitbox = owner.QueuedHitbox;
            if (hitbox == null) yield break;

            // First (highest-priority) cell touching a target decides its factor.
            var factors = new Dictionary<Enemy, float>();
            foreach (var hit in hitbox.Resolve(s, owner.Anchor))
                foreach (var en in s.EnemiesAt(hit.Cell.x, hit.Cell.y))
                {
                    if (!includeOwner && en.Id == owner.Id) continue;
                    if (!factors.ContainsKey(en)) factors[en] = hit.DamageFactor;
                }
            if (factors.Count == 0) yield break;

            yield return ctx.PlayOwnerPresetAndWait(castAnimationPreset);

            var killed = new List<int>();
            foreach (var kv in factors)
            {
                // Damage redirects to a linking absorber (Golem) — heals
                // stay on the target. Proportional amounts are measured
                // against the ORIGINAL target's health and scaled by its
                // statuses; a redirect then also applies the absorber's own
                // statuses to what it actually receives.
                var target = kv.Key;
                bool damaging = amount < 0f;
                var recipient = damaging ? CombatSystem.RouteDamage(s, target) : target;
                float baseAmount = mode switch
                {
                    ChangeMode.FlatAmount => amount,
                    ChangeMode.CurrentHealthFactor => target.HP * amount,
                    _ => target.MaxHP * amount,
                };
                float scaled = baseAmount * kv.Value;
                if (damaging)
                {
                    scaled *= target.DamageTakenMultiplier();
                    if (recipient != target) scaled *= recipient.DamageTakenMultiplier();
                }
                int delta = Mathf.RoundToInt(scaled);
                if (delta < 0 && recipient != target) s.NotifyDamageRedirected(target, recipient);
                if (delta < 0) delta = -CombatSystem.ClampToDetonator(recipient, -delta);
                recipient.HP = Mathf.Min(recipient.MaxHP, recipient.HP + delta);
                if (recipient.HP <= 0 && CombatSystem.HandleZeroHp(s, recipient) && !killed.Contains(recipient.Id))
                    killed.Add(recipient.Id);
            }
            foreach (var id in killed) s.RemoveEnemy(id);
            result.Success = true;
        }
    }
}
