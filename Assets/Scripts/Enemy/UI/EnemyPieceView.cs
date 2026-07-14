using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Root component of the enemy piece prefab:
    ///   Enemy Piece (EnemyPieceView + CanvasGroup) — sized to the footprint
    ///     ├─ Sprite (body Image)
    ///     │    └─ Face (face Image, stretch-anchored inside the body)
    ///     ├─ Health Bar Whole (EnemyHealthBarDisplay)
    ///     ├─ Effect Display (status-effect icons)
    ///     └─ Enemy Remains (death particles)
    ///          ├─ Explosion Flakes
    ///          └─ Explosion Cloud
    /// Every piece (main + wrap ghosts) has its own health bar; they all
    /// bind to the same enemy so they display the same health.
    [RequireComponent(typeof(CanvasGroup))]
    public class EnemyPieceView : MonoBehaviour
    {
        #region Serialized Fields


        [SerializeField] private Image spriteImage;
        [SerializeField] private Image faceImage;
        [SerializeField] private EnemyHealthBarDisplay healthBar;
        [Tooltip("Optional: lets enemy abilities play Animator presets on this piece.")]
        [SerializeField] private AnimationCaller animationCaller;

        [Header("Enemy remains")]
        [SerializeField] private ParticleSystem enemyRemains;
        [SerializeField] private ParticleSystem explosionFlakes;
        [SerializeField] private ParticleSystem explosionCloud;
        [Tooltip("Piece edge length (px) at which the remains particles look as authored; the shape scale is multiplied by shortestEdge / this.")]
        [SerializeField, Min(1f)] private float remainsReferenceSize = 80f;


        #endregion

        #region Private Fields


        private CanvasGroup _canvasGroup;
        private EnemyShape _faceSource;
        private Animator _animator;
        private RuntimeAnimatorController _defaultController;

        /// The prefab's authored colors/shape per system, captured on first
        /// use so re-applying overlays on pooled pieces never compounds.
        private class RemainsBase
        {
            public ParticleSystem.MinMaxGradient StartColor;
            public bool HasColorOverLifetime;
            public ParticleSystem.MinMaxGradient ColorOverLifetime;
            public Vector3 ShapeScale;
        }

        private readonly Dictionary<ParticleSystem, RemainsBase> _remainsBases = new Dictionary<ParticleSystem, RemainsBase>();


        #endregion

        #region Properties


        public Image SpriteImage => spriteImage;
        public Image FaceImage => faceImage;
        public EnemyHealthBarDisplay HealthBar => healthBar;

        /// Falls back to searching this piece if not assigned in the prefab.
        public AnimationCaller AnimationCaller
        {
            get
            {
                if (animationCaller == null)
                    animationCaller = GetComponentInChildren<AnimationCaller>(true);
                return animationCaller;
            }
        }

        public RectTransform RectTransform => (RectTransform)transform;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null) 
                    _canvasGroup = GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }


        #endregion

        #region MonoBehavior Callbacks


        /// The Animator may curve the face sprite during clips (expressions);
        /// the shape's face settings are the DEFAULT, restored whenever a
        /// preset finishes so clip-end values don't stick. Returning to Idle
        /// is driven manually (EnemyViewManager / EnemyAbilityContext replay
        /// the Idle preset on completion) — the Animator has no exit-time
        /// transitions back to Enemy Idle.
        private void Awake()
        {
            var caller = AnimationCaller;
            if (caller == null) return;
            foreach (var preset in caller.Presets)
                preset.OnComplete.AddListener(ReapplyFace);
        }


        #endregion

        #region Public Methods


        /// Pool-reuse cleanup: a previous Death clip leaves the sprite hidden
        /// and the remains active (Write Defaults is off, so animated
        /// GameObject toggles persist). Code owns these outside the clips.
        public void ResetToggles()
        {
            if (spriteImage != null && !spriteImage.gameObject.activeInHierarchy)
                spriteImage.gameObject.SetActive(true);
            if (enemyRemains != null && enemyRemains.gameObject.activeInHierarchy)
                enemyRemains.gameObject.SetActive(false);
        }

        /// Sets the shape whose face Image settings/padding this piece shows
        /// and applies them now; animation clips override while they play.
        public void SetFaceSource(EnemyShape shape)
        {
            _faceSource = shape;
            ReapplyFace();
        }

        public void ReapplyFace() => _faceSource?.ApplyFaceTo(faceImage);

        /// Swaps in the definition's Animator controller; null restores the
        /// prefab's default (pooled pieces rebind across enemy types).
        public void SetAnimatorController(RuntimeAnimatorController controller)
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();

            if (_animator == null) return;
            if (_defaultController == null) _defaultController = _animator.runtimeAnimatorController;
            var target = controller != null ? controller : _defaultController;
            if (_animator.runtimeAnimatorController != target)
            {
                _animator.runtimeAnimatorController = target;
                // The layer/parameter caches AnimationCaller built at Awake()
                // describe whichever controller was bound back then; a swap
                // here invalidates them (different controllers can have
                // different layers/parameters).
                AnimationCaller?.RefreshAnimatorBindings();
            }
        }

        /// Applies the definition's per-system color overlays (multiplied
        /// over every authored color), centers the Enemy Remains root on the
        /// piece, and scales each system's emission shape to the piece size.
        /// pieceSize is the piece root's rect size in pixels.
        public void ApplyRemains(Color remainsOverlay, Color flakesOverlay, Color cloudOverlay, Vector2 pieceSize)
        {
            if (enemyRemains != null)
            {
                // Piece root pivot is center-middle at runtime (see EnemyView),
                // so the piece center is the local origin.
                Transform t = enemyRemains.transform;
                t.localPosition = new Vector3(0f, 0f, t.localPosition.z);
            }

            _applyRemainsSystem(enemyRemains, remainsOverlay, pieceSize);
            _applyRemainsSystem(explosionFlakes, flakesOverlay, pieceSize);
            _applyRemainsSystem(explosionCloud, cloudOverlay, pieceSize);
        }


        #endregion

        #region Private Methods


        private void _applyRemainsSystem(ParticleSystem ps, Color overlay, Vector2 pieceSize)
        {
            if (ps == null) return;
            if (!_remainsBases.TryGetValue(ps, out var baseData))
            {
                var col = ps.colorOverLifetime;
                baseData = new RemainsBase
                {
                    StartColor = ps.main.startColor,
                    HasColorOverLifetime = col.enabled,
                    ColorOverLifetime = col.enabled ? col.color : default,
                    ShapeScale = ps.shape.scale,
                };
                _remainsBases[ps] = baseData;
            }

            var main = ps.main;
            main.startColor = _tint(baseData.StartColor, overlay);
            if (baseData.HasColorOverLifetime)
            {
                var col = ps.colorOverLifetime;
                col.color = _tint(baseData.ColorOverLifetime, overlay);
            }

            // Square scale: the authored shape uniformly scaled by the
            // piece's shortest edge relative to the authored piece size.
            float factor = Mathf.Min(pieceSize.x, pieceSize.y) / remainsReferenceSize;
            var shape = ps.shape;
            shape.scale = baseData.ShapeScale * factor;
        }

        /// Multiplies every color in the MinMaxGradient by the overlay.
        private static ParticleSystem.MinMaxGradient _tint(ParticleSystem.MinMaxGradient g, Color overlay)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(g.color * overlay);
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(g.colorMin * overlay, g.colorMax * overlay);
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(_tint(g.gradient, overlay));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(_tint(g.gradientMin, overlay), _tint(g.gradientMax, overlay));
                case ParticleSystemGradientMode.RandomColor:
                    ParticleSystem.MinMaxGradient random = new ParticleSystem.MinMaxGradient(_tint(g.gradient, overlay));
                    random.mode = ParticleSystemGradientMode.RandomColor;
                    return random;
                default:
                    return g;
            }
        }

        private static Gradient _tint(Gradient gradient, Color overlay)
        {
            if (gradient == null)
                return null;

            var colorKeys = gradient.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
                colorKeys[i].color *= overlay;

            var alphaKeys = gradient.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
                alphaKeys[i].alpha *= overlay.a;

            var tinted = new Gradient { mode = gradient.mode };
            tinted.SetKeys(colorKeys, alphaKeys);
            return tinted;
        }


        #endregion
    }
}
