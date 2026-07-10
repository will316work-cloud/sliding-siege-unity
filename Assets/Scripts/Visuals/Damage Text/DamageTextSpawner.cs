using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace SlidingSiege
{
    /// Spawns pooled damage/heal text indicators. Listens to every enemy's
    /// OnHealthLost / OnHealthGained events (hooked on spawn, unhooked on
    /// removal) and pops one indicator at the CENTER of EVERY piece of that
    /// enemy (main + wrap ghosts), on the Indicator Layer. Indicators return
    /// to the pool when their AnimationCaller preset completes (with a
    /// safety timeout in case a preset is misconfigured).
    public class DamageTextSpawner : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private DamageTextIndicator indicatorPrefab;
        [Tooltip("Unmasked layer above the Enemy Layer so text isn't clipped at grid edges.")]
        [SerializeField] private RectTransform indicatorLayer;

        [Header("Behavior")]
        [Tooltip("Random horizontal jitter in pixels applied to each indicator (± this value).")]
        [SerializeField, Min(0f)] private float horizontalJitter = 15f;
        [Tooltip("Safety release if the animation never reports completion.")]
        [SerializeField, Min(0.1f)] private float maxLifetime = 3f;

        private GridState _state;
        private EnemyViewManager _viewManager;
        private ObjectPool<DamageTextIndicator> _pool;
        private readonly Dictionary<int, (System.Action<int> lost, System.Action<int> gained)> _hooks
            = new Dictionary<int, (System.Action<int>, System.Action<int>)>();

        public void Initialize(GridState state, EnemyViewManager viewManager)
        {
            _state = state;
            _viewManager = viewManager;

            _pool ??= new ObjectPool<DamageTextIndicator>(
                createFunc: () => Instantiate(indicatorPrefab, indicatorLayer),
                actionOnGet: ind => { ind.gameObject.SetActive(true); ind.transform.SetParent(indicatorLayer, false); },
                actionOnRelease: ind => ind.gameObject.SetActive(false),
                actionOnDestroy: ind => Destroy(ind.gameObject),
                defaultCapacity: 12);

            _state.OnEnemySpawned += HookEnemy;
            _state.OnEnemyRemoved += UnhookEnemy;
            foreach (var en in _state.Enemies.Values) HookEnemy(en);
        }

        private void OnDestroy()
        {
            if (_state == null) return;
            _state.OnEnemySpawned -= HookEnemy;
            _state.OnEnemyRemoved -= UnhookEnemy;
            foreach (var en in _state.Enemies.Values) UnhookEnemy(en);
        }

        private void HookEnemy(Enemy en)
        {
            if (_hooks.ContainsKey(en.Id)) return;
            System.Action<int> lost = amount => SpawnForEnemy(en, amount, false);
            System.Action<int> gained = amount => SpawnForEnemy(en, amount, true);
            en.OnHealthLost += lost;
            en.OnHealthGained += gained;
            _hooks[en.Id] = (lost, gained);
        }

        private void UnhookEnemy(Enemy en)
        {
            if (!_hooks.TryGetValue(en.Id, out var hooks)) return;
            en.OnHealthLost -= hooks.lost;
            en.OnHealthGained -= hooks.gained;
            _hooks.Remove(en.Id);
        }

        /// Same-frame batching: multiple damage instances landing on one
        /// enemy in a single frame (attack hit + redirected link damage,
        /// blast + absorb) merge into ONE summed indicator, flushed at end
        /// of frame. Heals batch separately from damage — both can show.
        private class Pending
        {
            public List<RectTransform> Rects; // captured on first hit, in case the enemy dies this frame
            public int Damage;
            public int Heal;
        }

        private readonly Dictionary<int, Pending> _pending = new Dictionary<int, Pending>();

        private void SpawnForEnemy(Enemy en, int amount, bool isHeal)
        {
            if (!_pending.TryGetValue(en.Id, out var pending))
            {
                if (!_viewManager.TryGetPieceRects(en.Id, out var pieceRects)) return;
                _pending[en.Id] = pending = new Pending { Rects = pieceRects };
            }
            if (isHeal) pending.Heal += amount;
            else pending.Damage += amount;
        }

        /// Flush in Update — one frame after the damage landed. Activating a
        /// pooled indicator any later in the frame (LateUpdate/end-of-frame)
        /// loses the Play: the Animator rebinds on its next evaluation and
        /// resets to the default state, so the completion poll released the
        /// indicator immediately. Update-phase activation evaluates the same
        /// frame, exactly like the original per-hit spawning did.
        private void Update()
        {
            if (_pending.Count == 0) return;
            foreach (var pending in _pending.Values)
                foreach (var pieceRect in pending.Rects)
                {
                    if (pieceRect == null) continue; // piece destroyed this frame
                    if (pending.Damage > 0) SpawnAt(pieceRect, pending.Damage, false);
                    if (pending.Heal > 0) SpawnAt(pieceRect, pending.Heal, true);
                }
            _pending.Clear();
        }

        private void SpawnAt(RectTransform pieceRect, int amount, bool isHeal)
        {
            var indicator = _pool.Get();

            // World-space center of the piece, converted onto the layer,
            // plus horizontal jitter (matching the HTML game's wobble).
            Vector3 worldCenter = pieceRect.TransformPoint(pieceRect.rect.center);
            indicator.transform.position = worldCenter;
            indicator.transform.localPosition += new Vector3(
                Random.Range(-horizontalJitter, horizontalJitter), 0f, 0f);
            indicator.transform.SetAsLastSibling();

            bool released = false;
            void Release()
            {
                if (released) return;
                released = true;
                _pool.Release(indicator);
            }

            indicator.Show(amount, isHeal, Release);
            StartCoroutine(SafetyRelease(Release));
        }

        private IEnumerator SafetyRelease(System.Action release)
        {
            yield return new WaitForSeconds(maxLifetime);
            release();
        }
    }
}
