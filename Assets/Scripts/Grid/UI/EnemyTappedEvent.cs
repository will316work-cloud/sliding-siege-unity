using System;
using UnityEngine.Events;

namespace SlidingSiege
{
    /// Serializable UnityEvent carrying the tapped enemy, so responses can
    /// be wired in the Inspector.
    [Serializable]
    public class EnemyTappedEvent : UnityEvent<Enemy> { }
}
