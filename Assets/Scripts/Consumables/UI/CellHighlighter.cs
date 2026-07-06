using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Tints the grid's cell background Images to show attack/item target
    /// areas (the JS game's cell-class highlights). Restores the original
    /// color on clear.
    public class CellHighlighter : MonoBehaviour
    {
        private GridUIBuilder _builder;
        private readonly Dictionary<Graphic, Color> _originals = new Dictionary<Graphic, Color>();

        public void Initialize(GridUIBuilder builder) => _builder = builder;

        public void SetHighlights(IEnumerable<(Vector2Int cell, Color color)> highlights)
        {
            Clear();
            foreach (var (cell, color) in highlights)
            {
                var g = _builder.CellGraphic(cell.x, cell.y);
                if (g == null) continue;
                if (!_originals.ContainsKey(g)) _originals[g] = g.color;
                g.color = color;
            }
        }

        public void Clear()
        {
            foreach (var kv in _originals) if (kv.Key != null) kv.Key.color = kv.Value;
            _originals.Clear();
        }
    }
}
