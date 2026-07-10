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
        private readonly Dictionary<AttackDefinition, int> _charges = new Dictionary<AttackDefinition, int>();
        private readonly Dictionary<ItemKind, int> _itemCounts = new Dictionary<ItemKind, int>();

        public int AttacksRemaining = 1;

        /// Debug toggles: when true, using an attack/item consumes nothing
        /// and the ability lists never disable (counts show as infinite).
        public bool InfiniteAttacks;
        public bool InfiniteItems;

        /// Global outgoing damage multiplier hook (damage bonus % later).
        public Func<float> DamageMultiplier = () => 1f;

        public event Action<AttackResult> OnAttackResolved;
        public event Action OnInventoryChanged;

        public CombatSystem(GridState state)
        {
            _state = state;
            // Death breaks any spells the dead enemy had on the player's cards.
            _state.OnEnemyRemoved += en =>
            {
                if (ClearDisablesFrom(en.Id)) OnInventoryChanged?.Invoke();
            };
        }

        // ---------------- Consumable disabling ----------------
        // Enemies (Mage spells, Siren shrieks) can disable attacks/items.
        // Registered per source enemy id so a kill lifts that enemy's
        // disables immediately; sources reroll by clearing then re-adding.

        private readonly Dictionary<int, HashSet<AttackDefinition>> _attackDisables = new Dictionary<int, HashSet<AttackDefinition>>();
        private readonly Dictionary<int, HashSet<ItemKind>> _itemDisables = new Dictionary<int, HashSet<ItemKind>>();

        public void DisableAttack(int sourceEnemyId, AttackDefinition def)
        {
            if (!_attackDisables.TryGetValue(sourceEnemyId, out var set))
                _attackDisables[sourceEnemyId] = set = new HashSet<AttackDefinition>();
            set.Add(def);
            OnInventoryChanged?.Invoke();
        }

        public void DisableItem(int sourceEnemyId, ItemKind kind)
        {
            if (!_itemDisables.TryGetValue(sourceEnemyId, out var set))
                _itemDisables[sourceEnemyId] = set = new HashSet<ItemKind>();
            set.Add(kind);
            OnInventoryChanged?.Invoke();
        }

        public bool ClearDisablesFrom(int sourceEnemyId)
        {
            bool removed = _attackDisables.Remove(sourceEnemyId);
            removed |= _itemDisables.Remove(sourceEnemyId);
            return removed;
        }

        public bool IsAttackDisabled(AttackDefinition def) =>
            _attackDisables.Values.Any(set => set.Contains(def));

        public bool IsItemDisabled(ItemKind kind) =>
            _itemDisables.Values.Any(set => set.Contains(kind));

        /// Attacks a disabling enemy may target: set up and with charges
        /// left (all of them under InfiniteAttacks).
        public IEnumerable<AttackDefinition> AvailableAttacks() =>
            _charges.Where(kv => InfiniteAttacks || kv.Value > 0).Select(kv => kv.Key);

        /// Items a disabling enemy may target: set up and in stock.
        public IEnumerable<ItemKind> AvailableItems() =>
            _itemCounts.Where(kv => InfiniteItems || kv.Value > 0).Select(kv => kv.Key);

        /// All (source enemy, attack) disable pairs, for the link display.
        public IEnumerable<(int SourceId, AttackDefinition Attack)> AttackDisableEntries()
        {
            foreach (var kv in _attackDisables)
                foreach (var def in kv.Value)
                    yield return (kv.Key, def);
        }

        /// All (source enemy, item) disable pairs, for the link display.
        public IEnumerable<(int SourceId, ItemKind Item)> ItemDisableEntries()
        {
            foreach (var kv in _itemDisables)
                foreach (var kind in kv.Value)
                    yield return (kv.Key, kind);
        }

        // ---------------- Inventory ----------------

        public void SetupAttack(AttackDefinition def) => _charges[def] = def.StartingCharges;
        public void SetupItem(ItemDefinition def) => _itemCounts[def.Kind] = def.StartingCount;

        public int GetCharges(AttackDefinition def) => _charges.TryGetValue(def, out var v) ? v : 0;
        public int GetItemCount(ItemKind kind) => _itemCounts.TryGetValue(kind, out var v) ? v : 0;

        public bool ConsumeItem(ItemKind kind)
        {
            if (InfiniteItems) return true;
            if (GetItemCount(kind) <= 0) return false;
            _itemCounts[kind]--;
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool CanUseItem(ItemDefinition def) =>
            (InfiniteItems || GetItemCount(def.Kind) > 0) && !IsItemDisabled(def.Kind);

        // ---------------- Attacks ----------------

        public bool CanAttack(AttackDefinition def) =>
            (InfiniteAttacks || (AttacksRemaining > 0 && GetCharges(def) > 0)) && !IsAttackDisabled(def);

        public AttackResult ResolveAttack(AttackDefinition def, Vector2Int anchor, int variantIndex)
        {
            if (!CanAttack(def)) return null;

            var hits = def.ResolveCells(_state, anchor, variantIndex);
            var result = new AttackResult { Cells = hits.Select(h => h.Cell).ToList() };

            // First (highest-priority) hit cell touching each enemy decides
            // its damage percent — hits are already in part-priority order.
            var hitPercents = new Dictionary<int, float>();
            foreach (var hit in hits)
                foreach (var en in _state.EnemiesAt(hit.Cell.x, hit.Cell.y))
                    if (!hitPercents.ContainsKey(en.Id)) hitPercents[en.Id] = hit.DamageFactor;

            // Soul cloud halo: an un-hit enemy counts as hit if any halo cell
            // is in the attack's cell set (earliest such cell sets the percent).
            foreach (var en in _state.Enemies.Values)
            {
                if (hitPercents.ContainsKey(en.Id)) continue;
                if (!en.Statuses.OfType<SoulCloudStatus>().Any()) continue;
                var halo = new HashSet<Vector2Int>(HaloCells(_state, en));
                foreach (var hit in hits)
                    if (halo.Contains(hit.Cell)) { hitPercents[en.Id] = hit.DamageFactor; break; }
            }

            // Bomb-priority rule (mirrors the JS game): a direct hit on any
            // voiding enemy destroys every hit bomb outright and voids the
            // rest of the attack — no other enemy takes damage. The attack
            // and its charge are still consumed below.
            var bombIdsHit = hitPercents.Keys
                .Where(id => _state.Enemies.TryGetValue(id, out var en) && en.Definition.VoidsAttackOnHit)
                .ToList();

            if (bombIdsHit.Count > 0)
            {
                result.VoidedByBomb = true;
                foreach (var id in bombIdsHit)
                {
                    _state.Enemies[id].HP = 0;
                    result.HitEnemyIds.Add(id);
                    result.KilledEnemyIds.Add(id);
                }
            }
            else
            {
                float baseDamage = def.BaseDamage * DamageMultiplier();
                foreach (var kv in hitPercents)
                {
                    if (!_state.Enemies.TryGetValue(kv.Key, out var target)) continue;
                    // Golem rule: an absorber linking the target soaks the
                    // damage, recomputed against ITS multipliers (JS parity).
                    var recipient = RouteDamage(_state, target);
                    target.PendingHit = true; // slime clusters count absorbed hits too
                    int dmg = Mathf.RoundToInt(baseDamage * kv.Value * recipient.DamageTakenMultiplier());
                    dmg = ClampToSurvivor(recipient, dmg);
                    recipient.HP -= dmg;
                    if (recipient.Id != target.Id) _state.NotifyDamageRedirected(target, recipient);
                    result.HitEnemyIds.Add(kv.Key);
                    result.DamageDealt.TryGetValue(recipient.Id, out var prior);
                    result.DamageDealt[recipient.Id] = prior + dmg;
                    if (recipient.HP <= 0 && HandleZeroHp(_state, recipient) && !result.KilledEnemyIds.Contains(recipient.Id))
                        result.KilledEnemyIds.Add(recipient.Id);
                }
            }

            foreach (var id in result.KilledEnemyIds)
                _state.RemoveEnemy(id);

            if (!InfiniteAttacks)
            {
                AttacksRemaining--;
                _charges[def] = GetCharges(def) - 1;
            }
            OnInventoryChanged?.Invoke();
            OnAttackResolved?.Invoke(result);
            return result;
        }

        // ---------------- Damage routing (shared with abilities) ----------------

        /// The enemy that actually receives damage aimed at `target`: the
        /// first living, non-critical absorber (Golem) linking it, or the
        /// target itself. Absorbers never redirect to one another.
        public static Enemy RouteDamage(GridState s, Enemy target)
        {
            if (!target.Definition.AbsorbsLinkedDamage)
                foreach (var en in s.Enemies.Values)
                    if (en.Definition.AbsorbsLinkedDamage && !en.PendingDetonation && en.LinkedIds.Contains(target.Id))
                        return en;
            return target;
        }

        /// Enemies that survive at 0 HP never drop below it (avoids a
        /// phantom heal indicator when clamping back up); everyone else
        /// takes the full amount.
        public static int ClampToSurvivor(Enemy en, int dmg) =>
            en.Definition.DiesAtZeroHP ? dmg : Mathf.Min(dmg, en.HP);

        /// Call when an enemy is at 0 HP or less. Enemies that don't die at
        /// zero go critical (pending detonation, links dropped,
        /// OnEnemyWentCritical raised) and survive — returns false; returns
        /// true when the enemy really dies.
        public static bool HandleZeroHp(GridState s, Enemy en)
        {
            if (en.Definition.DiesAtZeroHP) return true;
            if (!en.PendingDetonation)
            {
                en.PendingDetonation = true;
                en.LinkedIds.Clear();
                Debug.Log($"[SlidingSiege] {en.Definition.name} is critically damaged and will explode next enemy phase!");
                s.NotifyEnemyWentCritical(en);
            }
            return false;
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
