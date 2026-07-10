using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Pure back-end grid model. No UnityEngine.UI dependencies.
    /// Each cell holds a LIST of occupant refs; enemies occupy an arbitrary
    /// set of body cells (offsets from their anchor, wrapping allowed).
    /// Shifting a line drags every line spanned by any multi-line enemy
    /// touching the already-included lines (fixed-point expansion), so
    /// bodies translate rigidly and anchors move with the shift.
    public class GridState
    {
        public int Rows { get; private set; }
        public int Cols { get; private set; }

        // [row, col] -> occupant refs
        private List<OccupantRef>[,] _cells;
        public readonly Dictionary<int, Enemy> Enemies = new Dictionary<int, Enemy>();
        private int _nextEnemyId = 1;

        public event Action<ShiftResult> OnShifted;
        public event Action<Enemy> OnEnemySpawned;
        public event Action<Enemy> OnEnemyRemoved;
        public event Action<Enemy, Vector2Int, MoveStyle> OnEnemyMoved; // (enemy, oldAnchor, style)
        public event Action<Enemy> OnEnemyResized;
        public event Action OnRebuilt;

        /// Raised when an enemy goes critical (Enemy.PendingDetonation set by
        /// CombatSystem.HandleZeroHp). Drives OnCritical-triggered abilities.
        public event Action<Enemy> OnEnemyWentCritical;
        public void NotifyEnemyWentCritical(Enemy en) => OnEnemyWentCritical?.Invoke(en);

        /// Raised when an enemy's stored hitbox changes outside the enemy
        /// phase (SetHitboxAbility), so telegraph overlays can redraw.
        public event Action<Enemy> OnEnemyHitboxChanged;
        public void NotifyEnemyHitboxChanged(Enemy en) => OnEnemyHitboxChanged?.Invoke(en);

        /// Raised when damage aimed at a linked enemy was absorbed by its
        /// linker (Golem): (intended target, absorber). Drives hit feedback.
        public event Action<Enemy, Enemy> OnDamageRedirected;
        public void NotifyDamageRedirected(Enemy target, Enemy absorber) => OnDamageRedirected?.Invoke(target, absorber);

        public void Initialize(int rows, int cols)
        {
            Rows = rows; Cols = cols;
            _cells = new List<OccupantRef>[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    _cells[r, c] = new List<OccupantRef>(2);
            Enemies.Clear();
            _nextEnemyId = 1;
            OnRebuilt?.Invoke();
        }

        public IReadOnlyList<OccupantRef> RefsAt(int r, int c) => _cells[r, c];

        public IEnumerable<Enemy> EnemiesAt(int r, int c)
        {
            foreach (var rf in _cells[r, c])
                if (rf.Kind == OccupantKind.Enemy && Enemies.TryGetValue(rf.Id, out var e))
                    yield return e;
        }

        public int Wrap(int v, int size) => ((v % size) + size) % size;

        // ---------------- Placement ----------------

        public bool CanPlaceAt(int r, int c, EnemyDefinition def)
        {
            foreach (var off in def.Shape.BodyCells)
                if (_cells[Wrap(r + off.x, Rows), Wrap(c + off.y, Cols)].Count > 0)
                    return false;
            return true;
        }

        /// True if the given body cells fit at anchor (r, c) (wrapped),
        /// treating cells occupied only by `ignoreEnemyId` as free.
        public bool CanPlaceBodyAtIgnoring(int r, int c, IReadOnlyList<Vector2Int> body, int ignoreEnemyId)
        {
            foreach (var off in body)
            {
                var refs = _cells[Wrap(r + off.x, Rows), Wrap(c + off.y, Cols)];
                foreach (var rf in refs)
                    if (!(rf.Kind == OccupantKind.Enemy && rf.Id == ignoreEnemyId))
                        return false;
            }
            return true;
        }

        public Enemy SpawnEnemy(EnemyDefinition def, int r, int c)
        {
            var enemy = new Enemy { Id = _nextEnemyId++, Definition = def, Anchor = new Vector2Int(r, c), HP = def.MaxHP };
            WriteBody(enemy);
            Enemies[enemy.Id] = enemy;
            OnEnemySpawned?.Invoke(enemy);
            return enemy;
        }

        /// Moves an enemy's anchor (body refs are re-laid; wrapped).
        /// Caller must have validated with CanPlaceBodyAtIgnoring.
        public void MoveEnemy(int id, Vector2Int newAnchor, MoveStyle style = MoveStyle.Instant)
        {
            if (!Enemies.TryGetValue(id, out var enemy)) return;
            Vector2Int old = enemy.Anchor;
            ClearRefs(id);
            enemy.Anchor = new Vector2Int(Wrap(newAnchor.x, Rows), Wrap(newAnchor.y, Cols));
            WriteBody(enemy);
            OnEnemyMoved?.Invoke(enemy, old, style);
        }

        /// Re-lays an enemy's body with a new anchor and shape override
        /// (null = back to the definition body). No occupancy validation:
        /// gate with a Shape Fits condition; overlaps stack refs.
        public void ReshapeEnemy(int id, Vector2Int newAnchor, EnemyShape shapeOverride)
        {
            if (!Enemies.TryGetValue(id, out var enemy)) return;
            ClearRefs(id);
            enemy.ShapeOverride = shapeOverride;
            enemy.Anchor = new Vector2Int(Wrap(newAnchor.x, Rows), Wrap(newAnchor.y, Cols));
            WriteBody(enemy);
            OnEnemyResized?.Invoke(enemy);
        }

        public void RemoveEnemy(int id)
        {
            if (!Enemies.TryGetValue(id, out var enemy)) return;
            ClearRefs(id);
            Enemies.Remove(id);
            OnEnemyRemoved?.Invoke(enemy);
        }

        private void WriteBody(Enemy enemy)
        {
            foreach (var off in enemy.BodyCells)
                _cells[Wrap(enemy.Anchor.x + off.x, Rows), Wrap(enemy.Anchor.y + off.y, Cols)]
                    .Add(new OccupantRef(OccupantKind.Enemy, enemy.Id));
        }

        private void ClearRefs(int id)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    _cells[r, c].RemoveAll(rf => rf.Kind == OccupantKind.Enemy && rf.Id == id);
        }

        /// Distinct wrapped lines (rows or columns) an enemy's body spans.
        private HashSet<int> LinesSpanned(Enemy en, bool rowAxis)
        {
            var lines = new HashSet<int>();
            foreach (var off in en.BodyCells)
                lines.Add(rowAxis ? Wrap(en.Anchor.x + off.x, Rows) : Wrap(en.Anchor.y + off.y, Cols));
            return lines;
        }

        // ---------------- Shifting ----------------

        /// Fixed-point expansion: any enemy spanning 2+ lines on the axis
        /// whose body touches a line already in the set drags ALL its lines
        /// into the set.
        public HashSet<int> LinkedLinesForAxis(bool rowAxis, int seed)
        {
            var result = new HashSet<int> { seed };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var en in Enemies.Values)
                {
                    var spanned = LinesSpanned(en, rowAxis);
                    if (spanned.Count < 2) continue;
                    if (!spanned.Any(result.Contains)) continue;
                    foreach (var line in spanned)
                        if (result.Add(line)) changed = true;
                }
            }
            return result;
        }

        public ShiftResult ShiftRow(int row, int dir) => Shift(true, row, dir);
        public ShiftResult ShiftCol(int col, int dir) => Shift(false, col, dir);

        /// Ids of all enemies whose body intersects any of the given lines
        /// on the given axis (i.e. the enemies a shift of those lines will
        /// actually move).
        public HashSet<int> EnemiesOnLines(bool rowAxis, HashSet<int> lines)
        {
            var ids = new HashSet<int>();
            foreach (var en in Enemies.Values)
                if (LinesSpanned(en, rowAxis).Any(lines.Contains))
                    ids.Add(en.Id);
            return ids;
        }

        private ShiftResult Shift(bool rowAxis, int seed, int dir)
        {
            var lines = LinkedLinesForAxis(rowAxis, seed);
            var result = new ShiftResult { IsRowShift = rowAxis, Direction = dir, ShiftedLines = lines };
            foreach (var kv in Enemies) result.OldAnchors[kv.Key] = kv.Value.Anchor;

            foreach (var line in lines)
            {
                if (rowAxis) RotateRow(line, dir);
                else RotateCol(line, dir);
            }

            // Bodies on the shifted lines translate rigidly (linked-line
            // expansion guarantees every spanned line moved together), so
            // anchors move by the shift delta instead of being re-derived.
            foreach (var id in EnemiesOnLines(rowAxis, lines))
            {
                var en = Enemies[id];
                en.Anchor = rowAxis
                    ? new Vector2Int(en.Anchor.x, Wrap(en.Anchor.y + dir, Cols))
                    : new Vector2Int(Wrap(en.Anchor.x + dir, Rows), en.Anchor.y);
                result.MovedEnemyIds.Add(id);
            }

            OnShifted?.Invoke(result);
            return result;
        }

        private void RotateRow(int r, int dir)
        {
            var copy = new List<OccupantRef>[Cols];
            for (int c = 0; c < Cols; c++) copy[c] = _cells[r, c];
            for (int c = 0; c < Cols; c++) _cells[r, Wrap(c + dir, Cols)] = copy[c];
        }

        private void RotateCol(int c, int dir)
        {
            var copy = new List<OccupantRef>[Rows];
            for (int r = 0; r < Rows; r++) copy[r] = _cells[r, c];
            for (int r = 0; r < Rows; r++) _cells[Wrap(r + dir, Rows), c] = copy[r];
        }

        public bool EnemyWrapsRows(Enemy en)
        {
            int topLeft = en.Anchor.x + en.BodyMin.x;
            return topLeft < 0 || topLeft + en.SizeRows > Rows;
        }

        public bool EnemyWrapsCols(Enemy en)
        {
            int topLeft = en.Anchor.y + en.BodyMin.y;
            return topLeft < 0 || topLeft + en.SizeCols > Cols;
        }
    }
}
