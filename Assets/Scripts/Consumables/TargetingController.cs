using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Owns attack/item selection and targeting (the JS game's
    /// targetingMode/itemTargetingMode + confirm flow):
    ///  - card click arms an attack or item (click again to disarm),
    ///  - taps set the target; re-tapping an attack's anchor cycles its
    ///    variant; Teleport takes an enemy then a destination,
    ///  - affected cells are tinted; Confirm commits,
    ///  - taps with nothing armed raise OnEnemyTapped (tool-tip later).
    /// While anything is armed, IsTargeting locks grid dragging.
    public class TargetingController : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private AttackDefinition[] attackDefinitions;
        [SerializeField] private ItemDefinition[] itemDefinitions;

        [Header("UI wiring")]
        [SerializeField] private AbilityListUI attackList;
        [SerializeField] private AbilityListUI itemList;
        [SerializeField] private CellHighlighter cellHighlighter;
        [SerializeField] private Button confirmButton;              // Confirm button prefab instance
        [SerializeField] private TextMeshProUGUI confirmLabel;      // optional

        [Header("Highlight colors")]
        [SerializeField] private Color attackCellColor = new Color(1f, 0.4f, 0.3f, 1f);
        [SerializeField] private Color anchorCellColor = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color itemCellColor = new Color(0.4f, 0.7f, 1f, 1f);

        [Header("Events")]
        public EnemyTappedEvent OnEnemyTapped = new EnemyTappedEvent();

        private GridState _state;
        private CombatSystem _combat;

        private AttackDefinition _selectedAttack;
        private int _variantIndex;
        private Vector2Int? _anchor;

        private ItemDefinition _selectedItem;
        private Vector2Int? _itemFirst;
        private Vector2Int? _itemSecond;

        public bool IsTargeting => _selectedAttack != null || _selectedItem != null;
        public CombatSystem Combat => _combat;

        public void Initialize(GridState state)
        {
            _state = state;
            _combat = new CombatSystem(state);
            foreach (var def in attackDefinitions) _combat.SetupAttack(def);
            foreach (var def in itemDefinitions) _combat.SetupItem(def);
            _combat.OnInventoryChanged += RefreshLists;

            confirmButton.onClick.AddListener(Confirm);
            ClearSelection();
            RefreshLists();
        }

        // ---------------- Card clicks ----------------

        private void SelectAttack(AttackDefinition def)
        {
            bool toggleOff = _selectedAttack == def;
            ClearSelection();
            if (!toggleOff && _combat.CanAttack(def)) _selectedAttack = def;
            RefreshAll();
        }

        private void SelectItem(ItemDefinition def)
        {
            bool toggleOff = _selectedItem == def;
            ClearSelection();
            if (!toggleOff && _combat.GetItemCount(def.Kind) > 0) _selectedItem = def;
            RefreshAll();
        }

        // ---------------- Grid taps ----------------

        /// Wire this to GridDragInput.OnCellTapped.
        public void HandleCellTapped(Vector2Int cell)
        {
            if (_selectedAttack != null)
            {
                if (_anchor == cell)
                {
                    var resolver = AttackShapeResolverFactory.Get(_selectedAttack.Kind);
                    _variantIndex = (_variantIndex + 1) % resolver.VariantCount;
                }
                else
                {
                    _anchor = cell;
                    _variantIndex = 0;
                }
                RefreshAll();
                return;
            }

            if (_selectedItem != null)
            {
                var effect = ItemEffectFactory.Get(_selectedItem.Kind);
                switch (effect.Targeting)
                {
                    case ItemTargeting.None:
                        break;
                    case ItemTargeting.Cell:
                    case ItemTargeting.Enemy:
                        _itemFirst = cell;
                        break;
                    case ItemTargeting.EnemyThenCell:
                        if (_itemFirst == null || cell == _itemFirst ||
                            !_state.EnemiesAt(_itemFirst.Value.x, _itemFirst.Value.y).Any())
                        {
                            _itemFirst = cell;
                            _itemSecond = null;
                        }
                        else _itemSecond = cell;
                        break;
                }
                RefreshAll();
                return;
            }

            // Nothing armed: enemy tap = future tool-tip.
            var enemy = _state.EnemiesAt(cell.x, cell.y).FirstOrDefault();
            if (enemy != null)
            {
                Debug.Log($"[SlidingSiege] Enemy tapped for tool-tip: id={enemy.Id} " +
                          $"({enemy.Definition.name}) HP={enemy.HP}/{enemy.Definition.MaxHP} at {cell}");
                OnEnemyTapped?.Invoke(enemy);
            }
        }

        // ---------------- Confirm ----------------

        private void Confirm()
        {
            if (_selectedAttack != null && _anchor != null)
            {
                var result = _combat.ResolveAttack(_selectedAttack, _anchor.Value, _variantIndex);
                if (result != null)
                {
                    ClearSelection();
                    RefreshAll();
                }
                return;
            }

            if (_selectedItem != null)
            {
                var effect = ItemEffectFactory.Get(_selectedItem.Kind);
                if (!effect.CanApply(_state, _combat, _itemFirst, _itemSecond)) return;
                if (!effect.Apply(_state, _combat, _itemFirst, _itemSecond, out var message))
                {
                    Debug.Log("[SlidingSiege] " + message);
                    return;
                }
                Debug.Log("[SlidingSiege] " + message);
                _combat.ConsumeItem(_selectedItem.Kind);
                ClearSelection();
                RefreshAll();
            }
        }

        // ---------------- Refresh ----------------

        private void ClearSelection()
        {
            _selectedAttack = null;
            _anchor = null;
            _variantIndex = 0;
            _selectedItem = null;
            _itemFirst = null;
            _itemSecond = null;
        }

        private void RefreshAll()
        {
            RefreshLists();
            RefreshHighlights();
            RefreshConfirm();
        }

        private void RefreshLists()
        {
            attackList.RebuildAttacks(attackDefinitions, _combat, _selectedAttack, SelectAttack);
            itemList.RebuildItems(itemDefinitions, _combat, _selectedItem, SelectItem);
        }

        private void RefreshHighlights()
        {
            var highlights = new List<(Vector2Int, Color)>();
            if (_selectedAttack != null && _anchor != null)
            {
                var resolver = AttackShapeResolverFactory.Get(_selectedAttack.Kind);
                foreach (var cell in resolver.GetCells(_state, _anchor.Value, _variantIndex))
                    highlights.Add((cell, attackCellColor));
                highlights.Add((_anchor.Value, anchorCellColor)); // anchor tint wins
            }
            else if (_selectedItem != null)
            {
                var effect = ItemEffectFactory.Get(_selectedItem.Kind);
                foreach (var cell in effect.PreviewCells(_state, _itemFirst, _itemSecond))
                    highlights.Add((cell, itemCellColor));
            }
            cellHighlighter.SetHighlights(highlights);
        }

        private void RefreshConfirm()
        {
            bool ready = false;
            string label = "Confirm";
            if (_selectedAttack != null)
            {
                ready = _anchor != null && _combat.CanAttack(_selectedAttack);
                var resolver = AttackShapeResolverFactory.Get(_selectedAttack.Kind);
                label = _selectedAttack.DisplayName + " (" + resolver.VariantLabel(_variantIndex) + ")";
            }
            else if (_selectedItem != null)
            {
                var effect = ItemEffectFactory.Get(_selectedItem.Kind);
                ready = effect.CanApply(_state, _combat, _itemFirst, _itemSecond);
                label = _selectedItem.DisplayName;
            }
            confirmButton.gameObject.SetActive(IsTargeting);
            confirmButton.interactable = ready;
            if (confirmLabel != null) confirmLabel.text = label;
        }
    }
}
