using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace SlidingSiege
{
    /// Simple DOTween slide with configurable duration + ease.
    public class DOTweenSlideAnimator : IMoveAnimator
    {
        private readonly float _duration;
        private readonly Ease _ease;
        private Sequence _sequence;

        public DOTweenSlideAnimator(float duration = 0.18f, Ease ease = Ease.OutCubic)
        {
            _duration = duration;
            _ease = ease;
        }

        public void AnimateShift(IReadOnlyList<RectTransform> targets, Vector2 delta, Action onComplete)
        {
            Kill();
            _sequence = DOTween.Sequence();
            foreach (var rt in targets)
                _sequence.Join(rt.DOAnchorPos(rt.anchoredPosition + delta, _duration).SetEase(_ease));
            _sequence.OnComplete(() => onComplete?.Invoke());
            if (targets.Count == 0) { onComplete?.Invoke(); }
        }

        public void Kill()
        {
            if (_sequence != null && _sequence.IsActive()) _sequence.Kill();
            _sequence = null;
        }
    }
}
