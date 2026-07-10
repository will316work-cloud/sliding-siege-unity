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

        public CombatSystem(GridState state) { _state = state; }

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
            InfiniteItems || GetItemCount(def.Kind) > 0;

        // ---------------- Attacks ----------------

        public bool CanAttack(AttackDefinition def) =>
            InfiniteAttacks || (AttacksRemaining > 0 && GetCharges(def) > 0);

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
                    if (!_state.Enemies.TryGetValue(kv.Key, out var en)) continue;
                    int dmg = Mathf.RoundToInt(baseDamage * kv.Value * en.DamageTakenMultiplier());
                    en.HP -= dmg;
                    result.HitEnemyIds.Add(kv.Key);
                    result.DamageDealt[kv.Key] = dmg;
                    if (en.HP <= 0) result.KilledEnemyIds.Add(kv.Key);
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
