using System.Collections;
using UnityEngine;

namespace SlidingSiege
{
    /// Base class for enemy abilities: ScriptableObject assets assigned to
    /// EnemyDefinition.Abilities. The enemy phase runner executes all
    /// (enemy, ability) pairs sorted by OrderIndex (ties broken by enemy
    /// spawn id). Assets are SHARED — keep no per-enemy state in fields;
    /// use the context instead.
    public abstract class EnemyAbility : ScriptableObject
    {
        [Header("Sequencing")]
        [Tooltip("EnemyPhase runs through the phase runner at the order index below; every other trigger runs event-driven at its own time (see AbilityTriggerDispatcher) and ignores the order index.")]
        [SerializeField] private AbilityTrigger trigger = AbilityTrigger.EnemyPhase;

        [Tooltip("EnemyPhase only: abilities across ALL enemies run in DESCENDING order index (higher runs first); ties run in enemy spawn-id order.")]
        [SerializeField] private int orderIndex = 0;

        [Tooltip("Delay (seconds) after this ability, applied ONLY when it succeeds.")]
        [SerializeField, Min(0f)] private float postDelay = 1f;

        [Tooltip("EnemyPhase only: batches with every OTHER queued entry tied at the same order index that ALSO has this on (any owner, any ability asset) — the runner starts all their Execute coroutines the same frame instead of one at a time, then waits for the whole batch (each one's own postDelay, then any animations) before moving to the next order index.")]
        [SerializeField] private bool runSimultaneously = false;

        [Tooltip("AnimationEvent trigger only: the ability runs when an animation event on the owner's piece fires this label (case-insensitive).")]
        [SerializeField] private string animationEventLabel = "";

        public AbilityTrigger Trigger => trigger;
        public int OrderIndex => orderIndex;
        public float PostDelay => postDelay;
        public bool RunSimultaneously => runSimultaneously;
        public string AnimationEventLabel => animationEventLabel;

        /// Coroutine-executed ability. Set result.Success = true if the
        /// ability actually did something (gates the post-delay).
        public abstract IEnumerator Execute(EnemyAbilityContext context, AbilityResult result);
    }
}
