using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Drives the enemy phase as a live queue of (ability, enemy) actions
    /// sorted by DESCENDING OrderIndex (ties broken by enemy spawn id;
    /// runner-owned abilities first). The serialized spawn abilities are
    /// runner-owned (no owner enemy) and join every phase at their order
    /// index. Enemies spawned MID-phase send their abilities into the
    /// running enumeration: only those at or below the current order index
    /// (not yet passed) are inserted; earlier ones are skipped this phase.
    /// Each ability's postDelay runs ONLY when it reports success.
    public class EnemyPhaseRunner : MonoBehaviour
    {
        [Header("Phase-start abilities")]
        [Tooltip("Runner-owned abilities (no owner enemy) inserted into EVERY phase at their order index — e.g. wave/initial spawns.")]
        [SerializeField] private List<EnemyAbility> spawnAbilities = new List<EnemyAbility>();

        public bool IsRunning { get; private set; }

        /// Player combat/inventory, wired by TargetingController so
        /// abilities can disable attacks/items through the context.
        public CombatSystem Combat { get; set; }

        /// Event-trigger dispatcher, wired by SlidingGridController. The
        /// runner flushes it between ability steps so mid-phase triggers
        /// (crits, deaths from blasts) run before the next enemy acts.
        public AbilityTriggerDispatcher TriggerDispatcher { get; set; }

        public event Action OnPhaseStarted;
        public event Action OnPhaseFinished;

        private GridState _state;
        private EnemyViewManager _views;

        private readonly List<(EnemyAbility ability, Enemy enemy)> _queue = new List<(EnemyAbility, Enemy)>();
        private int _currentOrderIndex;

        public void Initialize(GridState state, EnemyViewManager views)
        {
            _state = state;
            _views = views;
            _state.OnEnemySpawned += HandleEnemySpawned;
        }

        private void OnDestroy()
        {
            if (_state != null) _state.OnEnemySpawned -= HandleEnemySpawned;
        }

        /// Public entry point (wire to a button). Ignored while running.
        public void RunEnemyPhase()
        {
            if (IsRunning || _state == null) return;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            IsRunning = true;
            OnPhaseStarted?.Invoke();

            _queue.Clear();
            foreach (var en in _state.Enemies.Values)
            {
                if (en.Definition.Abilities == null) continue;
                foreach (var ability in en.Definition.Abilities)
                    if (ability != null && ability.Trigger == AbilityTrigger.EnemyPhase)
                        _queue.Add((ability, en));
            }
            foreach (var ability in spawnAbilities)
                if (ability != null) _queue.Add((ability, null));
            _queue.Sort((a, b) => a.ability.OrderIndex != b.ability.OrderIndex
                ? b.ability.OrderIndex.CompareTo(a.ability.OrderIndex)
                : (a.enemy?.Id ?? int.MinValue).CompareTo(b.enemy?.Id ?? int.MinValue));

            while (_queue.Count > 0)
            {
                var (ability, enemy) = _queue[0];
                _queue.RemoveAt(0);
                _currentOrderIndex = ability.OrderIndex;

                if (enemy != null)
                {
                    if (!_state.Enemies.ContainsKey(enemy.Id)) continue; // died mid-phase
                    if (!enemy.CanAct) continue;                          // stunned etc.
                }

                var ctx = new EnemyAbilityContext(enemy, _state, _views, this, Combat);
                var result = new AbilityResult();
                yield return StartCoroutine(ability.Execute(ctx, result));

                // Wait out any death sequences the ability caused before
                // moving on (deaths block game flow until finished).
                while (_views != null && _views.IsAnimating) yield return null;

                if (result.Success && ability.PostDelay > 0f)
                    yield return new WaitForSeconds(ability.PostDelay);

                if (TriggerDispatcher != null)
                    yield return TriggerDispatcher.Flush();
            }

            // Expire turn-limited statuses (permanent ones are negative).
            foreach (var en in _state.Enemies.Values)
                for (int i = en.Statuses.Count - 1; i >= 0; i--)
                {
                    var status = en.Statuses[i];
                    if (status.TurnsRemaining < 0) continue;
                    status.TurnsRemaining--;
                    if (status.TurnsRemaining <= 0) en.Statuses.RemoveAt(i);
                }

            IsRunning = false;
            OnPhaseFinished?.Invoke();
        }

        /// A mid-phase newcomer joins the running enumeration with only the
        /// abilities the phase hasn't passed yet (OrderIndex at or below the
        /// currently executing one), inserted in sorted position.
        private void HandleEnemySpawned(Enemy en)
        {
            if (!IsRunning || en.Definition.Abilities == null) return;
            foreach (var ability in en.Definition.Abilities)
            {
                if (ability == null || ability.Trigger != AbilityTrigger.EnemyPhase) continue;
                if (ability.OrderIndex > _currentOrderIndex) continue;
                int i = 0;
                while (i < _queue.Count && _queue[i].ability.OrderIndex >= ability.OrderIndex) i++;
                _queue.Insert(i, (ability, en));
            }
        }
    }
}
