using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Abstraction so shift movement styles can be swapped later
    /// (e.g. bouncy hops, staggered waves) without touching the view manager.
    public interface IMoveAnimator
    {
        /// Move every RectTransform by `delta` (anchored-position space).
        /// Invoke onComplete exactly once when ALL movements are finished.
        void AnimateShift(IReadOnlyList<RectTransform> targets, Vector2 delta, Action onComplete);
        void Kill();
    }
}
