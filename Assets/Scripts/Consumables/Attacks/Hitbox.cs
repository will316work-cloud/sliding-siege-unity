using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// One selectable arrangement of an attack (the unit the player cycles
    /// through by re-tapping the anchor). Parts are resolved first to last;
    /// a cell covered by an earlier part keeps that part's damage percent.
    [Serializable]
    public class Hitbox
    {
        [Tooltip("Shown on the Confirm button while this arrangement is active.")]
        public string Label;

        public HitboxPart[] Parts = new HitboxPart[0];

        public List<HitCell> Resolve(GridState state, Vector2Int anchor)
        {
            var hits = new List<HitCell>();
            if (Parts == null) return hits;
            var claimed = new HashSet<Vector2Int>();
            foreach (var part in Parts)
                foreach (var cell in part.GetCells(state, anchor))
                    if (claimed.Add(cell))
                        hits.Add(new HitCell(cell, part.DamageFactor));
            return hits;
        }
    }

    /// One resolved cell of a hitbox, carrying the damage percent of the
    /// part that claimed it.
    public struct HitCell
    {
        public Vector2Int Cell;
        public float DamageFactor;

        public HitCell(Vector2Int cell, float damageFactor)
        {
            Cell = cell;
            DamageFactor = damageFactor;
        }
    }
}
