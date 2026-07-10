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
            _state.OnEnemyWentCritical += en => Enqueue(en, AbilityTrigger.OnCritical);
            foreach (var en in _state.Enemies.Values) HookDamage(en);

            _host.StartCoroutine(Pump());
        }

        private void HandleSpawned(Enemy en)
        {
            HookDamage(en);
            Enqueue(en, AbilityTrigger.OnSpawn);
        }

        private void HandleRemoved(Enemy en)
        {
            if (_damageHooks.TryGetValue(en.Id, out var hook))
            {
                en.OnHealthLost -= hook;
                _damageHooks.Remove(en.Id);
            }
            Enqueue(en, AbilityTrigger.OnDeath);

            // Link-break watch: any linker whose LAST living link just died
            // fires its OnLinkBroken abilities (Siren stun).
            foreach (var linker in _state.Enemies.Values)
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
                    if (!_state.Enemies.ContainsKey(enemy.Id)) continue;
                    if (!enemy.CanAct) continue;
                }

                var ctx = new EnemyAbilityContext(enemy, _state, _views, _host, _combat);
                var result = new AbilityResult();
                yield return _host.StartCoroutine(ability.Execute(ctx, result));

                while (_views != null && _views.IsAnimating) yield return null;

                if (result.Success && ability.PostDelay > 0f)
                    yield return new WaitForSeconds(ability.PostDelay);
            }
            _flushing = false;
        }
    }
}
