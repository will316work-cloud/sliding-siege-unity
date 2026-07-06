using System;
using UnityEngine;
using UnityEngine.Events;

namespace SlidingSiege
{
    /// Serializable UnityEvent carrying the tapped (row, col) cell.
    [Serializable]
    public class CellTappedEvent : UnityEvent<Vector2Int> { }
}
