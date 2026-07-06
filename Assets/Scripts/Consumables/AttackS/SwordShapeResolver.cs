using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Full diagonal line through the anchor, edge to edge (↗ or ↘).
    public class SwordShapeResolver : IAttackShapeResolver
    {
        public int VariantCount => 2;
        public string VariantLabel(int v) => v == 0 ? "\u2197 Diagonal" : "\u2198 Diagonal";

        public List<Vector2Int> GetCells(GridState state, Vector2Int anchor, int v)
        {
            var cells = new List<Vector2Int>();
            int stepR = v == 0 ? -1 : 1;
            int maxK = Mathf.Max(state.Rows, state.Cols);
            for (int k = -maxK; k <= maxK; k++)
            {
                int r = anchor.x + stepR * k, c = anchor.y + k;
                if (r >= 0 && r < state.Rows && c >= 0 && c < state.Cols)
                    cells.Add(new Vector2Int(r, c));
            }
            return cells;
        }
    }
}
