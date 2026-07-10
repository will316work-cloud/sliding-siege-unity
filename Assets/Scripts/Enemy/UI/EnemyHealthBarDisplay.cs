using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Notched health bar: a HorizontalLayoutGroup filled with pooled,
    /// display-only Sliders (one per notch), each expanding in the layout
    /// with the group's spacing forming the notch gaps.
    ///
    /// Notch math: with expected health-per-notch E and max HP M,
    /// n = floor(M / E); if M divides evenly the bar has n segments of E
    /// each, otherwise n+1 segments of M/(n+1) each (e.g. M=330, E=50 →
    /// 7 segments of ~47.143).
    ///
    /// Binds to Enemy.OnHealthChanged and shows itself only while damaged
    /// (0 < HP < Max; it also re-hides if HP returns to Max). Enemies whose
    /// definition unticks DiesAtZeroHP keep an empty bar at exactly 0 HP.
    public class EnemyHealthBarDisplay : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Display-only Slider prefab (fill only; no handle needed).")]
        [SerializeField] private Slider sliderPrefab;
        [Tooltip("Parent for segment sliders (has the HorizontalLayoutGroup). Defaults to this object.")]
        [SerializeField] private RectTransform container;

        [Header("Notches")]
        [Tooltip("Approximate HP each notch represents; actual per-notch HP is fitted to MaxHP (see class docs).")]
        [SerializeField, Min(1f)] private float expectedHealthPerNotch = 50f;

        private ObjectPool<Slider> _pool;
        private readonly List<Slider> _segments = new List<Slider>();
        private Enemy _enemy;
        private float _healthPerSegment;

        private void Awake()
        {
            if (container == null) container = (RectTransform)transform;
        }

        private void EnsurePool()
        {
            _pool ??= new ObjectPool<Slider>(
                createFunc: () =>
                {
                    var s = Instantiate(sliderPrefab, container);
                    s.interactable = false;
                    s.transition = Selectable.Transition.None;
                    return s;
                },
                actionOnGet: s => { s.gameObject.SetActive(true); s.transform.SetParent(container, false); s.transform.SetAsLastSibling(); },
                actionOnRelease: s => s.gameObject.SetActive(false),
                actionOnDestroy: s => Destroy(s.gameObject),
                defaultCapacity: 8);
        }

        /// Attach to an enemy: builds segments for its MaxHP and starts
        /// listening to its health changes.
        public void Bind(Enemy enemy)
        {
            Unbind();
            _enemy = enemy;
            if (_enemy == null) return;

            EnsurePool();
            BuildSegments(_enemy.MaxHP);
            _enemy.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(_enemy.HP, _enemy.MaxHP);
        }

        /// Detach and release segments (call before pooling the piece).
        public void Unbind()
        {
            if (_enemy != null) _enemy.OnHealthChanged -= HandleHealthChanged;
            _enemy = null;
            foreach (var s in _segments) _pool.Release(s);
            _segments.Clear();
            gameObject.SetActive(false);
        }

        private void BuildSegments(int maxHP)
        {
            int wholeNotches = Mathf.FloorToInt(maxHP / expectedHealthPerNotch);
            bool hasRemainder = maxHP - wholeNotches * expectedHealthPerNotch > 0f;
            int count = Mathf.Max(1, hasRemainder ? wholeNotches + 1 : wholeNotches);
            _healthPerSegment = (float)maxHP / count;

            for (int i = 0; i < count; i++)
            {
                var s = _pool.Get();
                s.minValue = 0f;
                s.maxValue = 1f;
                _segments.Add(s);
            }
        }

        private void HandleHealthChanged(int current, int max)
        {
            // Visible only while damaged. Enemies that survive at 0 HP
            // (Golem critical, unresolved Slime) keep an empty bar instead
            // of it vanishing while they're still on the board.
            bool surviving = _enemy != null && !_enemy.Rules.DiesAtZeroHP;
            gameObject.SetActive(current < max && (current > 0 || surviving));

            for (int i = 0; i < _segments.Count; i++)
                _segments[i].value = Mathf.Clamp01(current / _healthPerSegment - i);
        }

        private void OnDestroy()
        {
            if (_enemy != null) _enemy.OnHealthChanged -= HandleHealthChanged;
        }
    }
}
