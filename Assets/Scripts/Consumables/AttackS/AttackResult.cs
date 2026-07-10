using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Outcome of one resolved attack, for UI/animation consumers.
    public class AttackResult
    {
        public List<Vector2Int> Cells = new List<Vector2Int>();
        public List<int> HitEnemyIds = new List<int>();
        public Dictionary<int, int> DamageDealt = new Dictionary<int, int>();
        public List<int> KilledEnemyIds = new List<int>();

        /// True when a bomb (VoidsAttackOnHit enemy) was hit directly:
        /// the bomb was destroyed and every other enemy was spared.
        public bool VoidedByBomb;
    }
}
