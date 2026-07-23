using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Core combat: attack charges, item counts, HP damage, kills.
    /// Extension points (score, combos, QTE multipliers, enemy special
    /// cases, turn cycle) hang off the events and DamageMultiplier hook.
    public class CombatSystem
    {
        private readonly GridState _state;
        private readonly Dictionary<AbilityDefinition, int> _counts = new Dictionary<AbilityDefinition, int>();

        public int AttacksRemaining { get; private set; } = 1;

        /// Extra Swing item: one more attack this turn.
        public void GrantExtraAttack() => AttacksRemaining++;

        /// Turn system hook: reset the per-turn attack allowance.
        public void SetAttacksRemaining(int count) => AttacksRemaining = count;

        /// Debug toggles: when true, using an attack/item consumes nothing
        /// and the ability lists never disable (counts show as infinite).
        public bool InfiniteAttacks { get; set; }
        public bool InfiniteItems { get; set; }

        /// Global outgoing damage multiplier hook (damage bonus % later).
        public Func<float> DamageMultiplier { get; set; } = () => 1f;

        public event Action<AttackResult> OnAttackResolved;
        public event Action OnInventoryChanged;

        public CombatSystem(GridState state)
        {
            _state = state;
            // Death breaks any spells the dead enemy had on the player's cards.
            _state.OnEnemyRemoved += en => ClearDisablesFrom(en.Id);
        }

        // ---------------- Consumable disabling ----------------
        // Enemies (Mage spells, Siren shrieks) can disable attacks/items.
        // Registered per source enemy id so a kill lifts that enemy's
        // disables immediately; sources reroll by clearing then re-adding.

        private readonly Dictionary<int, HashSet<AbilityDefinition>> _disables = new Dictionary<int, HashSet<AbilityDefinition>>();

        public void Disable(int sourceEnemyId, AbilityDefinition def)
        {
            if (!_disables.TryGetValue(sourceEnemyId, out var set))
                _disables[sourceEnemyId] = set = new HashSet<AbilityDefinition>();
            set.Add(def);
            OnInventoryChanged?.Invoke();
        }

        public bool ClearDisablesFrom(int sourceEnemyId)
        {
            bool removed = _disables.Remove(sourceEnemyId);
            if (removed) OnInventoryChanged?.Invoke();
            return removed;
        }

        public bool IsDisabled(AbilityDefinition def) =>
            _disables.Values.Any(set => set.Contains(def));

        /// Abilities a disabling enemy may target: set up and with
        /// charges/count left (all of them if that type's Infinite* toggle
        /// is on).
        public IEnumerable<AbilityDefinition> AvailableAbilities() =>
            _counts.Where(kv => kv.Key.IsInfinite(this) || kv.Value > 0).Select(kv => kv.Key);

        /// All (source enemy, ability) disable pairs, for the link display.
        public IEnumerable<(int SourceId, AbilityDefinition Def)> DisableEntries()
        {
            foreach (var kv in _disables)
                foreach (var def in kv.Value)
                    yield return (kv.Key, def);
        }

        // ---------------- Inventory ----------------

        public void Setup(AbilityDefinition def) => _counts[def] = def.StartingCount;

        public int GetCount(AbilityDefinition def) => _counts.TryGetValue(def, out var v) ? v : 0;

        /// Spends one charge/count of `def` (no-op success under that
        /// type's Infinite* toggle). `notify`: false lets a caller that's
        /// about to raise its own OnInventoryChanged (e.g. ResolveAttack)
        /// skip the redundant one this would otherwise fire.
        public bool Consume(AbilityDefinition def, bool notify = true)
        {
            if (def.IsInfinite(this)) return true;
            if (GetCount(def) <= 0) return false;
            _counts[def]--;
            if (notify) OnInventoryChanged?.Invoke();
            return true;
        }

        public bool CanUseItem(ItemDefinition def) =>
            (InfiniteItems || GetCount(def) > 0) && !IsDisabled(def);

        // ---------------- Attacks ----------------

        public bool CanAttack(AttackDefinition def) =>
            (InfiniteAttacks || (AttacksRemaining > 0 && GetCount(def) > 0)) && !IsDisabled(def);

        public AttackResult ResolveAttack(AttackDefinition def, Vector2Int anchor, int variantIndex)
        {
            if (!CanAttack(def)) return null;

            var hits = def.ResolveCells(_state, anchor, variantIndex);
            var result = new AttackResult();
            result.Cells.AddRange(hits.Select(h => h.Cell));

            // First (highest-priority) hit cell touching each enemy decides
            // its damage percent — hits are already in part-priority order.
            var hitPercents = new Dictionary<int, float>();
            foreach (var hit in hits)
                foreach (var en in _state.EnemiesAt(hit.Cell.x, hit.Cell.y))
                    if (!hitPercents.ContainsKey(en.Id)) hitPercents[en.Id] = hit.DamageFactor;

            // Soul cloud halo: an un-hit enemy counts as hit if any halo cell
            // is in the attack's cell set (earliest such cell sets the percent).
            foreach (var en in _state.AllEnemies)
            {
                if (hitPercents.ContainsKey(en.Id)) continue;
                if (!en.HasStatus<SoulCloudStatus>()) continue;
                var halo = new HashSet<Vector2Int>(HaloCells(_state, en));
                foreach (var hit in hits)
                    if (halo.Contains(hit.Cell)) { hitPercents[en.Id] = hit.DamageFactor; break; }
            }

            // Bomb-priority rule (mirrors the JS game): a direct hit on any
            // voiding enemy destroys every hit bomb outright and voids the
            // rest of the attack — no other enemy takes damage. The attack
            // and its charge are still consumed below.
            var bombIdsHit = hitPercents.Keys
                .Where(id => _state.TryGetEnemy(id, out var en) && en.Rules.VoidsAttackOnHit)
                .ToList();

            if (bombIdsHit.Count > 0)
            {
                result.VoidedByBomb = true;
                foreach (var id in bombIdsHit)
                {
                    if (_state.TryGetEnemy(id, out var bomb))
                    {
                        bomb.SetHP(0);
                        bomb.Rules.HandleZeroHp(_state, bomb);
                    }
                    result.HitEnemyIds.Add(id);
                    result.KilledEnemyIds.Add(id);
                }
            }
            else
            {
                float baseDamage = def.BaseDamage * DamageMultiplier();
                foreach (var kv in hitPercents)
                {
                    if (!_state.TryGetEnemy(kv.Key, out var target)) continue;
                    // Golem rule: an absorber linking the target soaks the
                    // damage, recomputed against ITS multipliers (JS parity).
                    var recipient = target.ResolveDamageRecipient(_state);
                    int rawDamage = Mathf.RoundToInt(baseDamage * kv.Value * recipient.DamageTakenMultiplier());
                    int dmg = -recipient.ChangeHP(-rawDamage);
                    bool died = recipient.HP <= 0 && recipient.Rules.HandleZeroHp(_state, recipient);
                    if (recipient.Id != target.Id) _state.NotifyDamageRedirected(target, recipient);
                    result.HitEnemyIds.Add(kv.Key);
                    result.DamageDealt.TryGetValue(recipient.Id, out var prior);
                    result.DamageDealt[recipient.Id] = prior + dmg;
                    if (died && !result.KilledEnemyIds.Contains(recipient.Id))
                        result.KilledEnemyIds.Add(recipient.Id);
                }
            }

            // Removal itself is owned by AbilityTriggerDispatcher, which
            // waits for a dying enemy's OnCritical abilities (if any) to
            // finish before taking it off the board — see CombatRules.
            // HandleZeroHp / AbilityTriggerDispatcher.HandleWentCritical.

            if (!InfiniteAttacks)
            {
                AttacksRemaining--;
                Consume(def, notify: false); // AttacksRemaining is attack-only; the shared count still lives on def
            }
            OnInventoryChanged?.Invoke();
            OnAttackResolved?.Invoke(result);
            return result;
        }

        // ---------------- Footprint helpers ----------------

        public static List<Vector2Int> FootprintCells(GridState s, Enemy en)
        {
            var cells = new List<Vector2Int>();
            foreach (var off in en.BodyCells)
                cells.Add(new Vector2Int(s.Wrap(en.Anchor.x + off.x, s.Rows), s.Wrap(en.Anchor.y + off.y, s.Cols)));
            return cells;
        }

        /// 8-neighborhood ring around the footprint (wrapped), excluding it.
        public static List<Vector2Int> HaloCells(GridState s, Enemy en)
        {
            var footprint = new HashSet<Vector2Int>(FootprintCells(s, en));
            var halo = new HashSet<Vector2Int>();
            foreach (var cell in footprint)
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        var n = new Vector2Int(s.Wrap(cell.x + dr, s.Rows), s.Wrap(cell.y + dc, s.Cols));
                        if (!footprint.Contains(n)) halo.Add(n);
                    }
            return halo.ToList();
        }

        public static List<Vector2Int> FootprintAndHaloCells(GridState s, Enemy en)
        {
            var cells = FootprintCells(s, en);
            cells.AddRange(HaloCells(s, en));
            return cells;
        }
    }
}
