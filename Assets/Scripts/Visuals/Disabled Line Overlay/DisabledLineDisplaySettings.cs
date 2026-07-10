using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Per-definition styling consumed by DisabledLineOverlay: how the
    /// cursed (slide-disabled) rows/columns sourced by this enemy look.
    /// Defaults reproduce the classic look: a plain fill in the enemy's
    /// LinkDisplay color at low alpha.
    [Serializable]
    public class DisabledLineDisplaySettings
    {
        [Header("Image")]
        [Tooltip("Optional sprite drawn along the line; empty = plain tinted fill.")]
        [SerializeField] private Sprite sprite;
        [Tooltip("Tint used when Use Link Color is off (multiplied over the sprite, if any). Its alpha is replaced by Tint Alpha.")]
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

        /// Applies all settings to a pooled line Image. linkColor is the
        /// source enemy's LinkDisplay color, used when Use Link Color is on.
        public void ApplyTo(Image img)
        {
            img.color = colorOverlay;
            img.sprite = sprite;
            img.type = imageType;
            img.preserveAspect = preserveAspect;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            img.fillCenter = fillCenter;
            img.fillMethod = fillMethod;
            img.fillAmount = fillAmount;
            img.material = material;
        }
    }
}
