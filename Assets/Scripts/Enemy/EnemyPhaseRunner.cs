using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Test driver for the enemy phase: collects every (enemy, ability)
    /// pair, sorts by ability OrderIndex (ties broken by enemy spawn id),
    /// and executes them as coroutines. Each ability's serialized postDelay
    /// runs ONLY when the ability reports success. Hook RunEnemyPhase() to
    /// a temporary End Turn button.
    public class EnemyPhaseRunner : MonoBehaviour
    {
        public bool IsRunning { get; private set; }

        public event Action OnPhaseStarted;
        public event Action OnPhaseFinished;

        private GridState _state;
        private EnemyViewManager _views;

        public void Initialize(GridState state, EnemyViewManager views)
        {
            _state = state;
            _views = views;
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

            // Snapshot the pairs up front (spawned-mid-phase enemies act
            // next phase; removed enemies are re-checked before executing).
            var pairs = _state.Enemies.Values
                .SelectMany(en => (en.Definition.Abilities ?? new List<EnemyAbility>())
                    .Where(a => a != null)
                    .Select(a => (ability: a, enemy: en)))
                .OrderByDescending(p => p.ability.OrderIndex)
                .ThenBy(p => p.enemy.Id)
                .ToList();

            foreach (var (ability, enemy) in pairs)
            {
                if (!_state.Enemies.ContainsKey(enemy.Id)) continue; // died mid-phase
                if (!enemy.CanAct) continue;                          // stunned etc.

                var ctx = new EnemyAbilityContext(enemy, _state, _views, this);
                var result = new AbilityResult();
                yield return StartCoroutine(ability.Execute(ctx, result));

                // Wait out any death sequences the ability caused before
                // moving on (deaths block game flow until finished).
                while (_views != null && _views.IsAnimating) yield return null;

                if (result.Success && ability.PostDelay > 0f)
                    yield return new WaitForSeconds(ability.PostDelay);
            }

            IsRunning = false;
            OnPhaseFinished?.Invoke();
        }
    }
}
