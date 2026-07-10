using System.Collections;
using UnityEngine;

namespace SlidingSiege
{
    /// Ghost/Phantom movement: instead of stepping, the owner teleports to
    /// a uniformly random anchor cell each enemy phase. Occupancy is NOT
    /// checked — transparent enemies may stack on other occupants (cells
    /// hold occupant lists), matching the JS "any cell is available" rule.
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Teleport Self")]
    public class TeleportSelfAbility : EnemyAbility
    {
        [Header("Teleport")]
        [Tooltip("Chance to teleport at all this phase.")]
        [SerializeField, Range(0f, 1f)] private float chance = 1f;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset played (and awaited) on the owner after the snap.")]
        [SerializeField] private string teleportAnimationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            var owner = ctx.Owner;
            if (owner == null || Random.value >= chance) yield break;

            var s = ctx.State;
            var dest = new Vector2Int(Random.Range(0, s.Rows), Random.Range(0, s.Cols));
            s.MoveEnemy(owner.Id, dest, MoveStyle.Instant);
            result.Success = true;

            yield return ctx.PlayOwnerPresetAndWait(teleportAnimationPreset);
        }
    }
}
