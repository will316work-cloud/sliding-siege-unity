using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Everything an ability needs at execution time. Abilities are shared
    /// ScriptableObject assets and must keep NO per-enemy state — all
    /// per-execution data flows through this context.
    public class EnemyAbilityContext
    {
        public Enemy Owner { get; }
        public GridState State { get; }
        public EnemyViewManager Views { get; }
        /// Host MonoBehaviour for nested coroutines (the phase runner).
        public MonoBehaviour Host { get; }
        /// Player combat/inventory; null until TargetingController wires it
        /// into the phase runner (abilities must null-check).
        public CombatSystem Combat { get; }

        public EnemyAbilityContext(Enemy owner, GridState state, EnemyViewManager views, MonoBehaviour host, CombatSystem combat = null)
        {
            Owner = owner;
            State = state;
            Views = views;
            Host = host;
            Combat = combat;
        }

        /// Distinct enemies inside the owner's stored hitbox (set by
        /// SetHitboxAbility), resolved at the owner's current anchor.
        /// Empty when no hitbox is stored.
        public List<Enemy> QueuedHitboxTargets()
        {
            var targets = new List<Enemy>();
            if (Owner == null || !Owner.TryResolveQueuedHitbox(State, out var hits)) return targets;
            var seen = new HashSet<int>();
            foreach (var hit in hits)
                foreach (var en in State.EnemiesAt(hit.Cell.x, hit.Cell.y))
                    if (seen.Add(en.Id)) targets.Add(en);
            return targets;
        }

        /// Plays an AnimationCaller preset on the owner's main piece and
        /// waits for it to complete, then resumes Idle (replacing the
        /// Animator's old exit-time transitions back to Idle). Yields
        /// nothing (no wait, no Idle resume) if the label is empty or the
        /// piece has no AnimationCaller.
        public IEnumerator PlayOwnerPresetAndWait(string presetLabel) =>
            PlayOwnerPresetAndWait(presetLabel, 1f);

        /// As above, with the preset's speed multiplied by speedScale
        /// (e.g. clipLength / desiredDuration to fit a real-time window).
        public IEnumerator PlayOwnerPresetAndWait(string presetLabel, float speedScale)
        {
            if (Owner == null) yield break; // runner-owned ability: no piece
            if (string.IsNullOrEmpty(presetLabel)) yield break;
            if (!Views.TryGetMainPiece(Owner.Id, out var piece)) yield break;
            var caller = piece.AnimationCaller;
            if (caller == null) yield break;

            bool done = false;
            caller.PlayPreset(presetLabel, speedScale, () => done = true);
            while (!done) yield return null;

            // Fire-and-forget so this never recurses back through
            // PlayOwnerPresetAndWait (Idle resuming Idle would loop forever).
            // Critical enemies sit at 0 HP (IsDead) but still rest on their
            // critical/idle preset while they remain on the board.
            var resting = Owner.Definition != null ? Owner.Definition.RestingPresetFor(Owner) : null;
            bool onBoard = State == null || State.ContainsEnemy(Owner.Id);
            if ((!Owner.IsDead || Owner.PendingDetonation) && onBoard
                && resting != null && !string.IsNullOrEmpty(resting.PresetLabel))
                caller.PlayPreset(resting.PresetLabel, resting.ReferenceClipLength);
        }
    }
}
