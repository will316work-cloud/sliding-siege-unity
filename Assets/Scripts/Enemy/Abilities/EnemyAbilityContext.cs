using System.Collections;
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

        public EnemyAbilityContext(Enemy owner, GridState state, EnemyViewManager views, MonoBehaviour host)
        {
            Owner = owner;
            State = state;
            Views = views;
            Host = host;
        }

        /// Plays an AnimationCaller preset on the owner's main piece and
        /// waits for it to complete. Yields nothing (no wait) if the label
        /// is empty or the piece has no AnimationCaller.
        public IEnumerator PlayOwnerPresetAndWait(string presetLabel) =>
            PlayOwnerPresetAndWait(presetLabel, 1f);

        /// As above, with the preset's speed multiplied by speedScale
        /// (e.g. clipLength / desiredDuration to fit a real-time window).
        public IEnumerator PlayOwnerPresetAndWait(string presetLabel, float speedScale)
        {
            if (string.IsNullOrEmpty(presetLabel)) yield break;
            if (!Views.TryGetMainPiece(Owner.Id, out var piece)) yield break;
            var caller = piece.AnimationCaller;
            if (caller == null) yield break;

            bool done = false;
            caller.PlayPreset(presetLabel, speedScale, () => done = true);
            while (!done) yield return null;
        }
    }
}
