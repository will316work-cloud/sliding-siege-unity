using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Draws the link display: straight lines from each linking enemy
    /// (Golem, Siren, Mage...) to its targets — other enemy pieces, or the
    /// attack/item cards it has disabled. Colors come from the source's
    /// EnemyDefinition.LinkColor. Rebuilt every LateUpdate from pooled
    /// Images so lines follow tweening pieces for free.
    ///
    /// Scene setup: put this on a full-screen stretched RectTransform (pivot
    /// centered) layered ABOVE the Enemy Layer, and assign the two ability
    /// lists. SlidingGridController wires the rest at startup.
    public class LinkOverlay : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private AbilityListUI attackList;
        [SerializeField] private AbilityListUI itemList;

        [Header("Appearance")]
        [SerializeField, Min(1f)] private float lineThickness = 4f;
        [SerializeField, Range(0f, 1f)] private float lineAlpha = 0.75f;

        private GridState _state;
        private CombatSystem _combat;
        private EnemyViewManager _views;
        private Canvas _canvas;
        private RectTransform _rect;

        private readonly List<Image> _pool = new List<Image>();
        private int _used;

        public void Initialize(GridState state, CombatSystem combat, EnemyViewManager views)
        {
            _state = state;
            _combat = combat;
            _views = views;
            _canvas = GetComponentInParent<Canvas>();
            _rect = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            _used = 0;
            if (_state != null)
            {
                // Enemy-to-enemy links (Golem, Siren).
                foreach (var en in _state.Enemies.Values)
                {
                    if (en.LinkedIds.Count == 0) continue;
                    if (!TryEnemyPoint(en.Id, out var from)) continue;
                    var color = LineColor(en);
                    foreach (var id in en.LinkedIds)
                        if (TryEnemyPoint(id, out var to))
                            Draw(from, to, color);
                }

                // Enemy-to-card links (Mage spells, Siren curses).
                if (_combat != null)
                {
                    foreach (var (sourceId, attack) in _combat.AttackDisableEntries())
                        if (_state.Enemies.TryGetValue(sourceId, out var source)
                            && TryEnemyPoint(sourceId, out var from)
                            && attackList != null && attackList.TryGetAttackCardRect(attack, out var cardRect)
                            && TryCardPoint(cardRect, out var to))
                            Draw(from, to, LineColor(source));
                    foreach (var (sourceId, item) in _combat.ItemDisableEntries())
                        if (_state.Enemies.TryGetValue(sourceId, out var source)
                            && TryEnemyPoint(sourceId, out var from)
                            && itemList != null && itemList.TryGetItemCardRect(item, out var itemRect)
                            && TryCardPoint(itemRect, out var to))
                            Draw(from, to, LineColor(source));
                }
            }
            for (int i = _used; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        }

        private Color LineColor(Enemy source)
        {
            var c = source.Definition.LinkColor;
            c.a = lineAlpha;
            return c;
        }

        private bool TryEnemyPoint(int enemyId, out Vector2 local)
        {
            local = default;
            if (_views == null || !_views.TryGetMainPiece(enemyId, out var piece)) return false;
            local = ToLocal(piece.RectTransform);
            return true;
        }

        private bool TryCardPoint(RectTransform rect, out Vector2 local)
        {
            local = default;
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            local = ToLocal(rect);
            return true;
        }

        private Vector2 ToLocal(RectTransform target)
        {
            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;
            var screen = RectTransformUtility.WorldToScreenPoint(cam, target.TransformPoint(target.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, cam, out var local);
            return local;
        }

        private void Draw(Vector2 a, Vector2 b, Color color)
        {
            var img = NextLine();
            var rt = (RectTransform)img.transform;
            var d = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, lineThickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            img.color = color;
            if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
        }

        private Image NextLine()
        {
            if (_used < _pool.Count) return _pool[_used++];
            var go = new GameObject("LinkLine", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            _pool.Add(img);
            _used++;
            return img;
        }
    }
}
