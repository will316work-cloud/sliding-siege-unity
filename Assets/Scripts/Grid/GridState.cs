using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Pure back-end grid model. No UnityEngine.UI dependencies.
    /// Mirrors the HTML game's model: each cell holds a LIST of occupant refs,
    /// multi-cell enemies occupy one ref per covered cell, anchors are
    /// recomputed from the refs after every shift, and shifting a line
    /// recursively drags every line spanned by any multi-cell enemy touching
    /// the already-included lines (fixed-point expansion).
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
        public event Action<Enemy, Vector2Int> OnEnemyMoved; // (enemy, oldAnchor)
        public event Action OnRebuilt;

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
            for (int dr = 0; dr < def.SizeRows; dr++)
                for (int dc = 0; dc < def.SizeCols; dc++)
                    if (_cells[Wrap(r + dr, Rows), Wrap(c + dc, Cols)].Count > 0)
                        return false;
            return true;
        }

        public Enemy SpawnEnemy(EnemyDefinition def, int r, int c)
        {
            var enemy = new Enemy { Id = _nextEnemyId++, Definition = def, Anchor = new Vector2Int(r, c), HP = def.MaxHP };
            for (int dr = 0; dr < def.SizeRows; dr++)
                for (int dc = 0; dc < def.SizeCols; dc++)
                    _cells[Wrap(r + dr, Rows), Wrap(c + dc, Cols)].Add(new OccupantRef(OccupantKind.Enemy, enemy.Id));
            Enemies[enemy.Id] = enemy;
            OnEnemySpawned?.Invoke(enemy);
            return enemy;
        }

        /// True if a footprint of the given size fits at (r, c) (wrapped),
        /// treating cells occupied only by `ignoreEnemyId` as free.
        public bool CanPlaceAtIgnoring(int r, int c, int sizeRows, int sizeCols, int ignoreEnemyId)
        {
            for (int dr = 0; dr < sizeRows; dr++)
                for (int dc = 0; dc < sizeCols; dc++)
                {
                    var refs = _cells[Wrap(r + dr, Rows), Wrap(c + dc, Cols)];
                    foreach (var rf in refs)
                        if (!(rf.Kind == OccupantKind.Enemy && rf.Id == ignoreEnemyId))
                            return false;
                }
            return true;
        }

        /// Moves an enemy's anchor (footprint refs are re-laid; wrapped).
        /// Caller must have validated with CanPlaceAtIgnoring.
        public void MoveEnemy(int id, Vector2Int newAnchor)
        {
            if (!Enemies.TryGetValue(id, out var enemy)) return;
            Vector2Int old = enemy.Anchor;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    _cells[r, c].RemoveAll(rf => rf.Kind == OccupantKind.Enemy && rf.Id == id);
            enemy.Anchor = new Vector2Int(Wrap(newAnchor.x, Rows), Wrap(newAnchor.y, Cols));
            for (int dr = 0; dr < enemy.SizeRows; dr++)
                for (int dc = 0; dc < enemy.SizeCols; dc++)
                    _cells[Wrap(enemy.Anchor.x + dr, Rows), Wrap(enemy.Anchor.y + dc, Cols)].Add(new OccupantRef(OccupantKind.Enemy, id));
            OnEnemyMoved?.Invoke(enemy, old);
        }

        public void RemoveEnemy(int id)
        {
            if (!Enemies.TryGetValue(id, out var enemy)) return;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    _cells[r, c].RemoveAll(rf => rf.Kind == OccupantKind.Enemy && rf.Id == id);
            Enemies.Remove(id);
            OnEnemyRemoved?.Invoke(enemy);
        }

        // ---------------- Shifting ----------------

        /// Fixed-point expansion: any multi-cell enemy whose footprint spans a
        /// line already in the set drags ALL lines it spans into the set.
        public HashSet<int> LinkedLinesForAxis(bool rowAxis, int seed)
        {
            var result = new HashSet<int> { seed };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var en in Enemies.Values)
                {
                    int axisSize = rowAxis ? en.SizeRows : en.SizeCols;
                    if (axisSize < 2) continue;

                    int anchorOnAxis = rowAxis ? en.Anchor.x : en.Anchor.y;
                    int gridSize = rowAxis ? Rows : Cols;

                    var fullRange = new int[axisSize];
                    for (int i = 0; i < axisSize; i++)
                        fullRange[i] = Wrap(anchorOnAxis + i, gridSize);

                    if (!fullRange.Any(result.Contains)) continue;
                    foreach (var idx in fullRange)
                        if (result.Add(idx)) changed = true;
                }
            }
            return result;
        }

        public ShiftResult ShiftRow(int row, int dir) => Shift(true, row, dir);
        public ShiftResult ShiftCol(int col, int dir) => Shift(false, col, dir);

        /// Ids of all enemies whose footprint intersects any of the given
        /// lines on the given axis (i.e. the enemies a shift of those lines
        /// will actually move).
        public HashSet<int> EnemiesOnLines(bool rowAxis, HashSet<int> lines)
        {
            var ids = new HashSet<int>();
            foreach (var en in Enemies.Values)
            {
                int axisSize = rowAxis ? en.SizeRows : en.SizeCols;
                int anchorOnAxis = rowAxis ? en.Anchor.x : en.Anchor.y;
                int gridSize = rowAxis ? Rows : Cols;
                for (int i = 0; i < axisSize; i++)
                    if (lines.Contains(Wrap(anchorOnAxis + i, gridSize))) { ids.Add(en.Id); break; }
            }
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

            RecomputeAnchors();

            foreach (var kv in Enemies)
                if (result.OldAnchors[kv.Key] != kv.Value.Anchor)
                    result.MovedEnemyIds.Add(kv.Key);

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

        /// Rebuilds every enemy's anchor by scanning cell refs, exactly like
        /// the HTML game's recomputeAnchors(): collect all covered cells per
        /// enemy id, then find the wrap-aware start index on each axis.
        public void RecomputeAnchors()
        {
            var seen = new Dictionary<int, List<Vector2Int>>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    foreach (var rf in _cells[r, c])
                    {
                        if (rf.Kind != OccupantKind.Enemy) continue;
                        if (!seen.TryGetValue(rf.Id, out var list))
                            seen[rf.Id] = list = new List<Vector2Int>();
                        list.Add(new Vector2Int(r, c));
                    }

            foreach (var kv in seen)
            {
                if (!Enemies.TryGetValue(kv.Key, out var en)) continue;
                var cells = kv.Value;
                if (en.SizeRows == 1 && en.SizeCols == 1) { en.Anchor = cells[0]; continue; }

                var rowIdx = new HashSet<int>(cells.Select(p => p.x));
                var colIdx = new HashSet<int>(cells.Select(p => p.y));
                en.Anchor = new Vector2Int(
                    FindAxisStart(rowIdx, Rows, en.SizeRows),
                    FindAxisStart(colIdx, Cols, en.SizeCols));
            }
        }

        /// Finds the index where a wrap-aware contiguous run of `size` begins.
        private static int FindAxisStart(HashSet<int> indices, int gridSize, int size)
        {
            var sorted = indices.OrderBy(i => i).ToList();
            if (size <= 1 || sorted.Count <= 1) return sorted[0];
            foreach (var candidate in sorted)
            {
                bool matches = true;
                for (int k = 0; k < size; k++)
                    if (!indices.Contains(((candidate + k) % gridSize + gridSize) % gridSize)) { matches = false; break; }
                if (matches) return candidate;
            }
            return sorted[0];
        }

        public bool EnemyWrapsRows(Enemy en) => en.Anchor.x + en.SizeRows > Rows;
        public bool EnemyWrapsCols(Enemy en) => en.Anchor.y + en.SizeCols > Cols;
    }
}
