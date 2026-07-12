using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Shared UI-Image styling block used everywhere a definition drives a
    /// pooled/managed Image: enemy piece sprites (EnemyShape), link redirect
    /// overlays (LinkDisplaySettings), and cursed-line fills
    /// (EnemyDefinition.DisabledLineDisplay). ApplyTo pushes every field.
    [Serializable]
    public class ImageSettings
    {
        [Tooltip("Sprite drawn by the Image; empty = plain tinted fill.")]
        [SerializeField] private Sprite sprite;
        [Tooltip("Color overlay / tint multiplied over the sprite.")]
        [SerializeField] private Color colorOverlay = Color.white;
        [SerializeField] private Image.Type imageType = Image.Type.Simple;
        [Tooltip("Simple/Filled only.")]
        [SerializeField] private bool preserveAspect = false;
        [Tooltip("Sliced/Tiled only.")]
        [SerializeField] private float pixelsPerUnitMultiplier = 1f;
        [Tooltip("Sliced only: draw the center region.")]
        [SerializeField] private bool fillCenter = true;
        [Tooltip("Filled only.")]
        [SerializeField] private Image.FillMethod fillMethod = Image.FillMethod.Radial360;
        [Tooltip("Filled only."), Range(0f, 1f)]
        [SerializeField] private float fillAmount = 1f;
        [SerializeField] private Material material;
        [SerializeField] private bool raycastTarget = false;

        public Sprite Sprite => sprite;
        public Color ColorOverlay => colorOverlay;

        /// Applies every setting to the Image, sprite included.
        public void ApplyTo(Image img)
        {
            img.sprite = sprite;
            img.color = colorOverlay;
            img.type = imageType;
            img.preserveAspect = preserveAspect;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            img.fillCenter = fillCenter;
            img.fillMethod = fillMethod;
            img.fillAmount = fillAmount;
            img.material = material;
            img.raycastTarget = raycastTarget;
        }
    }
}
