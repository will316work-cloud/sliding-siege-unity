// ============================================================
// ITEM DEFINITION (ScriptableObject)
// Translated from: ITEM_DEFS[key] in general-item-logic.js /
// each *-item-logic.js, plus the ITEM_EFFECT_RESOLVERS registry.
//
// Bunch 7 supplies the five concrete subclasses (ExtraSwingItem,
// GravityItem, ExpandSoulItem, TeleportItem, VulnerabilityItem)
// and fills in the targeting/apply virtuals to exactly mirror
// itemOnCellClick / applySelectedItem flow. Create one .asset per
// item under Assets/Data/Items/.
// ============================================================

using System.Collections;
using UnityEngine;

public abstract class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Registry key, e.g. \"extraSwing\". Must match the JS key exactly.")]
    public string Key;
    public string DisplayName;

    public Sprite Icon;
    [Tooltip("Placeholder text/emoji icon while Icon is unassigned.")]
    public string IconText;

    [TextArea(3, 6)]
    public string Description;

    [Header("Economy")]
    [Tooltip("Uses at run start. JS: hardcoded 2 for all five items.")]
    public int StartingUses = 2;

    [Header("Targeting")]
    [Tooltip("False for instant items (extraSwing). True for items that need a cell tap.")]
    public bool RequiresCellTarget = true;
    [Tooltip("True only for teleport-style items needing a second destination tap.")]
    public bool RequiresSecondTarget = false;

    /// <summary>Is (r, c) a legal first target right now? Drives cell
    /// highlighting and click acceptance. Overridden in Bunch 7.</summary>
    public virtual bool IsValidTarget(GameState state, int r, int c) => true;

    /// <summary>Is (r, c) a legal SECOND target (teleport destination)?</summary>
    public virtual bool IsValidSecondTarget(GameState state, int r, int c) => true;

    /// <summary>Apply the item's effect. Coroutine because several JS item
    /// applications were async (awaited animations between sub-steps).
    /// Reads its targets from Session.ItemPreviewCell / ItemSecondTarget,
    /// exactly like the JS read the loose globals.</summary>
    public abstract IEnumerator Apply(GameState state);
}
