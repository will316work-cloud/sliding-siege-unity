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
    }
}
