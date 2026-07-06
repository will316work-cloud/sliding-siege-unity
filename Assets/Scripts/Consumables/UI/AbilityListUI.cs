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

        private void EnsurePool()
        {
            _pool ??= new ObjectPool<AbilityCardUI>(
                createFunc: () => Instantiate(cardPrefab, container),
                actionOnGet: c => { c.gameObject.SetActive(true); c.transform.SetParent(container, false); },
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
                int charges = combat.GetCharges(def.Kind);
                if (charges <= 0) continue;
                int totalDmg = Mathf.RoundToInt(def.BaseDamage * combat.DamageMultiplier());
                var card = _pool.Get();
                var captured = def;
                card.Setup(def.Icon, def.DisplayName, charges, totalDmg + " dmg",
                    selected == def, combat.CanAttack(def), () => onClick(captured));
                _active.Add(card);
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
                if (count <= 0) continue;
                var card = _pool.Get();
                var captured = def;
                card.Setup(def.Icon, def.DisplayName, count, null,
                    selected == def, true, () => onClick(captured));
                _active.Add(card);
            }
        }

        public void Clear()
        {
            foreach (var c in _active) _pool.Release(c);
            _active.Clear();
        }
    }
}
