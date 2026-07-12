using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace SlidingSiege
{
    /// One EnemyView per enemy. Owns a "main" piece (always present) plus
    /// 0..3 pooled ghost pieces (persist while the enemy rests wrapped
    /// across an edge; also used transiently during shift tweens). Every
    /// piece is a FULL-footprint-size EnemyPieceView (sprite + own health
    /// bar) merely offset by ±gridWidth/±gridHeight; the Enemy Layer's Mask
    /// crops the parts that hang outside the grid.
    public class EnemyView
    {
        public int EnemyId { get; private set; }

        private readonly List<EnemyPieceView> _pieces = new List<EnemyPieceView>(4); // [0] = main
        private System.Func<EnemyPieceView> _acquirePiece;
        private System.Action<EnemyPieceView> _releasePiece;

        public void Bind(int enemyId, System.Func<EnemyPieceView> acquirePiece, System.Action<EnemyPieceView> releasePiece)
        {
            EnemyId = enemyId;
            _acquirePiece = acquirePiece;
            _releasePiece = releasePiece;
        }

        public IReadOnlyList<EnemyPieceView> Pieces => _pieces;

        public List<RectTransform> PieceRects()
        {
            var list = new List<RectTransform>(_pieces.Count);
            foreach (var p in _pieces) list.Add(p.RectTransform);
            return list;
        }

        /// Ensures exactly `count` pieces exist, configured from the enemy's
        /// definition (sprite, image type, tint, material, sizing) with each
        /// piece's health bar bound to the enemy (shared health display).
        public void EnsurePieceCount(int count, Enemy enemy, Vector2 footprintSizePx)
        {
            while (_pieces.Count < count)
            {
                var piece = _acquirePiece();
                piece.HealthBar.Bind(enemy);
                _pieces.Add(piece);
            }
            while (_pieces.Count > count)
            {
                var last = _pieces[_pieces.Count - 1];
                _pieces.RemoveAt(_pieces.Count - 1);
                last.HealthBar.Unbind();
                _releasePiece(last);
            }
            // Shape overrides may swap the sprite and visual rect at runtime.
            _visualOffset = enemy.VisualAnchorOffset(footprintSizePx);
            Vector2 visualSize = enemy.VisualSize(footprintSizePx);
            foreach (var piece in _pieces)
            {
                enemy.CurrentImage.ApplyTo(piece.SpriteImage);
                var rt = piece.RectTransform;
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
                var rt = _pieces[i].RectTransform;
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
                var rt = _pieces[i].RectTransform;
                rt.DOKill();
                rt.DOAnchorPos(_snappedPositions[i] + _visualOffset + offset, duration).SetEase(ease);
            }
        }

        public void ReleaseAll()
        {
            foreach (var p in _pieces)
            {
                p.HealthBar.Unbind();
                _releasePiece(p);
            }
            _pieces.Clear();
        }
    }
}
