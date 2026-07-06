using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Full row or full column through the anchor.
    public class AxeShapeResolver : IAttackShapeResolver
    {
        public int VariantCount => 2;
        public string VariantLabel(int v) => v == 0 ? "Row" : "Column";

        public List<Vector2Int> GetCells(GridState state, Vector2Int anchor, int v)
        {
            var cells = new List<Vector2Int>();
            if (v == 0) for (int c = 0; c < state.Cols; c++) cells.Add(new Vector2Int(anchor.x, c));
            else        for (int r = 0; r < state.Rows; r++) cells.Add(new Vector2Int(r, anchor.y));
            return cells;
        }
    }
}
