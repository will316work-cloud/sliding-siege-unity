using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Outcome of one resolved attack, for UI/animation consumers.
    /// CombatSystem fills the collections while resolving.
    public class AttackResult
    {
        public List<Vector2Int> Cells { get; } = new List<Vector2Int>();
        public List<int> HitEnemyIds { get; } = new List<int>();
        public Dictionary<int, int> DamageDealt { get; } = new Dictionary<int, int>();
        public List<int> KilledEnemyIds { get; } = new List<int>();

        /// True when a bomb (VoidsAttackOnHit enemy) was hit directly:
        /// the bomb was destroyed and every other enemy was spared.
        public bool VoidedByBomb { get; set; }
    }
}
