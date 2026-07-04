// ============================================================
// SHIFT BUTTON (UI prefab component)  (NEW in Bunch 2)
// Translated from: the .row-btn / .col-btn divs in buildGridDOM()
// (grid-rendering.js) — including their three CSS state classes:
//   .used          → dimmed (line already shifted this turn)
//   .disabled      → non-interactable (enemy phase / no attacks left)
//   .line-disabled → curse badge shown, click still allowed so the
//                    "cannot move" toast fires (matches JS, where the
//                    onclick stayed attached and shiftRow/Col aborts)
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShiftButton : MonoBehaviour, IPointerClickHandler
{
    public enum Axis { Row, Col }

    [Header("Wiring")]
    public TextMeshProUGUI Glyph;
    public GameObject LineDisabledBadge;   // JS: lineDisabledBadgeHTML()
    public CanvasGroup Group;              // used for the .used / .disabled dimming

    [HideInInspector] public Axis ButtonAxis;
    [HideInInspector] public int Index;
    [HideInInspector] public int Dir;

    private bool interactionBlocked;

    public void Init(Axis axis, int index, int dir)
    {
        ButtonAxis = axis;
        Index = index;
        Dir = dir;
        // JS glyphs: ◀ ▶ for rows, ▲ ▼ for cols
        if (Glyph != null)
            Glyph.text = axis == Axis.Row ? (dir == -1 ? "◀" : "▶") : (dir == -1 ? "▲" : "▼");
        name = $"{axis}Btn_{index}_{(dir == 1 ? "pos" : "neg")}";
    }

    /// <summary>Called by GridView every refresh — mirrors the JS class list.</summary>
    public void SetState(bool used, bool disabled, bool lineDisabled)
    {
        interactionBlocked = disabled;   // JS .disabled blocked via CSS pointer-events
        if (Group != null)
        {
            Group.alpha = disabled ? 0.3f : (used ? 0.45f : 1f);
        }
        if (LineDisabledBadge != null) LineDisabledBadge.SetActive(lineDisabled);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (interactionBlocked)
        {
            // JS parity: .disabled buttons still surface WHY via the shift
            // guards' toasts when tapped during enemy phase / locked grid.
            // GridLogic re-checks everything, so just forward.
        }
        if (ButtonAxis == Axis.Row) GridLogic.ShiftRow(Index, Dir);
        else GridLogic.ShiftCol(Index, Dir);
    }
}
