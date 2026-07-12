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
        [Tooltip("Image settings for the link lines drawn from this enemy to its link targets / disabled cards; the tint's alpha is the line opacity.")]
        [SerializeField] private ImageSettings lineImage = new ImageSettings();
        [Tooltip("Base thickness (px) of this enemy's link lines.")]
        [SerializeField, Min(1f)] private float lineThickness = 4f;

        [Header("Redirect pulse (computational default)")]
        [Tooltip("Seconds one grow-and-shrink pulse lasts when no animator prefab is assigned.")]
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.35f;
        [Tooltip("Peak line-thickness multiplier at the middle of the pulse.")]
        [SerializeField, Min(1f)] private float linePulsePeak = 3f;
        [Tooltip("Peak overlay scale relative to the linked enemy's piece size.")]
        [SerializeField, Min(1f)] private float overlayPulsePeak = 1.4f;
        [Tooltip("Peak overlay opacity at the middle of the pulse.")]
        [SerializeField, Range(0f, 1f)] private float overlayPulseAlpha = 0.6f;
        [Tooltip("Image settings for the hit overlay; empty sprite = plain tinted square. Its tint is overridden each frame by the pulse fade (Link Color at Overlay Pulse Alpha).")]
        [SerializeField] private ImageSettings overlayImage = new ImageSettings();

        [Header("Redirect pulse (animator override)")]
        [Tooltip("Optional. Spawned over the linked enemy per pulse (needs an Image); its preset drives the overlay instead of the code pulse. Released when the preset completes.")]
        [SerializeField] private AnimationCaller overlayAnimatorPrefab;
        [SerializeField] private string overlayPresetLabel = "LinkPulse";
        [Tooltip("Optional. Spawned (invisible) per pulse as a curve source: while its preset plays, the link line's thickness multiplier reads the instance's localScale.y each frame.")]
        [SerializeField] private AnimationCaller lineAnimatorPrefab;
        [SerializeField] private string linePresetLabel = "LinkPulse";

        public ImageSettings LineImage => lineImage;
        public float LineThickness => lineThickness;
        public float PulseDuration => pulseDuration;
        public float LinePulsePeak => linePulsePeak;
        public float OverlayPulsePeak => overlayPulsePeak;
        public float OverlayPulseAlpha => overlayPulseAlpha;
        public ImageSettings OverlayImage => overlayImage;
        public AnimationCaller OverlayAnimatorPrefab => overlayAnimatorPrefab;
        public string OverlayPresetLabel => overlayPresetLabel;
        public AnimationCaller LineAnimatorPrefab => lineAnimatorPrefab;
        public string LinePresetLabel => linePresetLabel;
    }
}
