using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Draws the link display: straight lines from each linking enemy
    /// (Golem, Siren, Mage...) to its targets — other enemy pieces, or the
    /// attack/item cards it has disabled — plus a grow-and-shrink pulse on
    /// the linked enemy and its line whenever an absorber soaks a
    /// redirected hit. ALL styling comes from the linking enemy's
    /// EnemyDefinition.LinkDisplay settings; this component only wires the
    /// ability-list card lookups. Rebuilt every LateUpdate from pooled
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

        private GridState _state;
        private CombatSystem _combat;
        private EnemyViewManager _views;
        private Canvas _canvas;
        private RectTransform _rect;

        private readonly List<Image> _pool = new List<Image>();
        private int _used;

        /// One redirected-hit pulse: overlay over the linked enemy plus a
        /// thickness swell on the absorber's link line. Computational by
        /// default; either half can be animator-driven via the absorber
        /// definition's prefabs.
        private class Pulse
        {
            public int TargetId;
            public int AbsorberId;
            public LinkDisplaySettings Settings;
            public float Elapsed;
            public Image Overlay;                // computational overlay (pooled)
            public AnimationCaller OverlayAnim;  // animator-mode overlay instance
            public AnimationCaller LineAnim;     // animator-mode line curve source
            public bool OverlayDone;
            public bool LineDone;
        }

        private readonly List<Pulse> _pulses = new List<Pulse>();
        private readonly List<Image> _overlayPool = new List<Image>();
        private readonly Dictionary<int, List<(Enemy, Vector2)>> _clusters
            = new Dictionary<int, List<(Enemy, Vector2)>>();

        public void Initialize(GridState state, CombatSystem combat, EnemyViewManager views)
        {
            _state = state;
            _combat = combat;
            _views = views;
            _canvas = GetComponentInParent<Canvas>();
            _rect = (RectTransform)transform;
            _state.OnDamageRedirected += HandleDamageRedirected;
        }

        private void OnDestroy()
        {
            if (_state != null) _state.OnDamageRedirected -= HandleDamageRedirected;
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
                    var settings = en.Definition.LinkDisplay;
                    foreach (var id in en.LinkedIds)
                        if (TryEnemyPoint(id, out var to))
                            Draw(from, to, LineColor(settings),
                                settings.LineThickness * PulseThicknessMultiplier(en.Id, id));
                }

                // Cluster chains (Slime): mutual groups sharing a ClusterId,
                // drawn as a nearest-neighbor chain in the cluster's style.
                _clusters.Clear();
                foreach (var en in _state.Enemies.Values)
                {
                    if (en.ClusterId < 0) continue;
                    if (!TryEnemyPoint(en.Id, out var p)) continue;
                    if (!_clusters.TryGetValue(en.ClusterId, out var list))
                        _clusters[en.ClusterId] = list = new List<(Enemy, Vector2)>();
                    list.Add((en, p));
                }
                foreach (var list in _clusters.Values)
                {
                    if (list.Count < 2) continue;
                    var settings = list[0].Item1.Definition.LinkDisplay;
                    var color = LineColor(settings);
                    var remaining = new List<(Enemy en, Vector2 pos)>(list);
                    var current = remaining[0];
                    remaining.RemoveAt(0);
                    while (remaining.Count > 0)
                    {
                        int nearest = 0;
                        float best = float.MaxValue;
                        for (int i = 0; i < remaining.Count; i++)
                        {
                            float d = (remaining[i].pos - current.pos).sqrMagnitude;
                            if (d < best) { best = d; nearest = i; }
                        }
                        Draw(current.pos, remaining[nearest].pos, color, settings.LineThickness);
                        current = remaining[nearest];
                        remaining.RemoveAt(nearest);
                    }
                }

                // Enemy-to-card links (Mage spells, Siren curses).
                if (_combat != null)
                {
                    foreach (var (sourceId, attack) in _combat.AttackDisableEntries())
                        if (_state.Enemies.TryGetValue(sourceId, out var source)
                            && TryEnemyPoint(sourceId, out var from)
                            && attackList != null && attackList.TryGetAttackCardRect(attack, out var cardRect)
                            && TryCardPoint(cardRect, out var to))
                            Draw(from, to, LineColor(source.Definition.LinkDisplay), source.Definition.LinkDisplay.LineThickness);
                    foreach (var (sourceId, item) in _combat.ItemDisableEntries())
                        if (_state.Enemies.TryGetValue(sourceId, out var source)
                            && TryEnemyPoint(sourceId, out var from)
                            && itemList != null && itemList.TryGetItemCardRect(item, out var itemRect)
                            && TryCardPoint(itemRect, out var to))
                            Draw(from, to, LineColor(source.Definition.LinkDisplay), source.Definition.LinkDisplay.LineThickness);
                }
            }
            for (int i = _used; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);

            UpdatePulses();
        }

        // ---------------- Redirect pulses ----------------

        private void HandleDamageRedirected(Enemy target, Enemy absorber)
        {
            var settings = absorber.Definition.LinkDisplay;
            var pulse = new Pulse
            {
                TargetId = target.Id,
                AbsorberId = absorber.Id,
                Settings = settings,
            };

            if (settings.OverlayAnimatorPrefab != null)
            {
                pulse.OverlayAnim = Instantiate(settings.OverlayAnimatorPrefab, transform);
                pulse.OverlayAnim.PlayPreset(settings.OverlayPresetLabel, () => pulse.OverlayDone = true);
            }
            if (settings.LineAnimatorPrefab != null)
            {
                pulse.LineAnim = Instantiate(settings.LineAnimatorPrefab, transform);
                pulse.LineAnim.PlayPreset(settings.LinePresetLabel, () => pulse.LineDone = true);
            }
            _pulses.Add(pulse);
        }

        private float PulseThicknessMultiplier(int absorberId, int targetId)
        {
            float mult = 1f;
            foreach (var pulse in _pulses)
            {
                if (pulse.AbsorberId != absorberId || pulse.TargetId != targetId) continue;
                mult = Mathf.Max(mult, pulse.LineAnim != null
                    ? Mathf.Max(0.01f, pulse.LineAnim.transform.localScale.y)
                    : 1f + (pulse.Settings.LinePulsePeak - 1f)
                        * Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulse.Elapsed / pulse.Settings.PulseDuration)));
            }
            return mult;
        }

        private void UpdatePulses()
        {
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                var pulse = _pulses[i];
                var settings = pulse.Settings;
                pulse.Elapsed += Time.deltaTime;

                bool targetVisible = _views != null && _views.TryGetMainPiece(pulse.TargetId, out var piece);
                bool finished = pulse.OverlayAnim != null || pulse.LineAnim != null
                    ? (pulse.OverlayAnim == null || pulse.OverlayDone) && (pulse.LineAnim == null || pulse.LineDone)
                    : pulse.Elapsed >= settings.PulseDuration;
                // Animator-mode pulses on a computational half still time out.
                if (pulse.OverlayAnim == null && pulse.Overlay != null && pulse.Elapsed >= settings.PulseDuration)
                { pulse.Overlay.gameObject.SetActive(false); pulse.Overlay = null; }

                if (!targetVisible || finished)
                {
                    ReleasePulse(pulse);
                    _pulses.RemoveAt(i);
                    continue;
                }

                // Follow the piece; computational overlay also scales/fades.
                _views.TryGetMainPiece(pulse.TargetId, out piece);
                var center = ToLocal(piece.RectTransform);
                var pieceSize = piece.RectTransform.rect.size;

                if (pulse.OverlayAnim != null)
                {
                    var rt = (RectTransform)pulse.OverlayAnim.transform;
                    rt.anchoredPosition = center;
                }
                else if (pulse.Elapsed < settings.PulseDuration)
                {
                    float t01 = Mathf.Clamp01(pulse.Elapsed / settings.PulseDuration);
                    float wave = Mathf.Sin(Mathf.PI * t01);
                    if (pulse.Overlay == null) pulse.Overlay = NextOverlay(settings.OverlaySprite);
                    var rt = (RectTransform)pulse.Overlay.transform;
                    rt.anchoredPosition = center;
                    rt.sizeDelta = pieceSize * (1f + (settings.OverlayPulsePeak - 1f) * wave);
                    var c = settings.LinkColor;
                    c.a = settings.OverlayPulseAlpha * wave;
                    pulse.Overlay.color = c;
                }
            }
        }

        private void ReleasePulse(Pulse pulse)
        {
            if (pulse.Overlay != null) pulse.Overlay.gameObject.SetActive(false);
            if (pulse.OverlayAnim != null) Destroy(pulse.OverlayAnim.gameObject);
            if (pulse.LineAnim != null) Destroy(pulse.LineAnim.gameObject);
        }

        private Image NextOverlay(Sprite sprite)
        {
            foreach (var img in _overlayPool)
                if (!img.gameObject.activeSelf)
                {
                    img.sprite = sprite;
                    img.gameObject.SetActive(true);
                    return img;
                }
            var go = new GameObject("LinkHitOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = sprite;
            _overlayPool.Add(image);
            return image;
        }

        // ---------------- Line drawing ----------------

        private static Color LineColor(LinkDisplaySettings settings)
        {
            var c = settings.LinkColor;
            c.a = settings.LineAlpha;
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

        private void Draw(Vector2 a, Vector2 b, Color color, float thickness)
        {
            var img = NextLine();
            var rt = (RectTransform)img.transform;
            var d = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, thickness);
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
