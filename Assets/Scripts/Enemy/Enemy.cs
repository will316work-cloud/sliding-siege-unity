using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Runtime enemy instance. Anchor is the top-left cell of its footprint
    /// (always normalized into [0,rows) x [0,cols); the footprint may wrap).
    public class Enemy
    {
        public int Id;
        public EnemyDefinition Definition;
        public Vector2Int Anchor;          // (row, col) => (x = row, y = col)

        /// Runtime shape override; null = definition body. Set ONLY via
        /// GridState.ReshapeEnemy so cell refs stay consistent.
        public EnemyShape ShapeOverride;

        /// While reshaped: offset from the current anchor back to the
        /// pre-change anchor cell, used to restore it on revert.
        public Vector2Int? ResizeOriginOffset;

        /// The shape currently driving body and visuals.
        public EnemyShape ActiveShape => ShapeOverride ?? Definition.Shape;

        /// Occupied cells as offsets from the anchor (the body's bounding
        /// box top-left — cells may be negative for anchor-centered shapes).
        public IReadOnlyList<Vector2Int> BodyCells =>
            ShapeOverride != null && ShapeOverride.BodyCells != null && ShapeOverride.BodyCells.Length > 0
                ? ShapeOverride.BodyCells
                : Definition.Shape.BodyCells;

        /// Smallest body offsets per axis. Body cells may be negative
        /// (shapes centered on the anchor), so the visual bounding box
        /// top-left is Anchor + BodyMin, not the anchor itself.
        public Vector2Int BodyMin
        {
            get
            {
                int mx = int.MaxValue, my = int.MaxValue;
                foreach (var c in BodyCells) { if (c.x < mx) mx = c.x; if (c.y < my) my = c.y; }
                return mx == int.MaxValue ? Vector2Int.zero : new Vector2Int(mx, my);
            }
        }

        /// Bounding box span of the current body.
        public int SizeRows { get { int lo = int.MaxValue, hi = int.MinValue; foreach (var c in BodyCells) { if (c.x < lo) lo = c.x; if (c.x > hi) hi = c.x; } return lo == int.MaxValue ? 1 : hi - lo + 1; } }
        public int SizeCols { get { int lo = int.MaxValue, hi = int.MinValue; foreach (var c in BodyCells) { if (c.y < lo) lo = c.y; if (c.y > hi) hi = c.y; } return lo == int.MaxValue ? 1 : hi - lo + 1; } }

        /// Active visuals: the shape override's when present, else the
        /// definition's (override sprite null keeps the definition sprite).
        public Sprite CurrentSprite =>
            ShapeOverride != null && ShapeOverride.Sprite != null ? ShapeOverride.Sprite : Definition.Shape.Sprite;

        public Vector2 VisualSize(Vector2 footprintSizePx) => ActiveShape.VisualSize(footprintSizePx);

        public Vector2 VisualAnchorOffset(Vector2 footprintSizePx) => ActiveShape.VisualAnchorOffset(footprintSizePx);

        private int _hp;

        /// Current health. Setting it raises OnHealthChanged(current, max).
        public int HP
        {
            get => _hp;
            set
            {
                if (_hp == value) return;
                int delta = value - _hp;
                _hp = value;
                OnHealthChanged?.Invoke(_hp, MaxHP);
                if (delta < 0) OnHealthLost?.Invoke(-delta);
                else OnHealthGained?.Invoke(delta);
            }
        }

        public int MaxHP => Definition != null ? Definition.MaxHP : 0;

        /// (currentHP, maxHP) — raised whenever HP changes.
        public event Action<int, int> OnHealthChanged;

        /// Raised with the (positive) amount whenever HP decreases.
        public event Action<int> OnHealthLost;

        /// Raised with the (positive) amount whenever HP increases.
        public event Action<int> OnHealthGained;

        public bool IsDead => HP <= 0;
        public readonly List<StatusEffect> Statuses = new List<StatusEffect>();

        /// Ids of enemies this one is linked to (Golem/Siren style), written
        /// by LinkRandomEnemiesAbility. Dead targets are simply stale ids.
        public readonly List<int> LinkedIds = new List<int>();

        /// Golem-style critical state: reached 0 HP but survives to detonate
        /// next enemy phase (see EnemyDefinition.DetonatesAtZeroHP).
        public bool PendingDetonation;

        /// Living enemies this one is currently linked to.
        public IEnumerable<Enemy> LivingLinkTargets(GridState s)
        {
            foreach (var id in LinkedIds)
                if (s.Enemies.TryGetValue(id, out var en)) yield return en;
        }

        /// Hitbox stored by SetHitboxAbility; persists until overwritten.
        /// Cast abilities and conditions resolve it at the current anchor.
        public Hitbox QueuedHitbox;

        /// Product of all status damage-taken multipliers.
        public float DamageTakenMultiplier()
        {
            float m = 1f;
            foreach (var s in Statuses) m *= s.DamageTakenMultiplier;
            return m;
        }

        public bool HasStatus<T>() where T : StatusEffect => Statuses.OfType<T>().Any();

        /// False while dead or any status (e.g. a future stun) vetoes acting.
        /// A pending-detonation enemy sits at 0 HP but still acts (it has to
        /// run its detonation abilities next enemy phase).
        public bool CanAct => (!IsDead || PendingDetonation) && Statuses.All(s => !s.PreventsAction);
    }
}
