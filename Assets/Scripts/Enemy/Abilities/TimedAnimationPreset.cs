using System;
using System.Collections;
using UnityEngine;

namespace SlidingSiege
{
    /// A named AnimationCaller preset paired with a speed-scale ratio,
    /// authored once and shared by SpawnAbility (its owner's cast clip) and
    /// EnemyDefinition (Hurt/Death/Idle). ReferenceClipLength is a ratio
    /// against the clip's original authored length: 1 = normal speed,
    /// 2 = twice as fast (half the length), 0.5 = half speed (twice the
    /// length).
    [Serializable]
    public class TimedAnimationPreset
    {
        [SerializeField] private string presetLabel = "";
        [Tooltip("Speed multiplier ratio applied against the clip's original (authored) length: 1 = normal speed, 2 = twice as fast, 0.5 = half speed.")]
        [SerializeField, Min(0.01f)] private float referenceClipLength = 1f;

        public string PresetLabel => presetLabel;
        public float ReferenceClipLength => referenceClipLength;

        /// Plays this preset (if labeled) on the owner's main piece, scaled
        /// by ReferenceClipLength, and waits for it to complete. Yields
        /// nothing (and invokes onDone immediately) if unlabeled.
        public IEnumerator PlayAnim(EnemyAbilityContext ctx, Action onDone)
        {
            if (string.IsNullOrEmpty(presetLabel))
            {
                onDone?.Invoke();
                yield break;
            }
            yield return ctx.PlayOwnerPresetAndWait(presetLabel, referenceClipLength);
            onDone?.Invoke();
        }
    }
}
