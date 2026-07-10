using System;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("LinkColor")]
        [SerializeField] private Color linkColor = Color.white;
        [Tooltip("Base thickness (px) of this enemy's link lines.")]
        [FormerlySerializedAs("LineThickness")]
        [SerializeField, Min(1f)] private float lineThickness = 4f;
        [Tooltip("Opacity of this enemy's link lines.")]
        [FormerlySerializedAs("LineAlpha")]
        [SerializeField, Range(0f, 1f)] private float lineAlpha = 0.75f;

        [Header("Redirect pulse (computational default)")]
        [Tooltip("Seconds one grow-and-shrink pulse lasts when no animator prefab is assigned.")]
        [FormerlySerializedAs("PulseDuration")]
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.35f;
        [Tooltip("Peak line-thickness multiplier at the middle of the pulse.")]
        [FormerlySerializedAs("LinePulsePeak")]
        [SerializeField, Min(1f)] private float linePulsePeak = 3f;
        [Tooltip("Peak overlay scale relative to the linked enemy's piece size.")]
        [FormerlySerializedAs("OverlayPulsePeak")]
        [SerializeField, Min(1f)] private float overlayPulsePeak = 1.4f;
        [Tooltip("Peak overlay opacity at the middle of the pulse.")]
        [FormerlySerializedAs("OverlayPulseAlpha")]
        [SerializeField, Range(0f, 1f)] private float overlayPulseAlpha = 0.6f;
        [Tooltip("Optional sprite for the hit overlay; empty = plain tinted square.")]
        [FormerlySerializedAs("OverlaySprite")]
        [SerializeField] private Sprite overlaySprite;

        [Header("Redirect pulse (animator override)")]
        [Tooltip("Optional. Spawned over the linked enemy per pulse (needs an Image); its preset drives the overlay instead of the code pulse. Released when the preset completes.")]
        [FormerlySerializedAs("OverlayAnimatorPrefab")]
        [SerializeField] private AnimationCaller overlayAnimatorPrefab;
        [FormerlySerializedAs("OverlayPresetLabel")]
        [SerializeField] private string overlayPresetLabel = "LinkPulse";
        [Tooltip("Optional. Spawned (invisible) per pulse as a curve source: while its preset plays, the link line's thickness multiplier reads the instance's localScale.y each frame.")]
        [FormerlySerializedAs("LineAnimatorPrefab")]
        [SerializeField] private AnimationCaller lineAnimatorPrefab;
        [FormerlySerializedAs("LinePresetLabel")]
        [SerializeField] private string linePresetLabel = "LinkPulse";

        public Color LinkColor => linkColor;
        public float LineThickness => lineThickness;
        public float LineAlpha => lineAlpha;
        public float PulseDuration => pulseDuration;
        public float LinePulsePeak => linePulsePeak;
        public float OverlayPulsePeak => overlayPulsePeak;
        public float OverlayPulseAlpha => overlayPulseAlpha;
        public Sprite OverlaySprite => overlaySprite;
        public AnimationCaller OverlayAnimatorPrefab => overlayAnimatorPrefab;
        public string OverlayPresetLabel => overlayPresetLabel;
        public AnimationCaller LineAnimatorPrefab => lineAnimatorPrefab;
        public string LinePresetLabel => linePresetLabel;
    }
}
