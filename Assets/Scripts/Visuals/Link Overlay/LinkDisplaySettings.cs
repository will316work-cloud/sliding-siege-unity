using System;
using UnityEngine;

namespace SlidingSiege
{
    /// Per-definition styling consumed by LinkOverlay: how this enemy's
    /// link lines look, and the redirected-hit pulse played on its linked
    /// targets. Always the LINKING enemy's settings (the Golem's, not the
    /// linked target's).
    [Serializable]
    public class LinkDisplaySettings
    {
        [Header("Lines")]
        [Tooltip("Color of the link lines drawn from this enemy to its link targets / disabled cards.")]
        public Color LinkColor = Color.white;
        [Tooltip("Base thickness (px) of this enemy's link lines.")]
        [Min(1f)] public float LineThickness = 4f;
        [Tooltip("Opacity of this enemy's link lines.")]
        [Range(0f, 1f)] public float LineAlpha = 0.75f;

        [Header("Redirect pulse (computational default)")]
        [Tooltip("Seconds one grow-and-shrink pulse lasts when no animator prefab is assigned.")]
        [Min(0.05f)] public float PulseDuration = 0.35f;
        [Tooltip("Peak line-thickness multiplier at the middle of the pulse.")]
        [Min(1f)] public float LinePulsePeak = 3f;
        [Tooltip("Peak overlay scale relative to the linked enemy's piece size.")]
        [Min(1f)] public float OverlayPulsePeak = 1.4f;
        [Tooltip("Peak overlay opacity at the middle of the pulse.")]
        [Range(0f, 1f)] public float OverlayPulseAlpha = 0.6f;
        [Tooltip("Optional sprite for the hit overlay; empty = plain tinted square.")]
        public Sprite OverlaySprite;

        [Header("Redirect pulse (animator override)")]
        [Tooltip("Optional. Spawned over the linked enemy per pulse (needs an Image); its preset drives the overlay instead of the code pulse. Released when the preset completes.")]
        public AnimationCaller OverlayAnimatorPrefab;
        public string OverlayPresetLabel = "LinkPulse";
        [Tooltip("Optional. Spawned (invisible) per pulse as a curve source: while its preset plays, the link line's thickness multiplier reads the instance's localScale.y each frame.")]
        public AnimationCaller LineAnimatorPrefab;
        public string LinePresetLabel = "LinkPulse";
    }
}
