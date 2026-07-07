using System;
using TMPro;
using UnityEngine;

namespace SlidingSiege
{
    /// Component on the text indicator prefab (root: AnimationCaller + this;
    /// TMP text on root or a child). Styles the text for damage vs heal and
    /// plays the matching AnimationCaller preset, invoking onFinished when
    /// the animation completes (so the spawner can pool it).
    [RequireComponent(typeof(AnimationCaller))]
    public class DamageTextIndicator : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private TMP_Text text;
        [SerializeField] private AnimationCaller animationCaller;

        [Header("Preset labels")]
        [SerializeField] private string damagePresetLabel = "Damage";
        [SerializeField] private string healPresetLabel = "Heal";

        [Header("Damage style")]
        [SerializeField] private Color damageColor = Color.white;
        [SerializeField] private TMP_FontAsset damageFont;
        [SerializeField, Min(1f)] private float damageFontSize = 24f;

        [Header("Heal style")]
        [SerializeField] private Color healColor = new Color(0.66f, 1f, 0.24f);
        [SerializeField] private TMP_FontAsset healFont;
        [SerializeField, Min(1f)] private float healFontSize = 24f;

        private void Reset() => animationCaller = GetComponent<AnimationCaller>();

        /// Displays "X" for damage or "+X" for heals, styled per the
        /// serialized fields, then plays the matching preset. onFinished
        /// fires when the animation completes.
        public void Show(int amount, bool isHeal, Action onFinished)
        {
            text.text = isHeal ? "+" + amount : amount.ToString();
            text.color = isHeal ? healColor : damageColor;
            text.fontSize = isHeal ? healFontSize : damageFontSize;
            var font = isHeal ? healFont : damageFont;
            if (font != null) text.font = font;

            animationCaller.PlayPreset(isHeal ? healPresetLabel : damagePresetLabel, onFinished);
        }
    }
}
