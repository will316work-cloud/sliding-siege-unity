using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Pool;

namespace SlidingSiege
{
    [Serializable] public class AbilityDefinitionEvent : UnityEvent<AbilityDefinition> { }

    /// Procedurally builds a card list from its own serialized definitions
    /// (add one instance per list — attacks, items, whatever else derives
    /// AbilityDefinition). Cards with 0 charges/count are hidden, matching
    /// the HTML game.
    ///
    /// Owns its own selection: clicking a card arms it (toggles off if it
    /// was already selected; arming a card that CanUse() says no to is a
    /// no-op). TargetingController just listens to OnSelectionChanged and
    /// coordinates cross-list exclusivity (an attack and an item can't
    /// both be armed) via ClearSelection.
    public class AbilityListUI : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private AbilityDefinition[] definitions;

        [Header("Wiring")]
        [SerializeField] private AbilityCardUI cardPrefab;
        [SerializeField] private RectTransform container; // has a LayoutGroup

        [Header("Events")]
        [Tooltip("Raised with the new selection (null when cleared/toggled off) whenever it changes.")]
        public AbilityDefinitionEvent OnSelectionChanged = new AbilityDefinitionEvent();

        public IReadOnlyList<AbilityDefinition> Definitions => definitions;
        public AbilityDefinition Selected { get; private set; }

        private CombatSystem _combat;
        private ObjectPool<AbilityCardUI> _pool;
        private readonly List<AbilityCardUI> _active = new List<AbilityCardUI>();
        private readonly Dictionary<AbilityDefinition, AbilityCardUI> _cards = new Dictionary<AbilityDefinition, AbilityCardUI>();

        /// Registers every one of this list's definitions with combat
        /// (AbilityDefinition.Setup — polymorphic, so it doesn't care which
        /// concrete type they are). Call once at startup.
        public void SetupAll(CombatSystem combat)
        {
            foreach (var def in definitions) def.Setup(combat);
        }

        /// Live card rect for one of this list's definitions (link display
        /// anchoring).
        public bool TryGetCardRect(AbilityDefinition def, out RectTransform rect)
        {
            rect = def != null && _cards.TryGetValue(def, out var card) ? (RectTransform)card.transform : null;
            return rect != null;
        }

        /// Clears this list's selection without toggling anything — e.g.
        /// because a sibling list (attacks vs items) just armed something
        /// instead. No-op (and no event) if nothing was selected.
        public void ClearSelection()
        {
            if (Selected == null) return;
            Selected = null;
            OnSelectionChanged.Invoke(null);
            Rebuild(_combat);
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

        /// Rebuilds the visible cards against `combat` (cached for the next
        /// click or ClearSelection call). Call whenever anything that
        /// affects display — inventory, damage multiplier, disables —
        /// changes.
        public void Rebuild(CombatSystem combat)
        {
            _combat = combat;
            EnsurePool();
            Clear();
            foreach (var def in definitions)
            {
                if (def == null) continue;
                bool infinite = def.IsInfinite(combat);
                int count = def.GetCount(combat);
                if (!infinite && count <= 0) continue;
                string countLabel = infinite ? "x∞" : "x" + count;
                var card = _pool.Get();
                var captured = def;
                card.Setup(def.Icon, def.DisplayName, countLabel, def.DamageLabel(combat),
                    Selected == def, def.CanUse(combat), () => HandleCardClicked(captured));
                _active.Add(card);
                _cards[def] = card;
            }
        }

        private void HandleCardClicked(AbilityDefinition def)
        {
            bool toggleOff = Selected == def;
            Selected = (!toggleOff && def.CanUse(_combat)) ? def : null;
            OnSelectionChanged.Invoke(Selected);
            Rebuild(_combat);
        }

        public void Clear()
        {
            foreach (var c in _active) _pool.Release(c);
            _active.Clear();
            _cards.Clear();
        }
    }
}
