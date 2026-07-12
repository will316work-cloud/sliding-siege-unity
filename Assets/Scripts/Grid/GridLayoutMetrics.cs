using System;
using UnityEngine;

namespace SlidingSiege
{
    /// Pixel metrics shared by the grid builder and enemy views.
    [Serializable]
    public class GridLayoutMetrics
    {
        [Tooltip("Cell edge length in pixels.")]
        [SerializeField] private float cellSize = 72f;
        [SerializeField] private float spacing = 4f;

        public float CellSize => cellSize;
        public float Spacing => spacing;

        /// Top-left offset (in pixels, +x right / +y down) of the centered
        /// cell content inside the Enemy Layer's stretched rect. Set by
        /// GridUIBuilder.Build().
        public Vector2 ContentOffset { get; set; }

        public float Stride => cellSize + spacing;
        public Vector2 GridPixelSize(int rows, int cols) => new Vector2(
            cols * cellSize + (cols - 1) * spacing,
            rows * cellSize + (rows - 1) * spacing);
    }
}
