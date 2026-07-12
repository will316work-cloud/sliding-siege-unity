using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Runtime enemy instance. Anchor is the top-left cell of its footprint
    /// (always normalized into [0,rows) x [0,cols); the footprint may wrap).
    /// Game-state flags (critical, pending hit, cluster, links) mutate only
    /// through the methods below.
    public class Enemy
    {
        public Enemy(int id, EnemyDefinition definition, Vector2Int anchor)
        {
            Id = id;
            Definition = definition;
            Anchor = anchor;
            _hp = definition != null ? definition.MaxHP : 0;
        }

        public int Id { get; }
        public EnemyDefinition Definition { get; }

        /// Combat rules strategy (never null; falls back to defaults).
        public CombatRules Rules => Definition != null ? Definition.Rules : CombatRules.Default;

        /// (row, col) => (x = row, y = col). Written by GridState only.
        public Vector2Int Anchor { get; set; }

        /// Runtime shape override; null = definition body. Set ONLY via
        /// GridState.ReshapeEnemy so cell refs stay consistent.
        public EnemyShape ShapeOverride { get; private set; }
        public void SetShapeOverride(EnemyShape shape) => ShapeOverride = shape;

        /// While reshaped: offset from the current anchor back to the
        /// pre-change anchor cell, used to restore it on revert.
        public Vector2Int? ResizeOriginOffset { get; set; }

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

        /// Active visuals: the shape override's when present, else the base
        /// shape's (an override with no sprite keeps the base shape's entire
        /// Image settings).
        public ImageSettings CurrentImage =>
            ShapeOverride != null && ShapeOverride.Sprite != null ? ShapeOverride.Image : Definition.Shape.Image;

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

        // ---------------- Statuses ----------------

        private readonly List<StatusEffect> _statuses = new List<StatusEffect>();
        public IReadOnlyList<StatusEffect> Statuses => _statuses;

        public void AddStatus(StatusEffect status) { if (status != null) _statuses.Add(status); }
        public void RemoveStatuses(Predicate<StatusEffect> match) => _statuses.RemoveAll(match);
        public bool HasStatus<T>() where T : StatusEffect => _statuses.OfType<T>().Any();

        /// Ticks down turn-limited statuses and drops the expired ones
        /// (EnemyPhaseRunner calls this at the end of each phase).
        public void TickStatuses() => _statuses.RemoveAll(s => s.TickAndCheckExpired());

        /// Product of all status damage-taken multipliers.
        public float DamageTakenMultiplier()
        {
            float m = 1f;
            foreach (var s in _statuses) m *= s.DamageTakenMultiplier;
            return m;
        }

        // ---------------- Links ----------------

        private readonly List<int> _linkedIds = new List<int>();

        /// Ids of enemies this one is linked to (Golem/Siren style), written
        /// by LinkRandomEnemiesAbility. Dead targets are simply stale ids.
        public IReadOnlyList<int> LinkedIds => _linkedIds;

        public void LinkTo(int enemyId) { if (!_linkedIds.Contains(enemyId)) _linkedIds.Add(enemyId); }
        public void ClearLinks() => _linkedIds.Clear();
        public bool IsLinkedTo(int enemyId) => _linkedIds.Contains(enemyId);

        /// Living enemies this one is currently linked to.
        public IEnumerable<Enemy> LivingLinkTargets(GridState s)
        {
            foreach (var id in _linkedIds)
                if (s.Enemies.TryGetValue(id, out var en)) yield return en;
        }

        // ---------------- Critical / cluster / pending hit ----------------

        /// Golem-style critical state: reached 0 HP but survives until its
        /// own abilities remove it (see CombatRules.DiesAtZeroHP, off).
        public bool PendingDetonation { get; private set; }

        /// Enter the critical state: survives at 0 HP, links dropped.
        public void GoCritical() { PendingDetonation = true; ClearLinks(); }

        /// Leave the critical state (e.g. a Slime regenerating to full).
        public void ResetCritical() => PendingDetonation = false;

        /// Slime cluster membership; -1 = none. Assigned once on spawn by
        /// SlimeClusterAssignAbility. LinkOverlay chains cluster members.
        public int ClusterId { get; private set; } = -1;
        public void AssignCluster(int clusterId) => ClusterId = clusterId;

        /// Living members of this enemy's cluster, including itself
        /// (just itself while unassigned).
        public IEnumerable<Enemy> ClusterMembers(GridState s)
        {
            if (ClusterId < 0) { yield return this; yield break; }
            foreach (var en in s.Enemies.Values)
                if (en.Definition == Definition && en.ClusterId == ClusterId)
                    yield return en;
        }

        /// Set whenever damage is aimed at this enemy (even when a Golem
        /// absorbs it); consumed by SlimeClusterResolveAbility each phase.
        public bool PendingHit { get; private set; }
        public void MarkPendingHit() => PendingHit = true;
        public void ClearPendingHit() => PendingHit = false;

        /// Generic per-enemy charge-up counter (Siren's song). Advanced and
        /// reset by charge abilities each enemy phase.
        public int ChargeCounter { get; private set; }
        public void AdvanceCharge() => ChargeCounter++;
        public void ResetCharge() => ChargeCounter = 0;

        /// Hitbox stored by SetHitboxAbility; persists until overwritten.
        /// Cast abilities and conditions resolve it at the current anchor.
        public Hitbox QueuedHitbox { get; set; }

        /// False while dead or any status (e.g. stun) vetoes acting. A
        /// pending-detonation enemy sits at 0 HP but still acts (it has to
        /// run its detonation abilities next enemy phase).
        public bool CanAct => (!IsDead || PendingDetonation) && _statuses.All(s => !s.PreventsAction);
    }
}
