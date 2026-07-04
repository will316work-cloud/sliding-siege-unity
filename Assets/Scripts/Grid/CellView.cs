// ============================================================
// CELL VIEW (UI prefab component)  (NEW in Bunch 2)
// Translated from: the per-cell portion of buildGridDOM() in
// grid-rendering.js — one pooled prefab instance per board cell
// instead of a rebuilt DOM node.
//
// IPointerClickHandler fires for BOTH mouse clicks and touch taps
// via the EventSystem — this single interface is the PC+mobile
// input story for the whole board.
//
// Overlay children map to the JS overlay divs:
//   HitboxOverlay   ← .hitbox-overlay        (attack preview)
//   ItemMarker      ← .item-destination-marker (item preview/destination)
//   AnchorDot       ← .anchor-dot
//   BombBlastZone   ← .bomb-blast-zone        (activated by Bunch 5)
//   SpriteTelegraph ← .sprite-hitbox-telegraph (activated by Bunch 5)
//   SoulCloudHalo   ← soul-cloud overlay      (activated by Bunch 8)
// Unassigned refs are safely ignored, so the prefab can grow with
// later bunches without breaking this one.
//
// TEMPORARY: RenderOccupantsPlaceholder() shows a text summary of
// occupants so shifts/reverts/cycling are verifiable NOW. Bunch 3
// (general-enemy-rendering.js) replaces it with pooled enemy token
// prefabs and hides DebugText.
// ============================================================

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CellView : MonoBehaviour, IPointerClickHandler
{
    [Header("Wiring")]
    public Image Background;
    public RectTransform ContentRoot;         // enemy tokens parent (Bunch 3)
    public TextMeshProUGUI DebugText;          // temporary occupant readout

    [Header("Overlays (enable/disable per refresh)")]
    public GameObject HitboxOverlay;
    public GameObject ItemMarker;
    public GameObject AnchorDot;
    public GameObject BombBlastZone;
    public GameObject SpriteTelegraph;
    public GameObject SoulCloudHalo;

    [HideInInspector] public int R;
    [HideInInspector] public int C;

    public void Init(int r, int c)
    {
        R = r;
        C = c;
        name = $"Cell_{r}_{c}";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CellClickRouter.Route(R, C);
    }

    public void SetOverlays(bool hitbox, bool anchor, bool itemMarker,
                            bool bombBlast, bool spriteTelegraph, bool soulHalo)
    {
        if (HitboxOverlay != null) HitboxOverlay.SetActive(hitbox);
        if (AnchorDot != null) AnchorDot.SetActive(anchor);
        if (ItemMarker != null) ItemMarker.SetActive(itemMarker);
        if (BombBlastZone != null) BombBlastZone.SetActive(bombBlast);
        if (SpriteTelegraph != null) SpriteTelegraph.SetActive(spriteTelegraph);
        if (SoulCloudHalo != null) SoulCloudHalo.SetActive(soulHalo);
    }

    /// <summary>TEMP until Bunch 3. Mirrors what the JS cell shows: every
    /// inline (1x1, non-stretched, non-pending) enemy + spore markers.
    /// Multi-cell enemies are drawn by a separate pass (Bunch 3), so here
    /// they appear as "·" continuation marks for visibility.</summary>
    public void RenderOccupantsPlaceholder(GameState s)
    {
        if (DebugText == null) return;
        var refs = s.Grid[R, C];
        if (refs.Count == 0) { DebugText.text = ""; return; }

        var sb = new StringBuilder();
        foreach (var occ in refs)
        {
            if (sb.Length > 0) sb.Append('\n');
            if (occ.Kind == OccupantKind.SporeCloud) { sb.Append("☁"); continue; }
            if (!s.Enemies.TryGetValue(occ.Id, out var en)) continue;
            if (en.PendingSpawn) continue;

            bool inline = en.Size.x == 1 && en.Size.y == 1
                          && !(GridLogic.IsRolly(en) && en.StretchAxis != null);
            if (!inline) { sb.Append('·'); continue; }

            string icon = Registry.Enemies.TryGetValue(en.Type, out var def) && !string.IsNullOrEmpty(def.IconText)
                ? def.IconText
                : en.Label;
            sb.Append(icon).Append(' ').Append(Mathf.Max(0, en.Hp));
        }
        DebugText.text = sb.ToString();
    }
}
