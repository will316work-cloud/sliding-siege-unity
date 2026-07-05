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
    }
}
