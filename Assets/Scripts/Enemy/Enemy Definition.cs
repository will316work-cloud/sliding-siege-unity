using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Authoring data for an enemy type. Size is (rows, cols).
    [CreateAssetMenu(menuName = "SlidingSiege/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Footprint (grid cells)")]
        [Min(1)] public int SizeRows = 1;
        [Min(1)] public int SizeCols = 1;

        [Header("Sprite")]
        public Sprite Sprite;

        [Header("Image settings")]
        public Image.Type ImageType = Image.Type.Simple;
        [Tooltip("Simple/Filled only.")]
        public bool PreserveAspect = false;
        [Tooltip("Sliced/Tiled only.")]
        public float PixelsPerUnitMultiplier = 1f;
        [Tooltip("Sliced only: draw the center region.")]
        public bool FillCenter = true;
        [Tooltip("Filled only.")]
        public Image.FillMethod FillMethod = Image.FillMethod.Radial360;
        [Tooltip("Filled only."), Range(0f, 1f)]
        public float FillAmount = 1f;
        [Tooltip("Color overlay / tint multiplied over the sprite.")]
        public Color ColorOverlay = Color.white;
        public Material Material;

        [Header("Visual rect (relative to grid footprint)")]
        [Tooltip("How the Image rect is sized: stretched to the footprint, scaled relative to it, or fixed pixels.")]
        public VisualSizeMode SizeMode = VisualSizeMode.StretchToFootprint;
        [Tooltip("FootprintScale mode: multiplier per axis (1,1 = exact footprint).")]
        public Vector2 FootprintScale = Vector2.one;
        [Tooltip("FixedPixels mode: absolute width/height in pixels.")]
        public Vector2 FixedPixelSize = new Vector2(72f, 72f);
        [Tooltip("Pixel offset of the visual from the footprint's top-left (+x right, +y down).")]
        public Vector2 VisualOffset = Vector2.zero;

        /// Applies all Image settings to a piece. Size/position are handled
        /// by the view layer (they depend on grid metrics).
        public void ApplyTo(Image img)
        {
            img.sprite = Sprite;
            img.type = ImageType;
            img.preserveAspect = PreserveAspect;
            img.pixelsPerUnitMultiplier = PixelsPerUnitMultiplier;
            img.fillCenter = FillCenter;
            img.fillMethod = FillMethod;
            img.fillAmount = FillAmount;
            img.color = ColorOverlay;
            img.material = Material;
        }

        /// Final visual rect size for a given footprint pixel size.
        public Vector2 VisualSize(Vector2 footprintSizePx) => SizeMode switch
        {
            VisualSizeMode.FootprintScale => Vector2.Scale(footprintSizePx, FootprintScale),
            VisualSizeMode.FixedPixels => FixedPixelSize,
            _ => footprintSizePx,
        };

        /// Pixel offset (anchored-position space: +x right, -y down) that
        /// keeps the visual centered on the footprint before VisualOffset.
        public Vector2 VisualAnchorOffset(Vector2 footprintSizePx)
        {
            Vector2 size = VisualSize(footprintSizePx);
            Vector2 centered = (footprintSizePx - size) * 0.5f;
            return new Vector2(centered.x + VisualOffset.x, -(centered.y + VisualOffset.y));
        }
    }
}
