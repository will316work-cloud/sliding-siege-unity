using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// One EnemyView per enemy. Owns:
    ///  - a "main" Image piece (always present), plus
    ///  - 0..3 pooled ghost Image pieces (persist while the enemy rests
    ///    wrapped across an edge; also used transiently during shift tweens).
    /// Every piece is a FULL-footprint-size Image showing the full sprite,
    /// merely offset by ±gridWidth/±gridHeight; the Enemy Layer's Mask crops
    /// the parts that hang outside the grid. Pieces are parented directly to
    /// the Enemy Layer so the mask applies.
    public class EnemyView
    {
        public int EnemyId { get; private set; }

        private readonly List<Image> _pieces = new List<Image>(4); // [0] = main
        private System.Func<Image> _acquirePiece;
        private System.Action<Image> _releasePiece;

        public void Bind(int enemyId, System.Func<Image> acquirePiece, System.Action<Image> releasePiece)
        {
            EnemyId = enemyId;
            _acquirePiece = acquirePiece;
            _releasePiece = releasePiece;
        }

        public IReadOnlyList<Image> Pieces => _pieces;

        public List<RectTransform> PieceRects()
        {
            var list = new List<RectTransform>(_pieces.Count);
            foreach (var p in _pieces) list.Add(p.rectTransform);
            return list;
        }

        /// Ensures exactly `count` pieces exist, all configured with the
        /// enemy's sprite and footprint pixel size.
        public void EnsurePieceCount(int count, Sprite sprite, Vector2 footprintSizePx)
        {
            while (_pieces.Count < count)
            {
                var img = _acquirePiece();
                _pieces.Add(img);
            }
            while (_pieces.Count > count)
            {
                var last = _pieces[_pieces.Count - 1];
                _pieces.RemoveAt(_pieces.Count - 1);
                _releasePiece(last);
            }
            foreach (var img in _pieces)
            {
                img.sprite = sprite;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // top-left of Enemy Layer
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = footprintSizePx;
            }
        }

        /// Snap all pieces to the given anchored positions (piece i -> pos i).
        public void SnapPieces(IReadOnlyList<Vector2> positions)
        {
            for (int i = 0; i < _pieces.Count && i < positions.Count; i++)
                _pieces[i].rectTransform.anchoredPosition = positions[i];
        }

        public void ReleaseAll()
        {
            foreach (var p in _pieces) _releasePiece(p);
            _pieces.Clear();
        }
    }
}
