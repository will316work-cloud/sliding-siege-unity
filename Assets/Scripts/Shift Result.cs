using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Result of a single shift, consumed by the view layer to animate.
    public class ShiftResult
    {
        public bool IsRowShift;                       // false => column shift
        public int Direction;                         // +1 or -1
        public HashSet<int> ShiftedLines = new HashSet<int>();
        public Dictionary<int, Vector2Int> OldAnchors = new Dictionary<int, Vector2Int>(); // enemyId -> anchor before shift
        public List<int> MovedEnemyIds = new List<int>();
    }
}
