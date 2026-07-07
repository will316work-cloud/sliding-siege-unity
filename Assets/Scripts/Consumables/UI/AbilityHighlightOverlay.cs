using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Renders attack/item target highlights as pooled Images on a dedicated
    /// layer ABOVE the enemies (instead of tinting the cell backgrounds).
    /// Each highlight is one cell-sized Image with a per-call color and an
    /// optional per-call sprite (falls back to the serialized default; null
    /// = plain colored square).
    public class AbilityHighlightOverlay : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Layer above the Enemy Layer (sibling after it; order vs the Shift Preview Overlay doesn't matter). Same rect as the Enemy Layer.")]
        [SerializeField] private RectTransform overlayRoot;

        [Header("Defaults")]
        [Tooltip("Default sprite for highlight cells; null = plain square.")]
        [SerializeField] private Sprite defaultSprite;

        private GridLayoutMetrics _metrics;
        private ObjectPool<Image> _pool;
        private readonly List<Image> _active = new List<Image>();

        public void Initialize(GridLayoutMetrics metrics)
        {
            _metrics = metrics;
            _pool ??= new ObjectPool<Image>(
                createFunc: () =>
                {
                    var go = new GameObject("AbilityHighlight", typeof(RectTransform), typeof(Image));
                    var img = go.GetComponent<Image>();
                    img.raycastTarget = false;
                    go.transform.SetParent(overlayRoot, false);
                    return img;
                },
                actionOnGet: img => { img.gameObject.SetActive(true); img.transform.SetParent(overlayRoot, false); },
                actionOnRelease: img => img.gameObject.SetActive(false),
                actionOnDestroy: img => Destroy(img.gameObject),
                defaultCapacity: 32);
        }

        /// Replaces all highlights. Sprite null = use the default sprite
        /// (or a plain square if that's also null).
        public void SetHighlights(IEnumerable<(Vector2Int cell, Color color, Sprite sprite)> highlights)
        {
            Clear();
            foreach (var (cell, color, sprite) in highlights)
            {
                var img = _pool.Get();
                img.sprite = sprite != null ? sprite : defaultSprite;
                img.color = color;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(_metrics.CellSize, _metrics.CellSize);
                rt.anchoredPosition = new Vector2(
                    _metrics.ContentOffset.x + cell.y * _metrics.Stride,
                    -(_metrics.ContentOffset.y + cell.x * _metrics.Stride));
                _active.Add(img);
            }
        }

        public void Clear()
        {
            if (_pool == null) return;
            foreach (var img in _active) _pool.Release(img);
            _active.Clear();
        }
    }
}
