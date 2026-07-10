using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace SlidingSiege
{
    /// Procedurally builds the attack or item card list from definitions
    /// (add one instance per list). Cards with 0 charges/count are hidden,
    /// matching the HTML game.
    public class AbilityListUI : MonoBehaviour
    {
        [SerializeField] private AbilityCardUI cardPrefab;
        [SerializeField] private RectTransform container; // has a LayoutGroup

        private ObjectPool<AbilityCardUI> _pool;
        private readonly List<AbilityCardUI> _active = new List<AbilityCardUI>();
        private readonly Dictionary<AttackDefinition, AbilityCardUI> _attackCards = new Dictionary<AttackDefinition, AbilityCardUI>();
        private readonly Dictionary<ItemKind, AbilityCardUI> _itemCards = new Dictionary<ItemKind, AbilityCardUI>();

        /// Live card rect for an attack definition (link display anchoring).
        public bool TryGetAttackCardRect(AttackDefinition def, out RectTransform rect)
        {
            rect = _attackCards.TryGetValue(def, out var card) ? (RectTransform)card.transform : null;
            return rect != null;
        }

        /// Live card rect for an item kind (link display anchoring).
        public bool TryGetItemCardRect(ItemKind kind, out RectTransform rect)
        {
            rect = _itemCards.TryGetValue(kind, out var card) ? (RectTransform)card.transform : null;
            return rect != null;
        }

        private void EnsurePool()
        {
            _pool ??= new ObjectPool<AbilityCardUI>(
                createFunc: () => Instantiate(cardPrefab, container),
                // SetAsLastSibling keeps visual order equal to rebuild order —
                // pooled cards otherwise keep stale sibling indices and the
                // selected card appears to jump to the end of the list.
                actionOnGet: c => { c.gameObject.SetActive(true); c.transform.SetParent(container, false); c.transform.SetAsLastSibling(); },
                actionOnRelease: c => c.gameObject.SetActive(false),
                actionOnDestroy: c => Destroy(c.gameObject),
                defaultCapacity: 8);
        }

        public void RebuildAttacks(IReadOnlyList<AttackDefinition> defs, CombatSystem combat,
            AttackDefinition selected, Action<AttackDefinition> onClick)
        {
            EnsurePool();
            Clear();
            foreach (var def in defs)
            {
                int charges = combat.GetCharges(def);
                if (!combat.InfiniteAttacks && charges <= 0) continue;
                string countLabel = combat.InfiniteAttacks ? "x∞" : "x" + charges;
                int totalDmg = Mathf.RoundToInt(def.BaseDamage * combat.DamageMultiplier());
                var card = _pool.Get();
                var captured = def;
                card.Setup(def.Icon, def.DisplayName, countLabel, totalDmg + " dmg",
                    selected == def, combat.CanAttack(def), () => onClick(captured));
                _active.Add(card);
                _attackCards[def] = card;
            }
        }

        public void RebuildItems(IReadOnlyList<ItemDefinition> defs, CombatSystem combat,
            ItemDefinition selected, Action<ItemDefinition> onClick)
        {
            EnsurePool();
            Clear();
            foreach (var def in defs)
            {
                int count = combat.GetItemCount(def.Kind);
                if (!combat.InfiniteItems && count <= 0) continue;
                string countLabel = combat.InfiniteItems ? "x∞" : "x" + count;
                var card = _pool.Get();
                var captured = def;
                card.Setup(def.Icon, def.DisplayName, countLabel, null,
                    selected == def, combat.CanUseItem(captured), () => onClick(captured));
                _active.Add(card);
                _itemCards[def.Kind] = card;
            }
        }

        public void Clear()
        {
            foreach (var c in _active) _pool.Release(c);
            _active.Clear();
            _attackCards.Clear();
            _itemCards.Clear();
        }
    }
}
