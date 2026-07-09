using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// One building block of a Hitbox: a row, column, diagonal, or explicit
    /// cell grid, positioned relative to the attack's anchor cell.
    [Serializable]
    public class HitboxPart
    {
        public HitboxPartType Type;

        [Tooltip("Shifts this part's origin away from the tapped anchor cell (x = row, y = col).")]
        public Vector2Int Offset;

        [Tooltip("Grid parts only: affected cells relative to the shifted origin (x = row, y = col).")]
        public Vector2Int[] GridCells = { Vector2Int.zero };

        [Tooltip("Rectangle parts only: extent in (rows, cols); the shifted origin is the top-left cell.")]
        public Vector2Int RectSize = new Vector2Int(1, 1);

        [Tooltip("Rectangle parts only: only the 1-cell-thick border instead of the full area.")]
        public bool PerimeterOnly;

        [Tooltip("Percent of the attack's BaseDamage this part deals.")]
        [Min(0)] public float DamageFactor = 1.0f;

        [Tooltip("Tint used when this part's cells are highlighted on the grid.")]
        public Color HighlightColor = new Color(1f, 0.4f, 0.3f, 0.55f);

        [Tooltip("Optional highlight sprite for this part's cells; null uses the overlay default.")]
        public Sprite HighlightSprite;

        /// In-bounds cells only, no wrap (matches the old shape resolvers).
        public List<Vector2Int> GetCells(GridState state, Vector2Int anchor)
        {
            var cells = new List<Vector2Int>();
            var origin = anchor + Offset;
            bool In(int r, int c) => r >= 0 && r < state.Rows && c >= 0 && c < state.Cols;

            switch (Type)
            {
                case HitboxPartType.Row:
                    if (origin.x >= 0 && origin.x < state.Rows)
                        for (int c = 0; c < state.Cols; c++) cells.Add(new Vector2Int(origin.x, c));
                    break;

                case HitboxPartType.Column:
                    if (origin.y >= 0 && origin.y < state.Cols)
                        for (int r = 0; r < state.Rows; r++) cells.Add(new Vector2Int(r, origin.y));
                    break;

                case HitboxPartType.DiagonalUp:
                case HitboxPartType.DiagonalDown:
                {
                    int stepR = Type == HitboxPartType.DiagonalUp ? -1 : 1;
                    int maxK = Mathf.Max(state.Rows, state.Cols);
                    for (int k = -maxK; k <= maxK; k++)
                    {
                        int r = origin.x + stepR * k, c = origin.y + k;
                        if (In(r, c)) cells.Add(new Vector2Int(r, c));
                    }
                    break;
                }

                case HitboxPartType.Grid:
                    if (GridCells != null)
                        foreach (var off in GridCells)
                        {
                            int r = origin.x + off.x, c = origin.y + off.y;
                            if (In(r, c)) cells.Add(new Vector2Int(r, c));
                        }
                    break;

                case HitboxPartType.Rectangle:
                {
                    int rows = Mathf.Max(1, RectSize.x), cols = Mathf.Max(1, RectSize.y);
                    for (int dr = 0; dr < rows; dr++)
                        for (int dc = 0; dc < cols; dc++)
                        {
                            if (PerimeterOnly && dr != 0 && dr != rows - 1 && dc != 0 && dc != cols - 1)
                                continue;
                            int r = origin.x + dr, c = origin.y + dc;
                            if (In(r, c)) cells.Add(new Vector2Int(r, c));
                        }
                    break;
                }
            }
            return cells;
        }
    }
}
