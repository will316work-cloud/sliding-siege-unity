using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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

        /// Ensures exactly `count` pieces exist, all configured from the
        /// enemy definition (sprite, image type, tint, material, sizing).
        public void EnsurePieceCount(int count, EnemyDefinition def, Vector2 footprintSizePx)
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
            _visualOffset = def.VisualAnchorOffset(footprintSizePx);
            Vector2 visualSize = def.VisualSize(footprintSizePx);
            foreach (var img in _pieces)
            {
                def.ApplyTo(img);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // top-left of Enemy Layer
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = visualSize;
            }
        }

        private Vector2 _visualOffset;
        private readonly List<Vector2> _snappedPositions = new List<Vector2>(4);

        /// Snap all pieces to the given footprint anchored positions; the
        /// definition's visual size/offset is applied on top (piece i -> pos i).
        public void SnapPieces(IReadOnlyList<Vector2> positions)
        {
            _snappedPositions.Clear();
            for (int i = 0; i < _pieces.Count && i < positions.Count; i++)
            {
                var rt = _pieces[i].rectTransform;
                rt.DOKill();
                rt.anchoredPosition = positions[i] + _visualOffset;
                _snappedPositions.Add(positions[i]);
            }
        }

        /// Tween all pieces to their snapped position plus `offset` (used for
        /// the drag shift preview nudge; pass Vector2.zero to settle back).
        public void NudgeTo(Vector2 offset, float duration, Ease ease = Ease.OutCubic)
        {
            for (int i = 0; i < _pieces.Count && i < _snappedPositions.Count; i++)
            {
                var rt = _pieces[i].rectTransform;
                rt.DOKill();
                rt.DOAnchorPos(_snappedPositions[i] + _visualOffset + offset, duration).SetEase(ease);
            }
        }

        public void ReleaseAll()
        {
            foreach (var p in _pieces) _releasePiece(p);
            _pieces.Clear();
        }
    }
}
