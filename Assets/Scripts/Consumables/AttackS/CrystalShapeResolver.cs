using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Target tile plus 2 tiles outward in a + or X pattern.
    public class CrystalShapeResolver : IAttackShapeResolver
    {
        private static readonly Vector2Int[] Plus = { new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1) };
        private static readonly Vector2Int[] Cross = { new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1) };

        public int VariantCount => 2;
        public string VariantLabel(int v) => v == 0 ? "+ Shape" : "X Shape";

        public List<Vector2Int> GetCells(GridState state, Vector2Int anchor, int v)
        {
            var cells = new List<Vector2Int>();
            bool In(int r, int c) => r >= 0 && r < state.Rows && c >= 0 && c < state.Cols;
            if (In(anchor.x, anchor.y)) cells.Add(anchor);
            var dirs = v == 0 ? Plus : Cross;
            foreach (var d in dirs)
                for (int k = 1; k <= 2; k++)
                {
                    int r = anchor.x + d.x * k, c = anchor.y + d.y * k;
                    if (In(r, c)) cells.Add(new Vector2Int(r, c));
                }
            return cells;
        }
    }
}
