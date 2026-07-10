using System;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("BodyCells")]
        [SerializeField] private Vector2Int[] bodyCells = { Vector2Int.zero };

        [Header("Visuals while this shape is active")]
        [Tooltip("Null keeps the definition's sprite.")]
        [FormerlySerializedAs("Sprite")]
        [SerializeField] private Sprite sprite;
        [Tooltip("How the Image rect is sized: stretched to the footprint bounding box, scaled relative to it, or fixed pixels.")]
        [FormerlySerializedAs("SizeMode")]
        [SerializeField] private VisualSizeMode sizeMode = VisualSizeMode.StretchToFootprint;
        [Tooltip("FootprintScale mode: multiplier per axis (1,1 = exact bounding box).")]
        [FormerlySerializedAs("FootprintScale")]
        [SerializeField] private Vector2 footprintScale = Vector2.one;
        [Tooltip("FixedPixels mode: absolute width/height in pixels.")]
        [FormerlySerializedAs("FixedPixelSize")]
        [SerializeField] private Vector2 fixedPixelSize = new Vector2(72f, 72f);
        [Header("Stretch padding (StretchToFootprint only, pixels)")]
        [FormerlySerializedAs("PaddingLeft")]
        [SerializeField, Min(0f)] private float paddingLeft;
        [FormerlySerializedAs("PaddingRight")]
        [SerializeField, Min(0f)] private float paddingRight;
        [FormerlySerializedAs("PaddingTop")]
        [SerializeField, Min(0f)] private float paddingTop;
        [FormerlySerializedAs("PaddingBottom")]
        [SerializeField, Min(0f)] private float paddingBottom;
        [Space]
        [Tooltip("Pixel offset of the visual from the bounding box's top-left (+x right, +y down).")]
        [FormerlySerializedAs("VisualOffset")]
        [SerializeField] private Vector2 visualOffset = Vector2.zero;

        public Vector2Int[] BodyCells => bodyCells;
        public Sprite Sprite => sprite;
        public VisualSizeMode SizeMode => sizeMode;

        /// Final visual rect size for a given footprint pixel size.
        public Vector2 VisualSize(Vector2 footprintSizePx) => sizeMode switch
        {
            VisualSizeMode.FootprintScale => Vector2.Scale(footprintSizePx, footprintScale),
            VisualSizeMode.FixedPixels => fixedPixelSize,
            _ => footprintSizePx - new Vector2(paddingLeft + paddingRight, paddingTop + paddingBottom),
        };

        /// Pixel offset (anchored-position space: +x right, -y down) placing
        /// the visual on the footprint before VisualOffset: stretched rects
        /// sit at (left, top) padding; other modes are centered.
        public Vector2 VisualAnchorOffset(Vector2 footprintSizePx)
        {
            if (sizeMode == VisualSizeMode.StretchToFootprint)
                return new Vector2(paddingLeft + visualOffset.x, -(paddingTop + visualOffset.y));
            Vector2 size = VisualSize(footprintSizePx);
            Vector2 centered = (footprintSizePx - size) * 0.5f;
            return new Vector2(centered.x + visualOffset.x, -(centered.y + visualOffset.y));
        }
    }
}
