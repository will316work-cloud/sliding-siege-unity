using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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
        public CombatSystem Combat { get; private set; }
        public void AttachCombat(CombatSystem combat) => Combat = combat;

        /// Event-trigger dispatcher, wired by SlidingGridController. The
        /// runner flushes it between ability steps so mid-phase triggers
        /// (crits, deaths from blasts) run before the next enemy acts.
        public AbilityTriggerDispatcher TriggerDispatcher { get; private set; }
        public void AttachTriggerDispatcher(AbilityTriggerDispatcher dispatcher) => TriggerDispatcher = dispatcher;

        [Header("Events")]
        public UnityEvent OnPhaseStarted = new UnityEvent();
        public UnityEvent OnPhaseFinished = new UnityEvent();

        private GridState _state;
        private EnemyViewManager _views;

        private readonly List<(EnemyAbility ability, Enemy enemy)> _queue = new List<(EnemyAbility, Enemy)>();
        private int _currentOrderIndex;

        public void Initialize(GridState state, EnemyViewManager views)
        {
            _state = state;
            _views = views;
            _state.OnEnemySpawned += HandleEnemySpawned;
            _state.OnEnemyWentCritical += HandleEnemyWentCritical;
        }

        private void OnDestroy()
        {
            if (_state == null) return;
            _state.OnEnemySpawned -= HandleEnemySpawned;
            _state.OnEnemyWentCritical -= HandleEnemyWentCritical;
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
            OnPhaseStarted.Invoke();

            // Row/column curses from last phase expire before anyone acts.
            _state.TickDisabledLines();

            _queue.Clear();
            foreach (var en in _state.AllEnemies)
                foreach (var ability in en.AbilitiesFor(AbilityTrigger.EnemyPhase))
                    _queue.Add((ability, en));
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
                    if (!_state.ContainsEnemy(enemy.Id)) continue; // died mid-phase
                    if (!enemy.CanAct) continue;                          // stunned etc.
                }

                if (ability.RunSimultaneously)
                {
                    yield return RunSimultaneousBatch(ability, enemy);
                }
                else
                {
                    var ctx = new EnemyAbilityContext(enemy, _state, _views, this, Combat);
                    var result = new AbilityResult();
                    yield return StartCoroutine(ability.Execute(ctx, result));

                    // Wait out any death sequences the ability caused before
                    // moving on (deaths block game flow until finished).
                    while (_views != null && _views.IsAnimating) yield return null;

                    if (result.Success && ability.PostDelay > 0f)
                        yield return new WaitForSeconds(ability.PostDelay);
                }

                if (TriggerDispatcher != null)
                    yield return TriggerDispatcher.Flush();
            }

            // Expire turn-limited statuses (permanent ones are negative).
            foreach (var en in _state.AllEnemies)
                en.TickStatuses();

            IsRunning = false;
            OnPhaseFinished.Invoke();
        }

        /// Gathers `first` plus every other entry still at the FRONT of the
        /// queue tied on the same order index that also has RunSimultaneously
        /// on (any owner, any ability asset — e.g. several Grunts' Kill Self
        /// entries), starts all their Execute coroutines the same frame, and
        /// waits for the whole batch (each entry's own postDelay, then any
        /// resulting animations) before returning. Entries at the same index
        /// WITHOUT the toggle are left in the queue to run one at a time as
        /// usual, right after this batch.
        private IEnumerator RunSimultaneousBatch(EnemyAbility firstAbility, Enemy firstEnemy)
        {
            var batch = new List<(EnemyAbility ability, Enemy enemy)> { (firstAbility, firstEnemy) };
            int orderIndex = firstAbility.OrderIndex;

            while (_queue.Count > 0 && _queue[0].ability.OrderIndex == orderIndex && _queue[0].ability.RunSimultaneously)
            {
                var (ability, enemy) = _queue[0];
                _queue.RemoveAt(0);
                if (enemy != null)
                {
                    if (!_state.ContainsEnemy(enemy.Id)) continue; // died mid-phase
                    if (!enemy.CanAct) continue;                          // stunned etc.
                }
                batch.Add((ability, enemy));
            }

            int pending = batch.Count;
            foreach (var (ability, enemy) in batch)
                StartCoroutine(RunBatchEntry(ability, enemy, () => pending--));
            while (pending > 0) yield return null;

            // Wait out any death sequences the batch caused before moving on
            // (deaths block game flow until finished), same as a solo entry.
            while (_views != null && _views.IsAnimating) yield return null;
        }

        private IEnumerator RunBatchEntry(EnemyAbility ability, Enemy enemy, Action onDone)
        {
            var ctx = new EnemyAbilityContext(enemy, _state, _views, this, Combat);
            var result = new AbilityResult();
            yield return StartCoroutine(ability.Execute(ctx, result));

            if (result.Success && ability.PostDelay > 0f)
                yield return new WaitForSeconds(ability.PostDelay);

            onDone();
        }

        /// A mid-phase newcomer joins the running enumeration with only the
        /// abilities the phase hasn't reached yet (OrderIndex STRICTLY below
        /// the currently executing one), inserted in sorted position. Equal
        /// indices are excluded — a spawn ability whose own definition
        /// re-spawns copies of itself at the same order index (e.g. a
        /// self-duplicating enemy) would otherwise insert its own entry into
        /// the still-running step and chain-replicate every phase instead of
        /// running once per activation.
        private void HandleEnemySpawned(Enemy en)
        {
            if (!IsRunning) return;
            foreach (var ability in en.AbilitiesFor(AbilityTrigger.EnemyPhase))
            {
                if (ability.OrderIndex >= _currentOrderIndex) continue;
                int i = 0;
                while (i < _queue.Count && _queue[i].ability.OrderIndex >= ability.OrderIndex) i++;
                _queue.Insert(i, (ability, en));
            }
        }

        /// An enemy sent critical MID-phase (e.g. caught in another Grunt's
        /// blast) chain-reacts in the SAME phase: any of its EnemyPhase
        /// abilities whose queue entry was already consumed this phase
        /// (its order index has passed) is re-inserted so it runs again now
        /// that its conditions can pass. Entries still waiting in the queue
        /// are left alone. The dispatcher's OnCritical flush (e.g. Set
        /// Hitbox) always runs between steps, before the re-inserted entry.
        private void HandleEnemyWentCritical(Enemy en)
        {
            if (!IsRunning || en.Definition.Abilities == null) return;
            foreach (var ability in en.Definition.Abilities)
            {
                if (ability == null || ability.Trigger != AbilityTrigger.EnemyPhase) continue;
                if (ability.OrderIndex < _currentOrderIndex) continue; // hasn't run yet: still queued below
                if (_queue.Contains((ability, en))) continue;
                int i = 0;
                while (i < _queue.Count && _queue[i].ability.OrderIndex >= ability.OrderIndex) i++;
                _queue.Insert(i, (ability, en));
            }
        }
    }
}
