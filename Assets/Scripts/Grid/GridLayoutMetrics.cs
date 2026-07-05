using System;
using UnityEngine;

namespace SlidingSiege
{
    /// Pixel metrics shared by the grid builder and enemy views.
    [Serializable]
    public class GridLayoutMetrics
    {
        [Tooltip("Computed at build time from the stretched Grid Panel rect.")]
        public float CellSize = 72f;
        public float Spacing = 4f;
        /// Top-left offset (in pixels, +x right / +y down) of the centered
        /// cell content inside the Enemy Layer's stretched rect. Set by
        /// GridUIBuilder.Build().
        [NonSerialized] public Vector2 ContentOffset;
        public float Stride => CellSize + Spacing;
        public Vector2 GridPixelSize(int rows, int cols) => new Vector2(
            cols * CellSize + (cols - 1) * Spacing,
            rows * CellSize + (rows - 1) * Spacing);
    }
}
