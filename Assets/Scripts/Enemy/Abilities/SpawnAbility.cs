using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Spawns other enemies. Flow (per your spec): gather valid empty spawn
    /// cells first; if none, fail silently (no roll, no delay); otherwise
    /// roll the chance once, and on success spawn up to `count` enemies at
    /// randomly-picked valid cells (revalidated after each spawn).
    [CreateAssetMenu(menuName = "SlidingSiege/Abilities/Spawn")]
    public class SpawnAbility : EnemyAbility
    {
        [Header("Spawn")]
        [SerializeField] private EnemyDefinition spawnDefinition;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField, Range(0f, 1f)] private float chance = 0.5f;
        [SerializeField] private SpawnPlacementMode placement = SpawnPlacementMode.AdjacentToOwner;

        [Header("Population limits (all-or-nothing: exceeding a cap spawns nothing)")]
        [Tooltip("Max living enemies of the SPAWNED definition allowed after this spawn; if `count` would push past it, nothing spawns. 0 = unlimited.")]
        [SerializeField, Min(0)] private int maxOfDefinition = 0;
        [Tooltip("Only spawn while the board's TOTAL living enemy count is at least this.")]
        [SerializeField, Min(0)] private int minTotalEnemies = 0;
        [Tooltip("Max TOTAL living enemies allowed after this spawn; if `count` would push past it, nothing spawns. 0 = unlimited.")]
        [SerializeField, Min(0)] private int maxTotalEnemies = 0;

        [Header("Animation")]
        [Tooltip("Optional AnimationCaller preset on the OWNER's piece, played (and awaited) before spawning.")]
        [SerializeField] private string spawnAnimationPreset = "";

        public override IEnumerator Execute(EnemyAbilityContext ctx, AbilityResult result)
        {
            if (spawnDefinition == null) yield break;
            if (!WithinPopulationLimits(ctx)) yield break; // capped: no roll, no delay

            var candidates = GatherCandidates(ctx);
            if (candidates.Count == 0) yield break;      // no room: no roll
            if (Random.value >= chance) yield break;      // roll failed

            yield return ctx.PlayOwnerPresetAndWait(spawnAnimationPreset);

            int spawned = 0;
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                var cell = candidates[Random.Range(0, candidates.Count)];
                if (ctx.State.CanPlaceAt(cell.x, cell.y, spawnDefinition))
                {
                    ctx.State.SpawnEnemy(spawnDefinition, cell.x, cell.y);
                    spawned++;
                }
                // Revalidate: earlier spawns may block remaining candidates.
                candidates = GatherCandidates(ctx);
            }

            result.Success = spawned > 0;
        }

        private bool WithinPopulationLimits(EnemyAbilityContext ctx)
        {
            int total = ctx.State.Enemies.Count;
            if (total < minTotalEnemies) return false;
            if (maxTotalEnemies > 0 && total + count > maxTotalEnemies) return false;
            if (maxOfDefinition > 0)
            {
                int sameDefinition = 0;
                foreach (var en in ctx.State.Enemies.Values)
                    if (en.Definition == spawnDefinition) sameDefinition++;
                if (sameDefinition + count > maxOfDefinition) return false;
            }
            return true;
        }

        private List<Vector2Int> GatherCandidates(EnemyAbilityContext ctx)
        {
            var s = ctx.State;
            var cells = new List<Vector2Int>();

            if (placement == SpawnPlacementMode.AdjacentToOwner)
            {
                foreach (var cell in CombatSystem.HaloCells(s, ctx.Owner))
                    if (s.CanPlaceAt(cell.x, cell.y, spawnDefinition))
                        cells.Add(cell);
            }
            else
            {
                for (int r = 0; r < s.Rows; r++)
                    for (int c = 0; c < s.Cols; c++)
                        if (s.CanPlaceAt(r, c, spawnDefinition))
                            cells.Add(new Vector2Int(r, c));
            }
            return cells;
        }
    }
}
