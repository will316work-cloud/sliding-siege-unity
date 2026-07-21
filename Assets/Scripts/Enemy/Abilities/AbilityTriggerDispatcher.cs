using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Runs event-triggered enemy abilities (every AbilityTrigger except
    /// EnemyPhase). Events enqueue the owner's matching abilities the moment
    /// they fire — mid-attack, mid-phase, whenever — and a pump coroutine on
    /// the host flushes the queue as soon as it is safe: never while the
    /// enemy phase runner or a previous flush is executing. Player attacks
    /// resolve synchronously within a frame, so their triggers flush the
    /// same frame, right after the attack finishes.
    ///
    /// OnDeath abilities run with the owner already off the board — fine for
    /// spawn/board effects, but owner-piece animations and self-moves no-op.
    public class AbilityTriggerDispatcher
    {
        private readonly GridState _state;
        private readonly EnemyViewManager _views;
        private readonly EnemyPhaseRunner _phaseRunner;
        private readonly CombatSystem _combat;
        private readonly MonoBehaviour _host;

        private readonly List<(EnemyAbility ability, Enemy enemy, AbilityTrigger trigger)> _queue
            = new List<(EnemyAbility, Enemy, AbilityTrigger)>();
        private readonly Dictionary<int, System.Action<int>> _damageHooks
            = new Dictionary<int, System.Action<int>>();
        private bool _flushing;

        /// Dies-at-zero enemies (CombatRules.DiesAtZeroHP) waiting on their
        /// own queued OnCritical abilities to finish before removal — see
        /// HandleWentCritical/Flush.
        private readonly HashSet<int> _pendingZeroHpRemoval = new HashSet<int>();

        public AbilityTriggerDispatcher(GridState state, EnemyViewManager views,
            EnemyPhaseRunner phaseRunner, CombatSystem combat, MonoBehaviour host)
        {
            _state = state;
            _views = views;
            _phaseRunner = phaseRunner;
            _combat = combat;
            _host = host;

            _state.OnEnemySpawned += HandleSpawned;
            _state.OnEnemyRemoved += HandleRemoved;
            _state.OnEnemyWentCritical += HandleWentCritical;
            if (_views != null) _views.OnPieceAbilityEvent += HandleAnimationAbilityEvent;
            foreach (var en in _state.AllEnemies) HookDamage(en);

            _host.StartCoroutine(Pump());
        }

        /// Animation event on an enemy's main piece: execute the owner's
        /// matching AnimationEvent abilities IMMEDIATELY (not queued) so the
        /// effect lands on the exact clip frame — e.g. the Grunt's blast on
        /// its death clip's explosion frame. Runs even for a removed owner,
        /// like OnDeath rattles.
        private void HandleAnimationAbilityEvent(Enemy en, string label)
        {
            if (en?.Definition?.Abilities == null || string.IsNullOrEmpty(label)) return;
            foreach (var ability in en.Definition.Abilities)
                if (ability != null && ability.Trigger == AbilityTrigger.AnimationEvent
                    && string.Equals(ability.AnimationEventLabel, label, System.StringComparison.OrdinalIgnoreCase))
                    _host.StartCoroutine(ExecuteImmediate(ability, en));
        }

        private IEnumerator ExecuteImmediate(EnemyAbility ability, Enemy enemy)
        {
            var ctx = new EnemyAbilityContext(enemy, _state, _views, _host, _combat);
            yield return _host.StartCoroutine(ability.Execute(ctx, new AbilityResult()));
        }

        /// Queues the enemy's OnCritical abilities. If its rules say it
        /// dies at zero HP, either remove it right away (no OnCritical
        /// abilities to wait for) or mark it for removal once Flush drains
        /// them all.
        private void HandleWentCritical(Enemy en)
        {
            int before = _queue.Count;
            Enqueue(en, AbilityTrigger.OnCritical);
            bool queuedAny = _queue.Count > before;

            if (!en.Rules.DiesAtZeroHP) return;
            if (queuedAny) _pendingZeroHpRemoval.Add(en.Id);
            else _state.RemoveEnemy(en.Id);
        }

        private void HandleSpawned(Enemy en)
        {
            HookDamage(en);
            Enqueue(en, AbilityTrigger.OnSpawn);
        }

        private void HandleRemoved(Enemy en)
        {
            _pendingZeroHpRemoval.Remove(en.Id);
            if (_damageHooks.TryGetValue(en.Id, out var hook))
            {
                en.OnHealthLost -= hook;
                _damageHooks.Remove(en.Id);
            }
            Enqueue(en, AbilityTrigger.OnDeath);

            // Link-break watch: any linker whose LAST living link just died
            // fires its OnLinkBroken abilities (Siren stun).
            foreach (var linker in _state.AllEnemies)
                if (linker.IsLinkedTo(en.Id) && !System.Linq.Enumerable.Any(linker.LivingLinkTargets(_state)))
                    Enqueue(linker, AbilityTrigger.OnLinkBroken);
        }

        private void HookDamage(Enemy en)
        {
            if (_damageHooks.ContainsKey(en.Id)) return;
            System.Action<int> hook = _ => Enqueue(en, AbilityTrigger.OnDamaged);
            _damageHooks[en.Id] = hook;
            en.OnHealthLost += hook;
        }

        private void Enqueue(Enemy en, AbilityTrigger trigger)
        {
            if (en?.Definition?.Abilities == null) return;
            foreach (var ability in en.Definition.Abilities)
                if (ability != null && ability.Trigger == trigger)
                    _queue.Add((ability, en, trigger));
        }

        private IEnumerator Pump()
        {
            while (true)
            {
                if (_queue.Count > 0 && !_flushing
                    && (_phaseRunner == null || !_phaseRunner.IsRunning))
                    yield return Flush();
                yield return null;
            }
        }

        /// Drains the queue. Safe to call any time (no-ops while a flush is
        /// already running); the phase runner calls it between its own
        /// ability steps so mid-phase triggers run before the next enemy
        /// acts, and the pump calls it outside the phase.
        public IEnumerator Flush()
        {
            if (_flushing) yield break;
            _flushing = true;
            while (_queue.Count > 0)
            {
                var (ability, enemy, trigger) = _queue[0];
                _queue.RemoveAt(0);

                // Death rattles run for the removed owner; everything else
                // requires the owner alive (or critical) and able to act.
                if (trigger != AbilityTrigger.OnDeath)
                {
                    if (!_state.ContainsEnemy(enemy.Id)) continue;
                    if (!enemy.CanAct) continue;
                }

                if (ability.RunSimultaneously)
                {
                    yield return RunSimultaneousBatch(ability, enemy, trigger);
                }
                else
                {
                    var ctx = new EnemyAbilityContext(enemy, _state, _views, _host, _combat);
                    var result = new AbilityResult();
                    yield return _host.StartCoroutine(ability.Execute(ctx, result));

                    while (_views != null && _views.IsAnimating) yield return null;

                    if (result.Success && ability.PostDelay > 0f)
                        yield return new WaitForSeconds(ability.PostDelay);

                    TryRemoveAfterCritical(trigger, enemy);
                }
            }
            _flushing = false;
        }

        /// Gathers `first` plus every other entry still at the FRONT of the
        /// queue tied on the same trigger that also has RunSimultaneously
        /// on (any owner, any ability asset — e.g. several enemies' OnCritical
        /// abilities going off together), starts all their Execute coroutines
        /// the same frame, and waits for the whole batch (each entry's own
        /// postDelay, then any resulting animations) before returning.
        /// Entries at the front WITHOUT the toggle are left in the queue to
        /// run one at a time as usual, right after this batch. Mirrors
        /// EnemyPhaseRunner.RunSimultaneousBatch.
        private IEnumerator RunSimultaneousBatch(EnemyAbility firstAbility, Enemy firstEnemy, AbilityTrigger trigger)
        {
            var batch = new List<(EnemyAbility ability, Enemy enemy)> { (firstAbility, firstEnemy) };

            while (_queue.Count > 0 && _queue[0].trigger == trigger && _queue[0].ability.RunSimultaneously)
            {
                var (ability, enemy, _) = _queue[0];
                _queue.RemoveAt(0);
                if (trigger != AbilityTrigger.OnDeath)
                {
                    if (!_state.ContainsEnemy(enemy.Id)) continue;
                    if (!enemy.CanAct) continue;
                }
                batch.Add((ability, enemy));
            }

            int pending = batch.Count;
            foreach (var (ability, enemy) in batch)
                _host.StartCoroutine(RunBatchEntry(ability, enemy, () => pending--));
            while (pending > 0) yield return null;

            // Wait out any death sequences the batch caused before moving on
            // (deaths block game flow until finished), same as a solo entry.
            while (_views != null && _views.IsAnimating) yield return null;

            foreach (var (_, enemy) in batch)
                TryRemoveAfterCritical(trigger, enemy);
        }

        private IEnumerator RunBatchEntry(EnemyAbility ability, Enemy enemy, System.Action onDone)
        {
            var ctx = new EnemyAbilityContext(enemy, _state, _views, _host, _combat);
            var result = new AbilityResult();
            yield return _host.StartCoroutine(ability.Execute(ctx, result));
            if (result.Success && ability.PostDelay > 0f)
                yield return new WaitForSeconds(ability.PostDelay);
            onDone();
        }

        /// Last queued OnCritical ability for a dies-at-zero enemy just
        /// finished — safe to remove it now.
        private void TryRemoveAfterCritical(AbilityTrigger trigger, Enemy enemy)
        {
            if (trigger == AbilityTrigger.OnCritical && _pendingZeroHpRemoval.Contains(enemy.Id)
                && !_queue.Exists(q => q.enemy.Id == enemy.Id && q.trigger == AbilityTrigger.OnCritical))
            {
                _pendingZeroHpRemoval.Remove(enemy.Id);
                _state.RemoveEnemy(enemy.Id);
            }
        }
    }
}
