// ============================================================
// ATTACK DEFINITION (ScriptableObject)
// Translated from: ATTACK_DEFS[key] = { name, icon, baseDmg,
// variants, defaultVariant, desc } in each *-attack-logic.js,
// PLUS that file's ATTACK_CELL_RESOLVERS[key] function — the
// resolver becomes the abstract GetAttackCells() override.
//
// Bunch 6 supplies the five concrete subclasses
// (AxeAttackDefinition, SwordAttackDefinition, ...) and you'll
// create one .asset per attack under Assets/Data/Attacks/.
//
// REMINDER: BaseDmg here is the pristine DEFAULT. Runtime reads
// go through GameState.AttackBaseDmg[Key] (shop can upgrade it
// per-run) — never mutate this asset at runtime.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public abstract class AttackDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Registry key, e.g. \"axe\". Must match everywhere the JS used the key.")]
    public string Key;
    public string DisplayName;

    [Tooltip("Sprite icon (your art, later). Leave empty to fall back to IconText.")]
    public Sprite Icon;
    [Tooltip("Placeholder text/emoji icon used while Icon is unassigned, e.g. 🪓")]
    public string IconText;

    [TextArea(3, 6)]
    public string Description;

    [Header("Combat")]
    public int BaseDmg;

    [Tooltip("Variant keys, e.g. row/col for axe, plus/x for crystal. Empty if none.")]
    public string[] Variants;
    public string DefaultVariant;

    [Header("Economy")]
    [Tooltip("Charges at run start. JS: hardcoded 2 for all five attacks.")]
    public int StartingCharges = 2;

    /// <summary>The ATTACK_CELL_RESOLVERS[key] function: which cells does this
    /// attack hit when targeted at (r, c) with the given variant.
    /// Vector2Int convention: x = row, y = col.</summary>
    public abstract List<Vector2Int> GetAttackCells(GameState state, int r, int c, string variant);
}
