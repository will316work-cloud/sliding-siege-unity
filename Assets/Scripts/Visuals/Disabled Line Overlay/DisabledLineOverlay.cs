using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Tints cursed (slide-disabled) rows and columns, styled per source
    /// enemy by its EnemyDefinition.DisabledLineDisplay settings (sprite,
    /// color, Image options). Rebuilt every LateUpdate from pooled Images,
    /// so curses appearing/expiring/dying update instantly.
    ///
    /// Scene setup: put this on a RectTransform with the SAME rect as the
    /// Enemy Layer (sibling above the cell backgrounds); SlidingGridController
    /// wires state + metrics at startup.
    public class DisabledLineOverlay : MonoBehaviour
    {
        private static readonly DisabledLineDisplaySettings FallbackSettings = new DisabledLineDisplaySettings();

        [Header("Blocked-shift feedback")]
        [Tooltip("Extra opacity added to a cursed line while the player drags a shift it blocks.")]
        [SerializeField, Range(0f, 1f)] private float highlightAlphaBoost = 0.15f;
        [Tooltip("Seconds the error shake lasts.")]
        [SerializeField, Min(0.05f)] private float shakeDuration = 0.35f;
        [Tooltip("Peak shake displacement in pixels (rows shake horizontally, columns vertically).")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 8f;
        [Tooltip("Shake oscillations per second.")]
        [SerializeField, Min(1f)] private float shakeFrequency = 14f;

        private GridState _state;
        private GridLayoutMetrics _metrics;
        private readonly List<Image> _pool = new List<Image>();
        private int _used;
        private readonly HashSet<(bool IsRow, int Index)> _highlighted = new HashSet<(bool, int)>();
        private readonly Dictionary<(bool IsRow, int Index), float> _shakes = new Dictionary<(bool, int), float>();
        private readonly List<(bool, int)> _finishedShakes = new List<(bool, int)>();

        public void Initialize(GridState state, GridLayoutMetrics metrics)
        {
            _state = state;
            _metrics = metrics;
        }

        // ---------------- Blocked-shift feedback ----------------

        /// Brightens the given cursed lines while a blocked drag is held.
        /// Pass null (or ClearHighlight) when the drag ends or unblocks.
        public void SetHighlight(IEnumerable<(bool IsRow, int Index)> lines)
        {
            _highlighted.Clear();
            if (lines != null) foreach (var line in lines) _highlighted.Add(line);
        }

        public void ClearHighlight() => _highlighted.Clear();

        /// Plays the error shake on a cursed line: rows shake horizontally,
        /// columns vertically — the axis the blocked shift would have moved.
        public void Shake(bool isRow, int index) => _shakes[(isRow, index)] = shakeDuration;

        private void LateUpdate()
        {
            TickShakes();
            _used = 0;
            if (_state != null && _metrics != null)
            {
                var gridSize = _metrics.GridPixelSize(_state.Rows, _state.Cols);
                foreach (var (isRow, index, sourceId) in _state.DisabledLines())
                {
                    var settings = FallbackSettings;
                    if (_state.Enemies.TryGetValue(sourceId, out var source))
                    {
                        settings = source.Definition.DisabledLineDisplay;
                    }

                    var img = NextTint();
                    settings.ApplyTo(img);
                    if (_highlighted.Contains((isRow, index)))
                    {
                        var c = img.color;
                        c.a = Mathf.Clamp01(c.a + highlightAlphaBoost);
                        img.color = c;
                    }

                    float shake = ShakeOffset(isRow, index);
                    var rt = img.rectTransform;
                    if (isRow)
                    {
                        rt.sizeDelta = new Vector2(gridSize.x, _metrics.CellSize);
                        rt.anchoredPosition = new Vector2(
                            _metrics.ContentOffset.x + shake,
                            -(_metrics.ContentOffset.y + index * _metrics.Stride));
                    }
                    else
                    {
                        rt.sizeDelta = new Vector2(_metrics.CellSize, gridSize.y);
                        rt.anchoredPosition = new Vector2(
                            _metrics.ContentOffset.x + index * _metrics.Stride,
                            -_metrics.ContentOffset.y + shake);
                    }
                }
            }
            for (int i = _used; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        }

        private void TickShakes()
        {
            if (_shakes.Count == 0) return;
            _finishedShakes.Clear();
            _finishedShakes.AddRange(_shakes.Keys);
            foreach (var key in _finishedShakes)
            {
                float remaining = _shakes[key] - Time.deltaTime;
                if (remaining <= 0f) _shakes.Remove(key);
                else _shakes[key] = remaining;
            }
        }

        /// Damped sine offset for an active shake on this line (0 if none).
        private float ShakeOffset(bool isRow, int index)
        {
            if (!_shakes.TryGetValue((isRow, index), out float remaining)) return 0f;
            float t = 1f - remaining / shakeDuration; // 0 -> 1 over the shake
            float damp = 1f - t;
            return Mathf.Sin(t * shakeDuration * shakeFrequency * 2f * Mathf.PI) * shakeAmplitude * damp;
        }

        private Image NextTint()
        {
            Image img;
            if (_used < _pool.Count)
            {
                img = _pool[_used++];
                if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
                return img;
            }
            var go = new GameObject("DisabledLine", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            img = go.GetComponent<Image>();
            img.raycastTarget = false;
            _pool.Add(img);
            _used++;
            return img;
        }
    }
}
