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
        [SerializeField] private AbilityHighlightOverlay highlightOverlay;
        [SerializeField] private Button confirmButton;              // Confirm button prefab instance
        [SerializeField] private TextMeshProUGUI confirmLabel;      // optional

        [Header("Highlight appearance")]
        [Tooltip("Cell colors/sprites live on each HitboxPart; only the anchor marker and the part-less fallback stay here.")]
        [SerializeField] private Color anchorCellColor = new Color(1f, 0.9f, 0.2f, 0.6f);
        [SerializeField] private Sprite anchorCellSprite;
        [Tooltip("Used for preview cells whose hitbox part is missing.")]
        [SerializeField] private Color fallbackCellColor = new Color(1f, 1f, 1f, 0.4f);

        [Header("Debug toggles (runtime-updatable)")]
        [Tooltip("Using an attack consumes no charges/uses; attack cards never disable.")]
        [SerializeField] private bool infiniteAttacks;
        [Tooltip("Using an item consumes no count; item cards never disable.")]
        [SerializeField] private bool infiniteItems;

        [Header("Events")]
        public EnemyTappedEvent OnEnemyTapped = new EnemyTappedEvent();

        private GridState _state;
        private EnemyPhaseRunner _phaseRunner;
        private CombatSystem _combat;

        private AttackDefinition _selectedAttack;
        private int _variantIndex;
        private Vector2Int? _anchor;

        private ItemDefinition _selectedItem;
        private Vector2Int? _itemFirst;
        private Vector2Int? _itemSecond;

        public bool IsTargeting => _selectedAttack != null || _selectedItem != null;
        public CombatSystem Combat => _combat;

        public void Initialize(GridState state, EnemyPhaseRunner phaseRunner)
        {
            _state = state;
            _phaseRunner = phaseRunner;
            _combat = new CombatSystem(state)
            {
                InfiniteAttacks = infiniteAttacks,
                InfiniteItems = infiniteItems,
            };
            foreach (var def in attackDefinitions) _combat.SetupAttack(def);
            foreach (var def in itemDefinitions) _combat.SetupItem(def);
            if (_phaseRunner != null) _phaseRunner.Combat = _combat;
            _combat.OnInventoryChanged += RefreshLists;

            // Enemy telegraphs (stored hitboxes) re-render on any board change.
            _state.OnEnemySpawned += HandleEnemyEvent;
            _state.OnEnemyRemoved += HandleEnemyEvent;
            _state.OnEnemyMoved += HandleEnemyMoved;
            _state.OnEnemyResized += HandleEnemyEvent;
            _state.OnShifted += HandleShifted;
            _state.OnRebuilt += RefreshHighlights;
            _state.OnEnemyHitboxChanged += HandleEnemyEvent;
            if (_phaseRunner != null) _phaseRunner.OnPhaseFinished += RefreshHighlights;

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
            if (!toggleOff && _combat.CanUseItem(def)) _selectedItem = def;
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
                    _variantIndex = (_variantIndex + 1) % _selectedAttack.VariantCount;
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
                if (!effect.CanApply(_state, _selectedItem, _combat, _itemFirst, _itemSecond)) return;
                if (!effect.Apply(_state, _selectedItem, _combat, _itemFirst, _itemSecond, out var message))
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
            var highlights = new List<(Vector2Int, Color, Sprite)>();

            // Enemy telegraphs go on their own root BEHIND the Enemy Layer.
            // Same-definition enemies share one claim set so intersecting
            // cells render once; different definitions overlay one another.
            var telegraphs = new List<(Vector2Int, Color, Sprite)>();
            var claimedByDef = new Dictionary<EnemyDefinition, HashSet<Vector2Int>>();
            foreach (var en in _state.Enemies.Values.OrderBy(e => e.Id))
            {
                if (en.QueuedHitbox == null) continue;
                if (!claimedByDef.TryGetValue(en.Definition, out var claimed))
                    claimedByDef[en.Definition] = claimed = new HashSet<Vector2Int>();
                foreach (var hit in en.QueuedHitbox.Resolve(_state, en.Anchor))
                    if (claimed.Add(hit.Cell))
                        telegraphs.Add((hit.Cell, PartColor(hit.Part), hit.Part?.HighlightSprite));
            }

            if (_selectedAttack != null && _anchor != null)
            {
                foreach (var hit in _selectedAttack.ResolveCells(_state, _anchor.Value, _variantIndex))
                    highlights.Add((hit.Cell, PartColor(hit.Part), hit.Part?.HighlightSprite));
                highlights.Add((_anchor.Value, anchorCellColor, anchorCellSprite)); // anchor drawn last, on top
            }
            else if (_selectedItem != null)
            {
                var effect = ItemEffectFactory.Get(_selectedItem.Kind);
                foreach (var hit in effect.PreviewCells(_state, _selectedItem, _itemFirst, _itemSecond))
                    highlights.Add((hit.Cell, PartColor(hit.Part), hit.Part?.HighlightSprite));
                // Every selected cell gets the anchor marker, drawn last so
                // it sits on top — even when the effect previews nothing.
                if (_itemFirst != null) highlights.Add((_itemFirst.Value, anchorCellColor, anchorCellSprite));
                if (_itemSecond != null) highlights.Add((_itemSecond.Value, anchorCellColor, anchorCellSprite));
            }

            highlightOverlay.SetHighlights(highlights, telegraphs);
        }

        private Color PartColor(HitboxPart part) => part != null ? part.HighlightColor : fallbackCellColor;

        private void HandleEnemyEvent(Enemy _) => RefreshHighlights();
        private void HandleEnemyMoved(Enemy _, Vector2Int __, MoveStyle ___) => RefreshHighlights();
        private void HandleShifted(ShiftResult _) => RefreshHighlights();

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnEnemySpawned -= HandleEnemyEvent;
                _state.OnEnemyRemoved -= HandleEnemyEvent;
                _state.OnEnemyMoved -= HandleEnemyMoved;
                _state.OnEnemyResized -= HandleEnemyEvent;
                _state.OnShifted -= HandleShifted;
                _state.OnRebuilt -= RefreshHighlights;
                _state.OnEnemyHitboxChanged -= HandleEnemyEvent;
            }
            if (_phaseRunner != null) _phaseRunner.OnPhaseFinished -= RefreshHighlights;
        }

        /// Inspector toggle changes apply at runtime (deferred to Update —
        /// list rebuilds touch the hierarchy, which OnValidate must not).
        private void OnValidate()
        {
            if (!Application.isPlaying || _combat == null) return;
            _pendingValidate = true;
        }

        private bool _pendingValidate;

        private void Update()
        {
            if (!_pendingValidate) return;
            _pendingValidate = false;
            _combat.InfiniteAttacks = infiniteAttacks;
            _combat.InfiniteItems = infiniteItems;
            RefreshAll();
        }

        private void RefreshConfirm()
        {
            bool ready = false;
            string label = "Confirm";
            if (_selectedAttack != null)
            {
                ready = _anchor != null && _combat.CanAttack(_selectedAttack);
                label = _selectedAttack.DisplayName + " (" + _selectedAttack.VariantLabel(_variantIndex) + ")";
            }
            else if (_selectedItem != null)
            {
                var effect = ItemEffectFactory.Get(_selectedItem.Kind);
                ready = effect.CanApply(_state, _selectedItem, _combat, _itemFirst, _itemSecond);
                label = _selectedItem.DisplayName;
            }
            confirmButton.gameObject.SetActive(IsTargeting);
            confirmButton.interactable = ready;
            if (confirmLabel != null) confirmLabel.text = label;
        }
    }
}
