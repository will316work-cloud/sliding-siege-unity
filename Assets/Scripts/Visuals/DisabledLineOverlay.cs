using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Tints cursed (slide-disabled) rows and columns, colored by the
    /// source enemy's LinkDisplay color. Rebuilt every LateUpdate from
    /// pooled Images, so curses appearing/expiring/dying update instantly.
    ///
    /// Scene setup: put this on a RectTransform with the SAME rect as the
    /// Enemy Layer (sibling above the cell backgrounds); SlidingGridController
    /// wires state + metrics at startup.
    public class DisabledLineOverlay : MonoBehaviour
    {
        [Header("Appearance")]
        [Tooltip("Opacity of the line tint (color comes from the source enemy's LinkDisplay).")]
        [SerializeField, Range(0f, 1f)] private float tintAlpha = 0.22f;

        private GridState _state;
        private GridLayoutMetrics _metrics;
        private readonly List<Image> _pool = new List<Image>();
        private int _used;

        public void Initialize(GridState state, GridLayoutMetrics metrics)
        {
            _state = state;
            _metrics = metrics;
        }

        private void LateUpdate()
        {
            _used = 0;
            if (_state != null && _metrics != null)
            {
                var gridSize = _metrics.GridPixelSize(_state.Rows, _state.Cols);
                foreach (var (isRow, index, sourceId) in _state.DisabledLines())
                {
                    var color = Color.white;
                    if (_state.Enemies.TryGetValue(sourceId, out var source))
                        color = source.Definition.LinkDisplay.LinkColor;
                    color.a = tintAlpha;

                    var img = NextTint();
                    img.color = color;
                    var rt = img.rectTransform;
                    if (isRow)
                    {
                        rt.sizeDelta = new Vector2(gridSize.x, _metrics.CellSize);
                        rt.anchoredPosition = new Vector2(
                            _metrics.ContentOffset.x,
                            -(_metrics.ContentOffset.y + index * _metrics.Stride));
                    }
                    else
                    {
                        rt.sizeDelta = new Vector2(_metrics.CellSize, gridSize.y);
                        rt.anchoredPosition = new Vector2(
                            _metrics.ContentOffset.x + index * _metrics.Stride,
                            -_metrics.ContentOffset.y);
                    }
                }
            }
            for (int i = _used; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
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
