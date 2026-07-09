using System;
using UnityEngine;

namespace SlidingSiege
{
    /// A body arrangement: occupied cells (offsets from the bounding box's
    /// top-left, normalized so min row = min col = 0 — the anchor may be an
    /// empty cell for shapes like an L) plus the sprite and visual rect used
    /// while this shape is active. Applied as a runtime override via
    /// GridState.ReshapeEnemy (see ChangeShapeAbility).
    [Serializable]
    public class EnemyShape
    {
        [Tooltip("Occupied cells, offsets from the bounding box top-left (x = row, y = col); min row/col should be 0. Non-rectangular shapes (L, X, ...) allowed.")]
        public Vector2Int[] BodyCells = { Vector2Int.zero };

        [Header("Visuals while this shape is active")]
        [Tooltip("Null keeps the definition's sprite.")]
        public Sprite Sprite;
        [Tooltip("How the Image rect is sized: stretched to the footprint bounding box, scaled relative to it, or fixed pixels.")]
        public VisualSizeMode SizeMode = VisualSizeMode.StretchToFootprint;
        [Tooltip("FootprintScale mode: multiplier per axis (1,1 = exact bounding box).")]
        public Vector2 FootprintScale = Vector2.one;
        [Tooltip("FixedPixels mode: absolute width/height in pixels.")]
        public Vector2 FixedPixelSize = new Vector2(72f, 72f);
        [Header("Stretch padding (StretchToFootprint only, pixels)")]
        [Min(0f)] public float PaddingLeft;
        [Min(0f)] public float PaddingRight;
        [Min(0f)] public float PaddingTop;
        [Min(0f)] public float PaddingBottom;
        [Space]
        [Tooltip("Pixel offset of the visual from the bounding box's top-left (+x right, +y down).")]
        public Vector2 VisualOffset = Vector2.zero;

        /// Final visual rect size for a given footprint pixel size.
        public Vector2 VisualSize(Vector2 footprintSizePx) => SizeMode switch
        {
            VisualSizeMode.FootprintScale => Vector2.Scale(footprintSizePx, FootprintScale),
            VisualSizeMode.FixedPixels => FixedPixelSize,
            _ => footprintSizePx - new Vector2(PaddingLeft + PaddingRight, PaddingTop + PaddingBottom),
        };

        /// Pixel offset (anchored-position space: +x right, -y down) placing
        /// the visual on the footprint before VisualOffset: stretched rects
        /// sit at (left, top) padding; other modes are centered.
        public Vector2 VisualAnchorOffset(Vector2 footprintSizePx)
        {
            if (SizeMode == VisualSizeMode.StretchToFootprint)
                return new Vector2(PaddingLeft + VisualOffset.x, -(PaddingTop + VisualOffset.y));
            Vector2 size = VisualSize(footprintSizePx);
            Vector2 centered = (footprintSizePx - size) * 0.5f;
            return new Vector2(centered.x + VisualOffset.x, -(centered.y + VisualOffset.y));
        }
    }
}
