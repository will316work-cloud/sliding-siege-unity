using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Returns the grid cells an attack hits (mirrors the HTML game's
    /// ATTACK_CELL_RESOLVERS registry). Cells are in-bounds only, no wrap,
    /// matching the JS resolvers.
    public interface IAttackShapeResolver
    {
        int VariantCount { get; }
        string VariantLabel(int variantIndex);
        List<Vector2Int> GetCells(GridState state, Vector2Int anchor, int variantIndex);
    }
}
