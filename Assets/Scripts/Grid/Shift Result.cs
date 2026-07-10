using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Result of a single shift, consumed by the view layer to animate.
    /// GridState fills the collections while performing the shift.
    public class ShiftResult
    {
        public ShiftResult(bool isRowShift, int direction, HashSet<int> shiftedLines)
        {
            IsRowShift = isRowShift;
            Direction = direction;
            ShiftedLines = shiftedLines ?? new HashSet<int>();
        }

        public bool IsRowShift { get; }                 // false => column shift
        public int Direction { get; }                   // +1 or -1
        public HashSet<int> ShiftedLines { get; }
        public Dictionary<int, Vector2Int> OldAnchors { get; } = new Dictionary<int, Vector2Int>(); // enemyId -> anchor before shift
        public List<int> MovedEnemyIds { get; } = new List<int>();
    }
}
