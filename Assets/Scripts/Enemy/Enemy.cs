using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Runtime enemy instance. Anchor is the top-left cell of its footprint
    /// (always normalized into [0,rows) x [0,cols); the footprint may wrap).
    public class Enemy
    {
        public int Id;
        public EnemyDefinition Definition;
        public Vector2Int Anchor;          // (row, col) => (x = row, y = col)
        public int SizeRows => Definition.SizeRows;
        public int SizeCols => Definition.SizeCols;

        public int HP;
        public bool IsDead => HP <= 0;
        public readonly List<StatusEffect> Statuses = new List<StatusEffect>();

        /// Product of all status damage-taken multipliers.
        public float DamageTakenMultiplier()
        {
            float m = 1f;
            foreach (var s in Statuses) m *= s.DamageTakenMultiplier;
            return m;
        }

        public bool HasStatus<T>() where T : StatusEffect => Statuses.OfType<T>().Any();
    }
}
