using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// 1-tile-thick square ring around the anchor (center not hit);
    /// variant cycles radius 1..3.
    public class RingShapeResolver : IAttackShapeResolver
    {
        public int VariantCount => 3;
        public string VariantLabel(int v) => "Radius " + (v + 1);

        public List<Vector2Int> GetCells(GridState state, Vector2Int anchor, int v)
        {
            int rad = v + 1;
            var cells = new List<Vector2Int>();
            for (int dr = -rad; dr <= rad; dr++)
                for (int dc = -rad; dc <= rad; dc++)
                {
                    if (Mathf.Max(Mathf.Abs(dr), Mathf.Abs(dc)) != rad) continue;
                    int r = anchor.x + dr, c = anchor.y + dc;
                    if (r >= 0 && r < state.Rows && c >= 0 && c < state.Cols)
                        cells.Add(new Vector2Int(r, c));
                }
            return cells;
        }
    }
}
