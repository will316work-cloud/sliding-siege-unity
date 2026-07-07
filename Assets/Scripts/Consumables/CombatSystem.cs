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
        private readonly Dictionary<AttackKind, int> _charges = new Dictionary<AttackKind, int>();
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

        public void SetupAttack(AttackDefinition def) => _charges[def.Kind] = def.StartingCharges;
        public void SetupItem(ItemDefinition def) => _itemCounts[def.Kind] = def.StartingCount;

        public int GetCharges(AttackKind kind) => _charges.TryGetValue(kind, out var v) ? v : 0;
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
            InfiniteAttacks || (AttacksRemaining > 0 && GetCharges(def.Kind) > 0);

        public AttackResult ResolveAttack(AttackDefinition def, Vector2Int anchor, int variantIndex)
        {
            if (!CanAttack(def)) return null;

            var resolver = AttackShapeResolverFactory.Get(def.Kind);
            var result = new AttackResult { Cells = resolver.GetCells(_state, anchor, variantIndex) };

            var cellSet = new HashSet<Vector2Int>(result.Cells);
            var hitIds = new HashSet<int>();
            foreach (var cell in result.Cells)
                foreach (var en in _state.EnemiesAt(cell.x, cell.y))
                    hitIds.Add(en.Id);

            // Soul cloud halo: an un-hit enemy counts as hit if any halo cell
            // is in the attack's cell set.
            foreach (var en in _state.Enemies.Values)
            {
                if (hitIds.Contains(en.Id)) continue;
                if (!en.Statuses.OfType<SoulCloudStatus>().Any()) continue;
                if (HaloCells(_state, en).Any(cellSet.Contains)) hitIds.Add(en.Id);
            }

            int baseDamage = Mathf.RoundToInt(def.BaseDamage * DamageMultiplier());
            foreach (var id in hitIds)
            {
                if (!_state.Enemies.TryGetValue(id, out var en)) continue;
                int dmg = Mathf.RoundToInt(baseDamage * en.DamageTakenMultiplier());
                en.HP -= dmg;
                result.HitEnemyIds.Add(id);
                result.DamageDealt[id] = dmg;
                if (en.HP <= 0) result.KilledEnemyIds.Add(id);
            }

            foreach (var id in result.KilledEnemyIds)
                _state.RemoveEnemy(id);

            if (!InfiniteAttacks)
            {
                AttacksRemaining--;
                _charges[def.Kind] = GetCharges(def.Kind) - 1;
            }
            OnInventoryChanged?.Invoke();
            OnAttackResolved?.Invoke(result);
            return result;
        }

        // ---------------- Footprint helpers ----------------

        public static List<Vector2Int> FootprintCells(GridState s, Enemy en)
        {
            var cells = new List<Vector2Int>();
            for (int i = 0; i < en.SizeRows; i++)
                for (int j = 0; j < en.SizeCols; j++)
                    cells.Add(new Vector2Int(s.Wrap(en.Anchor.x + i, s.Rows), s.Wrap(en.Anchor.y + j, s.Cols)));
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
