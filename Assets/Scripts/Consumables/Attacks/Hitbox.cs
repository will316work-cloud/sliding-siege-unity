using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// One selectable arrangement of an attack (the unit the player cycles
    /// through by re-tapping the anchor). Parts are resolved first to last;
    /// a cell covered by an earlier part keeps that part's damage factor
    /// and highlight appearance.
    [Serializable]
    public class Hitbox
    {
        [Tooltip("Shown on the Confirm button while this arrangement is active.")]
        public string Label;

        public HitboxPart[] Parts = new HitboxPart[0];

        /// First part, e.g. as the appearance carrier for procedural
        /// previews. Null when the hitbox has no parts.
        public HitboxPart FirstPart => Parts != null && Parts.Length > 0 ? Parts[0] : null;

        public List<HitCell> Resolve(GridState state, Vector2Int anchor)
        {
            var hits = new List<HitCell>();
            if (Parts == null) return hits;
            var claimed = new HashSet<Vector2Int>();
            foreach (var part in Parts)
                foreach (var cell in part.GetCells(state, anchor))
                    if (claimed.Add(cell))
                        hits.Add(new HitCell(cell, part));
            return hits;
        }
    }

    /// One resolved cell of a hitbox, carrying the part that claimed it
    /// (earlier parts win overlaps — damage factor and highlight alike).
    public struct HitCell
    {
        public Vector2Int Cell;
        public HitboxPart Part;

        public float DamageFactor => Part?.DamageFactor ?? 1f;

        public HitCell(Vector2Int cell, HitboxPart part)
        {
            Cell = cell;
            Part = part;
        }
    }
}
