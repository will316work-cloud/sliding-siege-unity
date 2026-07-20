using System;
using UnityEngine;

namespace SlidingSiege
{
    /// Toggled per-value replication settings for SpawnAbility: each
    /// enabled value is copied from the SOURCE enemy (the ability's owner)
    /// onto the freshly spawned enemy, some scaled by a factor. Skipped
    /// entirely for runner-owned spawn abilities (no owner to copy from).
    [Serializable]
    public class SpawnReplication
    {
        [Header("Health")]
        [Tooltip("Spawn's max HP = source's max HP x factor (min 1). Stored as a per-instance override; health bars re-fit to it.")]
        [SerializeField] private bool copyMaxHP;
        [SerializeField, Min(0f)] private float maxHPFactor = 1f;

        [Tooltip("Spawn's HP = source's current HP x factor, capped at the spawn's max HP. A result at or below 0 is allowed: the spawn immediately dies or goes critical per its combat rules.")]
        [SerializeField] private bool copyCurrentHP;
        [SerializeField, Min(0f)] private float currentHPFactor = 1f;

        [Header("State")]
        [Tooltip("Copy the source's active status effects (own expiry countdowns).")]
        [SerializeField] private bool copyStatuses;

        [Tooltip("Copy the source's runtime shape override, when its body fits at the spawn cell (skipped silently otherwise).")]
        [SerializeField] private bool copyShapeOverride;

        [Tooltip("Copy the source's stored hitbox (Set Hitbox), resolved at the SPAWN's anchor from then on.")]
        [SerializeField] private bool copyQueuedHitbox;

        [Tooltip("Spawn's charge counter = source's counter x factor (rounded, min 0).")]
        [SerializeField] private bool copyChargeCounter;
        [SerializeField, Min(0f)] private float chargeCounterFactor = 1f;

        [Tooltip("A source pending detonation spawns copies that are also pending detonation (at 0 HP, OnCritical abilities fire).")]
        [SerializeField] private bool copyCriticalState;

        /// Copies every enabled value from source onto spawn. May remove the
        /// spawn from the state (replicated HP at or below 0 with
        /// dies-at-zero rules), so callers must not assume it survives.
        public void ApplyTo(Enemy source, Enemy spawn, GridState state)
        {
            if (source == null || spawn == null || state == null) return;

            // Shape first: occupancy/visuals settle before health events.
            if (copyShapeOverride && source.ShapeOverride != null
                && state.CanPlaceBodyAtIgnoring(spawn.Anchor.x, spawn.Anchor.y, source.ShapeOverride.BodyCells, spawn.Id))
                state.ReshapeEnemy(spawn.Id, spawn.Anchor, source.ShapeOverride);

            if (copyMaxHP)
            {
                spawn.OverrideMaxHP(Mathf.RoundToInt(source.MaxHP * maxHPFactor));
                // Without an HP copy below, a rescaled clone is born at its
                // new full health rather than the definition's. Raw: a
                // spawn-time heal must not fire OnHealthGained (floating
                // heal number) as if it were actually healed.
                if (!copyCurrentHP) spawn.SetHpRaw(spawn.MaxHP);
            }

            if (copyCurrentHP)
                SetReplicatedHp(spawn, state, Mathf.RoundToInt(source.HP * currentHPFactor));

            if (copyStatuses)
                foreach (var status in source.Statuses)
                    spawn.AddStatus(status.Clone());

            if (copyQueuedHitbox && source.QueuedHitbox != null)
            {
                spawn.QueueHitbox(source.QueuedHitbox);
                state.NotifyEnemyHitboxChanged(spawn);
            }

            if (copyChargeCounter)
                spawn.SetCharge(Mathf.RoundToInt(source.ChargeCounter * chargeCounterFactor));

            if (copyCriticalState && source.PendingDetonation && !spawn.PendingDetonation)
                SetReplicatedHp(spawn, state, 0);

            // The spawn's health bar bound BEFORE replication ran (its view
            // is created synchronously inside SpawnEnemy, before this method
            // runs) — the silent HP writes above never fired OnHealthChanged,
            // so without this the bar would show full/hidden until the
            // spawn's next real HP change. Skip if replication killed it.
            if (state.ContainsEnemy(spawn.Id)) spawn.RefreshHealthDisplay();
        }

        /// Silently sets the spawn's HP to `target` (capped at its max) — no
        /// OnHealthLost/OnHealthGained, so replication never plays the Hurt
        /// animation, spawns a floating damage/heal number, or fires
        /// OnDamaged-triggered abilities as if the spawn had actually been
        /// hit or healed. Still explicitly resolves Rules.HandleZeroHp when
        /// the result lands at or below 0 (critical for survives-at-zero
        /// rules, removal otherwise) — the same as real damage would.
        private static void SetReplicatedHp(Enemy spawn, GridState state, int target)
        {
            spawn.SetHpRaw(Mathf.Min(spawn.MaxHP, target));
            if (spawn.HP <= 0 && spawn.Rules.HandleZeroHp(state, spawn))
                state.RemoveEnemy(spawn.Id);
        }
    }
}
