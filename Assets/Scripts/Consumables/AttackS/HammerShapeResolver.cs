using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// 2x2 area anchored at the target; variant rotates the quadrant.
    public class HammerShapeResolver : IAttackShapeResolver
    {
        private static readonly Vector2Int[][] OffsetsByRot =
        {
            new[] { new Vector2Int(0,0), new Vector2Int(0,1),  new Vector2Int(1,0),  new Vector2Int(1,1)  },
            new[] { new Vector2Int(0,0), new Vector2Int(0,-1), new Vector2Int(1,0),  new Vector2Int(1,-1) },
            new[] { new Vector2Int(0,0), new Vector2Int(0,-1), new Vector2Int(-1,0), new Vector2Int(-1,-1)},
            new[] { new Vector2Int(0,0), new Vector2Int(0,1),  new Vector2Int(-1,0), new Vector2Int(-1,1) },
        };

        public int VariantCount => 4;
        public string VariantLabel(int v) => (v * 90) + "\u00B0";

        public List<Vector2Int> GetCells(GridState state, Vector2Int anchor, int v)
        {
            var cells = new List<Vector2Int>();
            foreach (var off in OffsetsByRot[Mathf.Clamp(v, 0, 3)])
            {
                int r = anchor.x + off.x, c = anchor.y + off.y;
                if (r >= 0 && r < state.Rows && c >= 0 && c < state.Cols)
                    cells.Add(new Vector2Int(r, c));
            }
            return cells;
        }
    }
}
