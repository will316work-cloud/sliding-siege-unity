// ============================================================
// GRID VIEW  (NEW in Bunch 2)
// Translated from: buildGridDOM() in grid-rendering.js — but
// pooled instead of rebuilt. The JS tore down and recreated the
// whole DOM every render(); here the cell/button instances are
// created once (or when dimensions change) and refreshed in place
// on every GameEvents.StateChanged.
//
// Layout replaces the JS flex/grid CSS with built-in UGUI layout
// components (GridLayoutGroup / Horizontal- / VerticalLayoutGroup)
// — see the Bunch 2 scene instructions for the exact hierarchy.
// This script force-syncs cell sizes so row/col buttons always
// align with the board (replacing the JS getBoundingClientRect
// width-matching hack at buildGridDOM lines 343–347).
//
// Overlay passes still owned by later bunches (the JS computed
// these in buildGridDOM; each hook's owner is noted):
//   ItemPreviewCellsHook            → Bunch 7 (getItemPreviewCells)
//   BombBlastCellsHook              → Bunch 5 (getBombBlastCells per bomb)
//   SpriteTelegraphCellsHook        → Bunch 5 (spriteShapeCells)
//   SoulCloudCellsHook              → Bunch 8 (getSoulCloudExpandedCells)
//   Bunch 3 adds the multi-cell block / rolly block / thread-link
//   passes onto OverlayRoot (JS: drawMultiCellEnemyBlocks etc.).
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridView : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private CellView cellPrefab;
    [SerializeField] private ShiftButton shiftButtonPrefab;

    [Header("Containers (see scene instructions)")]
    [SerializeField] private RectTransform gridPanel;        // has GridLayoutGroup
    [SerializeField] private RectTransform topColTrack;      // ▲ buttons
    [SerializeField] private RectTransform bottomColTrack;   // ▼ buttons
    [SerializeField] private RectTransform leftRowTrack;     // ◀ buttons
    [SerializeField] private RectTransform rightRowTrack;    // ▶ buttons
    [SerializeField] private RectTransform overlayRoot;      // multi-cell blocks/threads (Bunch 3)

    [Header("Metrics (JS: cell 52px + 4px gap, board ≤ 420px)")]
    [SerializeField] private float cellSize = 80f;
    [SerializeField] private float gap = 6f;
    [SerializeField] private float shiftButtonThickness = 44f;   // touch-friendly ≥ 44

    // ---- Later-bunch overlay hooks (null = feature absent) ----
    public static Func<string, Vector2Int, List<Vector2Int>> ItemPreviewCellsHook = null;   // Bunch 7
    public static Func<GameState, HashSet<Vector2Int>> BombBlastCellsHook = null;           // Bunch 5
    public static Func<GameState, HashSet<Vector2Int>> SpriteTelegraphCellsHook = null;     // Bunch 5
    public static Func<GameState, HashSet<Vector2Int>> SoulCloudHaloCellsHook = null;       // Bunch 8

    private readonly List<CellView> cells = new List<CellView>();
    private readonly List<ShiftButton> rowButtons = new List<ShiftButton>();
    private readonly List<ShiftButton> colButtons = new List<ShiftButton>();
    private int builtRows = -1, builtCols = -1;

    public RectTransform OverlayRoot => overlayRoot;
    public float CellSize => cellSize;
    public float Gap => gap;
    public float Stride => cellSize + gap;   // JS getGridMetrics().stride

    private void OnEnable() { GameEvents.StateChanged += Refresh; }
    private void OnDisable() { GameEvents.StateChanged -= Refresh; }
    private void Start() { if (GameManager.S != null) Refresh(); }

    // ============================================================
    // Build (once per board dimension)
    // ============================================================

    private void EnsureBuilt(GameState s)
    {
        if (builtRows == s.Rows && builtCols == s.Cols) return;
        builtRows = s.Rows; builtCols = s.Cols;

        ClearChildren(gridPanel); cells.Clear();
        ClearChildren(topColTrack); ClearChildren(bottomColTrack);
        ClearChildren(leftRowTrack); ClearChildren(rightRowTrack);
        rowButtons.Clear(); colButtons.Clear();

        // Configure the built-in GridLayoutGroup (replaces CSS grid template).
        var layout = gridPanel.GetComponent<GridLayoutGroup>();
        if (layout != null)
        {
            layout.cellSize = new Vector2(cellSize, cellSize);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = s.Cols;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;   // row-major, matches r/c loops
        }
        float boardW = s.Cols * cellSize + (s.Cols - 1) * gap;
        float boardH = s.Rows * cellSize + (s.Rows - 1) * gap;
        gridPanel.sizeDelta = new Vector2(boardW, boardH);
        if (overlayRoot != null) overlayRoot.sizeDelta = new Vector2(boardW, boardH);

        // Cells, row-major so drawing order matches the JS loops.
        for (int r = 0; r < s.Rows; r++)
        {
            for (int c = 0; c < s.Cols; c++)
            {
                var cell = Instantiate(cellPrefab, gridPanel);
                cell.Init(r, c);
                cells.Add(cell);
            }
        }

        // Row buttons: ◀ on the left track, ▶ on the right track.
        for (int r = 0; r < s.Rows; r++)
        {
            rowButtons.Add(MakeButton(leftRowTrack, ShiftButton.Axis.Row, r, -1,
                new Vector2(shiftButtonThickness, cellSize)));
            rowButtons.Add(MakeButton(rightRowTrack, ShiftButton.Axis.Row, r, 1,
                new Vector2(shiftButtonThickness, cellSize)));
        }
        // Col buttons: ▲ on the top track, ▼ on the bottom track.
        for (int c = 0; c < s.Cols; c++)
        {
            colButtons.Add(MakeButton(topColTrack, ShiftButton.Axis.Col, c, -1,
                new Vector2(cellSize, shiftButtonThickness)));
            colButtons.Add(MakeButton(bottomColTrack, ShiftButton.Axis.Col, c, 1,
                new Vector2(cellSize, shiftButtonThickness)));
        }
    }

    private ShiftButton MakeButton(RectTransform parent, ShiftButton.Axis axis, int idx, int dir, Vector2 size)
    {
        var btn = Instantiate(shiftButtonPrefab, parent);
        btn.Init(axis, idx, dir);
        var le = btn.GetComponent<LayoutElement>();
        if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size.x;
        le.preferredHeight = size.y;
        return btn;
    }

    private static void ClearChildren(RectTransform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    // ============================================================
    // Refresh (JS: everything buildGridDOM computed per render)
    // ============================================================

    public void Refresh()
    {
        var s = GameManager.S;
        if (s == null || cellPrefab == null || shiftButtonPrefab == null) return;
        EnsureBuilt(s);

        // JS line 62: gridButtonsDisabled
        bool gridButtonsDisabled = s.EnemyPhaseActive
            || (!DebugFlags.InfiniteAttacks && s.AttacksRemainingThisTurn <= 0)
            || DebugFlags.OtherDebugInteractionsLocked();

        // ---- Preview cell sets (JS lines 104–124) ----
        var hitboxCells = new HashSet<Vector2Int>();
        Vector2Int? anchorCell = null;
        var pt = Session.PreviewTarget;
        if (pt != null && Registry.Attacks.TryGetValue(pt.Attack, out var atkDef))
        {
            foreach (var p in atkDef.GetAttackCells(s, pt.R, pt.C, pt.Variant))
                hitboxCells.Add(p);
            anchorCell = new Vector2Int(pt.R, pt.C);
        }

        var itemCells = new HashSet<Vector2Int>();
        Vector2Int? itemAnchor = null;
        if (Session.ItemTargetingMode != null && Session.ItemPreviewCell.HasValue && ItemPreviewCellsHook != null)
        {
            foreach (var p in ItemPreviewCellsHook(Session.ItemTargetingMode, Session.ItemPreviewCell.Value))
                itemCells.Add(p);
            itemAnchor = Session.ItemPreviewCell.Value;
        }
        if (Session.ItemSecondTarget.HasValue && ItemPreviewCellsHook != null)
        {
            foreach (var p in ItemPreviewCellsHook("teleport", Session.ItemSecondTarget.Value))
                itemCells.Add(p);   // JS: isItemDestination uses the same marker class
        }

        var bombCells = BombBlastCellsHook != null ? BombBlastCellsHook(s) : null;
        var telegraphCells = SpriteTelegraphCellsHook != null ? SpriteTelegraphCellsHook(s) : null;
        var soulHaloCells = SoulCloudHaloCellsHook != null ? SoulCloudHaloCellsHook(s) : null;

        // ---- Cells ----
        foreach (var cell in cells)
        {
            var pos = new Vector2Int(cell.R, cell.C);
            cell.SetOverlays(
                hitbox: hitboxCells.Contains(pos),
                anchor: (anchorCell.HasValue && anchorCell.Value == pos)
                     || (itemAnchor.HasValue && itemAnchor.Value == pos),
                itemMarker: itemCells.Contains(pos),
                bombBlast: bombCells != null && bombCells.Contains(pos),
                spriteTelegraph: telegraphCells != null && telegraphCells.Contains(pos),
                soulHalo: soulHaloCells != null && soulHaloCells.Contains(pos));
            cell.RenderOccupantsPlaceholder(s);   // TEMP — Bunch 3 replaces
        }

        // ---- Shift buttons (JS .used / .disabled / .line-disabled) ----
        foreach (var btn in rowButtons)
        {
            btn.SetState(
                used: s.RowsTouchedThisTurn.Contains(btn.Index),
                disabled: gridButtonsDisabled,
                lineDisabled: GridLogic.IsLineDisabledHook("row", btn.Index));
        }
        foreach (var btn in colButtons)
        {
            btn.SetState(
                used: s.ColsTouchedThisTurn.Contains(btn.Index),
                disabled: gridButtonsDisabled,
                lineDisabled: GridLogic.IsLineDisabledHook("col", btn.Index));
        }
    }

    // ============================================================
    // Coordinate helpers for overlay passes (JS: getGridMetrics /
    // cellElAt). Bunch 3's multi-cell block drawer positions
    // RectTransforms on OverlayRoot with these.
    // ============================================================

    /// <summary>Anchored position (top-left pivot space) of cell (r, c)
    /// on OverlayRoot. JS: pxTop(i)/pxLeft(i) in drawMultiCellEnemyBlocks.</summary>
    public Vector2 CellAnchoredPos(int r, int c)
        => new Vector2(c * Stride, -r * Stride);

    /// <summary>JS: spanPx(n) = n*cellSize + (n-1)*gap.</summary>
    public float SpanPx(int n) => n * cellSize + (n - 1) * gap;

    public CellView CellAt(int r, int c)
    {
        int idx = r * builtCols + c;
        return idx >= 0 && idx < cells.Count ? cells[idx] : null;
    }
}
